using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Pure team-size parsing and deterministic team allocation.
/// Unity-facing code supplies the number of team presentation slots that are available.
/// </summary>
public static class TeamAllocation
{
    public const int MinimumTeamCount = 2;

    private static readonly char[] TeamSizeSeparators =
    {
        ',', ';', ' ', '\t', '\r', '\n'
    };

    /// <summary>
    /// Resolves the requested team sizes. Automatic mode balances the remainder across
    /// the first teams; detailed mode accepts comma, semicolon, or whitespace separators.
    /// </summary>
    public static bool TryBuildTeamSizes(
        int playerCount,
        int teamCount,
        int presentationCapacity,
        bool useDetailedSizes,
        string detailedSizesText,
        out int[] teamSizes,
        out string error)
    {
        teamSizes = Array.Empty<int>();
        error = string.Empty;

        if (presentationCapacity < MinimumTeamCount)
        {
            error = $"표시 가능한 팀 슬롯이 최소 {MinimumTeamCount}개 필요합니다. " +
                    $"(현재 {presentationCapacity}개)";
            return false;
        }

        if (teamCount < MinimumTeamCount || teamCount > presentationCapacity)
        {
            error = $"팀 수는 {MinimumTeamCount}~{presentationCapacity} 사이여야 합니다. " +
                    $"(현재 {teamCount}팀)";
            return false;
        }

        if (playerCount < teamCount)
        {
            error = $"플레이어 수({playerCount}명)가 팀 수({teamCount}팀)보다 적습니다.";
            return false;
        }

        if (!useDetailedSizes)
        {
            int quotient = playerCount / teamCount;
            int remainder = playerCount % teamCount;
            teamSizes = Enumerable.Range(0, teamCount)
                .Select(index => quotient + (index < remainder ? 1 : 0))
                .ToArray();
            return true;
        }

        string[] tokens = (detailedSizesText ?? string.Empty).Split(
            TeamSizeSeparators,
            StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != teamCount)
        {
            error = $"팀별 인원수를 정확히 {teamCount}개 입력해야 합니다. " +
                    $"(현재 {tokens.Length}개)";
            return false;
        }

        int[] parsedSizes = new int[teamCount];
        long parsedTotal = 0;
        for (int index = 0; index < tokens.Length; index++)
        {
            if (!int.TryParse(
                    tokens[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedSize) || parsedSize <= 0)
            {
                error = $"{index + 1}팀 인원수는 0보다 큰 정수여야 합니다. " +
                        $"(입력값: {tokens[index]})";
                return false;
            }

            parsedSizes[index] = parsedSize;
            parsedTotal += parsedSize;
        }

        if (parsedTotal != playerCount)
        {
            error = $"팀별 인원수 합계가 전체 플레이어 수와 같아야 합니다. " +
                    $"(합계 {parsedTotal}명 / 전체 {playerCount}명)";
            return false;
        }

        teamSizes = parsedSizes;
        return true;
    }

    /// <summary>
    /// Generates teams using only the supplied random source. Reusing the same seed and
    /// inputs therefore produces the same allocation.
    /// </summary>
    public static bool TryGenerateTeams(
        IReadOnlyList<string> players,
        IReadOnlyList<string> extras,
        IReadOnlyList<int> requestedTeamSizes,
        System.Random random,
        out List<List<string>> teams,
        out string error)
    {
        teams = new List<List<string>>();
        error = string.Empty;

        if (random == null)
        {
            error = "팀 배정에 사용할 난수 생성기가 없습니다.";
            return false;
        }

        if (requestedTeamSizes == null || requestedTeamSizes.Count < MinimumTeamCount)
        {
            error = $"요청된 팀은 최소 {MinimumTeamCount}개여야 합니다.";
            return false;
        }

        long requestedPlayerTotal = 0;
        for (int index = 0; index < requestedTeamSizes.Count; index++)
        {
            int size = requestedTeamSizes[index];
            if (size <= 0)
            {
                error = $"{index + 1}팀 인원수는 0보다 커야 합니다. (현재 {size}명)";
                return false;
            }

            requestedPlayerTotal += size;
        }

        if (!TryNormalizeUniqueNames(players, "플레이어", out List<string> normalizedPlayers, out error))
            return false;

        IReadOnlyList<string> extraValues = extras ?? Array.Empty<string>();
        if (!TryNormalizeUniqueNames(extraValues, "깍두기", out List<string> normalizedExtras, out error))
            return false;

        if (requestedPlayerTotal != normalizedPlayers.Count)
        {
            error = $"요청된 팀 인원수 합계가 플레이어 수와 같아야 합니다. " +
                    $"(합계 {requestedPlayerTotal}명 / 플레이어 {normalizedPlayers.Count}명)";
            return false;
        }

        HashSet<string> playerSet = new HashSet<string>(normalizedPlayers, StringComparer.Ordinal);
        foreach (string extra in normalizedExtras)
        {
            if (!playerSet.Contains(extra))
            {
                error = $"깍두기 [{extra}]가 플레이어 명단에 없습니다.";
                return false;
            }
        }

        for (int index = 0; index < requestedTeamSizes.Count; index++)
            teams.Add(new List<string>(requestedTeamSizes[index]));

        List<int> preferredTeamOrder = BuildPreferredTeamOrder(requestedTeamSizes, random);
        List<string> shuffledExtras = new List<string>(normalizedExtras);
        ShuffleInPlace(shuffledExtras, random);

        int nextPreferredPosition = 0;
        for (int extraIndex = 0; extraIndex < shuffledExtras.Count; extraIndex++)
        {
            int selectedPosition = -1;
            for (int offset = 0; offset < preferredTeamOrder.Count; offset++)
            {
                int candidatePosition = (nextPreferredPosition + offset) % preferredTeamOrder.Count;
                int candidateTeam = preferredTeamOrder[candidatePosition];
                if (teams[candidateTeam].Count < requestedTeamSizes[candidateTeam])
                {
                    selectedPosition = candidatePosition;
                    break;
                }
            }

            if (selectedPosition < 0)
            {
                teams.Clear();
                error = "깍두기를 배치할 수 있는 팀 자리가 부족합니다.";
                return false;
            }

            int selectedTeam = preferredTeamOrder[selectedPosition];
            teams[selectedTeam].Add(shuffledExtras[extraIndex]);
            nextPreferredPosition = (selectedPosition + 1) % preferredTeamOrder.Count;
        }

        HashSet<string> extraSet = new HashSet<string>(normalizedExtras, StringComparer.Ordinal);
        List<string> normalPlayers = normalizedPlayers
            .Where(player => !extraSet.Contains(player))
            .ToList();
        ShuffleInPlace(normalPlayers, random);

        int nextNormalIndex = 0;
        for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
        {
            while (teams[teamIndex].Count < requestedTeamSizes[teamIndex])
            {
                if (nextNormalIndex >= normalPlayers.Count)
                {
                    teams.Clear();
                    error = "팀 배정 중 일반 플레이어가 부족해졌습니다.";
                    return false;
                }

                teams[teamIndex].Add(normalPlayers[nextNormalIndex]);
                nextNormalIndex++;
            }
        }

        if (nextNormalIndex != normalPlayers.Count ||
            teams.Where((team, index) => team.Count != requestedTeamSizes[index]).Any())
        {
            teams.Clear();
            error = "모든 플레이어를 요청된 팀 인원수에 맞게 배정하지 못했습니다.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeUniqueNames(
        IReadOnlyList<string> values,
        string label,
        out List<string> normalized,
        out string error)
    {
        normalized = new List<string>();
        error = string.Empty;

        if (values == null)
        {
            error = $"{label} 명단이 없습니다.";
            return false;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"{label} 명단의 {index + 1}번째 이름이 비어 있습니다.";
                return false;
            }

            string trimmed = value.Trim();
            if (!seen.Add(trimmed))
            {
                error = $"{label} 명단에 중복된 이름 [{trimmed}]이 있습니다.";
                return false;
            }

            normalized.Add(trimmed);
        }

        return true;
    }

    private static List<int> BuildPreferredTeamOrder(
        IReadOnlyList<int> requestedTeamSizes,
        System.Random random)
    {
        List<int> result = new List<int>(requestedTeamSizes.Count);
        IEnumerable<IGrouping<int, int>> sizeGroups = Enumerable.Range(0, requestedTeamSizes.Count)
            .GroupBy(index => requestedTeamSizes[index])
            .OrderByDescending(group => group.Key);

        foreach (IGrouping<int, int> sizeGroup in sizeGroups)
        {
            List<int> tiedTeamIndices = sizeGroup.ToList();
            ShuffleInPlace(tiedTeamIndices, random);
            result.AddRange(tiedTeamIndices);
        }

        return result;
    }

    private static void ShuffleInPlace<T>(IList<T> values, System.Random random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            T value = values[index];
            values[index] = values[swapIndex];
            values[swapIndex] = value;
        }
    }
}
