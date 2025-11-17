using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using System.Text;

public class TeamRandomizer : MonoBehaviour
{
    [Header("UI & 이펙트")]
    [SerializeField] private TMP_Text[] teamTexts;                  // 6개
    [SerializeField] private ParticleSystem[] teamEffects;          // 팀 완성 팡
    [SerializeField] private ParticleSystem[] teamMemberEffects;    // 멤버 등장 팡
    [SerializeField] private AudioSource effect;                    // 팡 사운드
    [SerializeField] private RandomMoveLight randomMoveLight;       // 라이트 포커스
    [SerializeField] private GameObject[] TeamTitles;          // 팀 이름들 (UI 오브젝트)
    [SerializeField] private MusicPlayer musicPlayer;                // 음악 플레이어

    [SerializeField] private GameObject SettingCanvas;          // 설정 UI
    [SerializeField] private Button shuffleButton;                         // 셔플 버튼
    [SerializeField] private GameObject CompletedText;                 // 완료 텍스트
    [SerializeField] private Subscription subscription;                // 자막 출력 스크립트

    [Header("옵션")]
    [SerializeField] private Toggle noDuplicateToggle;              // 이전 팀 중복 방지 옵션
    [SerializeField] private Toggle fixedSeedToggle;                // 시드 고정 옵션
    [SerializeField] private TMP_InputField seedInputField;         // 시드 입력 필드

    [Header("플레이어 명단")]
    [SerializeField] private List<string> players = new();   // 여기에는 "기본 18명"만 넣기

    [Header("깍두기")]
    [SerializeField] private List<string> extras = new();

    [Header("Exit Button")]
    [SerializeField] private GameObject ExitButton;                // 종료 버튼

    [Header("For Debugging")]
    [SerializeField] private TextMeshProUGUI ErrorText;            // 에러 메시지 출력용

    private Dictionary<string, HashSet<string>> bannedPairs;
    private List<List<string>> teams = new();
    private List<string> teamsToShow = new();

    private System.Random rng;
    private void Awake()
    {
        // 토글 이벤트 등록
        fixedSeedToggle.onValueChanged.AddListener(OnFixedSeedToggleChanged);
        // 초깃값 반영
        OnFixedSeedToggleChanged(fixedSeedToggle.isOn);

        shuffleButton.onClick.AddListener(ShuffleTeams);
    }

    private void OnFixedSeedToggleChanged(bool isOn)
    {
        // 고정 시드 사용 설정에 따라 입력 필드 활성화
        seedInputField.gameObject.SetActive(isOn);
    }
    public void ShuffleTeams()
    {
        shuffleButton.interactable = false;
        SettingCanvas.SetActive(false);

        // RNG 초기화
        if (fixedSeedToggle.isOn)
        {
            if (!int.TryParse(seedInputField.text, out int seed))
            {
                Debug.LogWarning("잘못된 시드값입니다. 기본 시드(0)로 고정합니다.");
                ErrorText.text = "시드값이 잘못되었습니다. 기본 시드(0) 사용.";
                seed = 0;
            }
            rng = new System.Random(seed);
        }
        else
        {
            rng = new System.Random();
        }

        // --- 인원 검증 ---
        if (players.Count != 18)
        {
            ErrorText.text = $"플레이어 인원은 18명이어야 합니다. (현재 {players.Count}명)";
            shuffleButton.interactable = true;
            SettingCanvas.SetActive(true);
            return;
        }

        if (extras.Count < 0 || extras.Count > 3)
        {
            ErrorText.text = "깍두기는 0~3명이어야 합니다.";
            shuffleButton.interactable = true;
            SettingCanvas.SetActive(true);
            return;
        }

        // 깍두기 players 포함 체크
        foreach (var k in extras)
        {
            if (!players.Contains(k))
            {
                ErrorText.text = $"깍두기 [{k}] 가 players 리스트에 없습니다.";
                shuffleButton.interactable = true;
                SettingCanvas.SetActive(true);
                return;
            }
        }

        // --- 팀 초기화 (5팀, 4/4/4/3/3) ---
        teams.Clear();
        teamsToShow.Clear();

        for (int i = 0; i < 5; i++)
            teams.Add(new List<string>());

        // ---------- 1) 깍두기 배치 ----------
        int K = extras.Count;

        // 4인 팀(0,1,2) 중에서 깍두기를 넣을 팀을 K개 뽑기
        var fourTeamIndices = Enumerable.Range(0, 3).ToList(); // 0,1,2
        fourTeamIndices = fourTeamIndices.OrderBy(_ => rng.Next()).ToList();

        var selectedTeams = fourTeamIndices.Take(K).ToList();

        // 깍두기를 섞고 배치
        var shuffledextras = extras.OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < K; i++)
        {
            int teamIndex = selectedTeams[i];
            teams[teamIndex].Add(shuffledextras[i]);  // 한 팀당 깍두기 1명
        }

        // ---------- 2) 일반 인원 섞기 ----------
        var normals = players
            .Where(p => !extras.Contains(p))
            .OrderBy(_ => rng.Next())
            .ToList();

        int idx = 0;

        // ---------- 3) 4인 팀(0-2) 자리 채우기 ----------
        for (int teamIndex = 0; teamIndex < 3; teamIndex++)
        {
            while (teams[teamIndex].Count < 4)
                teams[teamIndex].Add(normals[idx++]);
        }

        // ---------- 4) 3인 팀(3-4) 자리 채우기 ----------
        for (int teamIndex = 3; teamIndex < 5; teamIndex++)
        {
            while (teams[teamIndex].Count < 3)
                teams[teamIndex].Add(normals[idx++]);
        }

        // 18명 정확히 소진됐는지 검증
        if (idx != normals.Count)
        {
            ErrorText.text = "팀 인원 배분 오류(18명 구성 불일치).";
            shuffleButton.interactable = true;
            SettingCanvas.SetActive(true);
            return;
        }

        // 화면 표시 및 CSV
        foreach (var t in teams)
            teamsToShow.AddRange(t);

        SaveResultsToCsv();
        StartCoroutine(PlayTeamReveal());
    }


    // --------------------------------------------------------
    // PreviousTeams.txt 로드
    private List<List<string>> LoadPreviousTeamsFromFile()
    {
        // Application.dataPath는 빌드 시 *Data 폴더* 경로이므로, 상위 폴더(exe가 있는 위치)로 이동
        string dataPath = Application.dataPath;
        string exeFolder = Path.GetDirectoryName(dataPath);
        string filePath = Path.Combine(exeFolder, "PreviousTeams.txt");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {filePath}");
            ErrorText.text = $"파일을 찾을 수 없습니다: {filePath}";
            
            return null;
        }

        var lines = File.ReadAllLines(filePath);
        var teams = new List<List<string>>();

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("팀"))
            {
                if (i + 1 < lines.Length)
                {
                    var members = lines[i + 1]
                        .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                    if (members.Count == 4)
                    {
                        teams.Add(members);
                        teamsToShow.AddRange(members);
                        if(players.Count < 28)
                            players.AddRange(members);
                    }
                    else
                    {

                        Debug.LogWarning($"라인 형식 오류 (멤버 수 !=4): {lines[i + 1]}");
                        ErrorText.text = $"라인 형식 오류 (멤버 수 !=4): {lines[i + 1]}";
                    }
                }
            }
        }

        return teams;
    }
    // --------------------------------------------------------
    // CSV 파일로 결과 저장
    private void SaveResultsToCsv()
    {
        string dataPath = Application.dataPath;
        string exeFolder = Path.GetDirectoryName(dataPath);
        string filePath = Path.Combine(exeFolder, "Result.csv");

        try
        {
            using (var sw = new StreamWriter(filePath, false, new UTF8Encoding(true))) // UTF-8 with BOM
            {
                sw.WriteLine("Team,Member1,Member2,Member3,Member4");
                for (int i = 0; i < teams.Count; i++)
                {
                    var members = teams[i];

                    string m1 = members.Count > 0 ? members[0] : "";
                    string m2 = members.Count > 1 ? members[1] : "";
                    string m3 = members.Count > 2 ? members[2] : "";
                    string m4 = members.Count > 3 ? members[3] : "";

                    sw.WriteLine($"Team{i + 1},{m1},{m2},{m3},{m4}");
                }
            }
            Debug.Log($"Result.csv saved to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save Result.csv: {ex}");
            ErrorText.text = $"Failed to save Result.csv: {ex}";
        }
    }

    // --------------------------------------------------------
    // 팀 생성 로직
    private List<List<string>> GenerateRound2Teams(
        List<string> allPlayers,
        Dictionary<string, HashSet<string>> banned,
        int maxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var shuffled = allPlayers.OrderBy(_ => rng.Next()).ToList();
            var candidate = new List<List<string>>();
            bool valid = true;

            for (int i = 0; i < 7; i++)
            {
                var group = shuffled.Skip(i * 4).Take(4).ToList();
                if (HasConflict(group, banned))
                {
                    valid = false;
                    break;
                }
                candidate.Add(group);
            }

            if (valid)
                return candidate;
        }
        return null;
    }


    private bool HasConflict(List<string> group, Dictionary<string, HashSet<string>> banned)
    {
        foreach (var a in group)
            foreach (var b in group)
                if (a != b && banned.TryGetValue(a, out var set) && set.Contains(b))
                    return true;
        return false;
    }

    private Dictionary<string, HashSet<string>> BuildBannedPairs(List<List<string>> teams)
    {
        var dict = new Dictionary<string, HashSet<string>>();
        foreach (var team in teams)
        {
            foreach (var p in team)
                if (!dict.ContainsKey(p))
                    dict[p] = new HashSet<string>();

            for (int i = 0; i < team.Count; i++)
                for (int j = i + 1; j < team.Count; j++)
                {
                    dict[team[i]].Add(team[j]);
                    dict[team[j]].Add(team[i]);
                }
        }
        return dict;
    }

    // --------------------------------------------------------
    // 팀 공개 연출
    private IEnumerator PlayTeamReveal()
    {
        subscription.GoSub();
        musicPlayer.Play();
        randomMoveLight.GoLight();
        yield return new WaitForSeconds(30f);

        for (int ti = 0; ti < teams.Count; ti++)
        {
            var group = teams[ti];
            teamTexts[ti].text = "";
            TeamTitles[ti].SetActive(true);

            for (int mi = 0; mi < group.Count; mi++)
            {
                var name = group[mi];

                bool isLastTeam = ti == teams.Count - 1;
                bool isLastMember = mi == group.Count - 1;

                if (isLastTeam && isLastMember)
                {
                    // 마지막 팀의 마지막 멤버는 딜레이 후 '팡' 등장만
                    CameraFocusController.Instance.FocusOnTeam(teamTexts[ti].transform, 2.8f, 0.4f);
                    randomMoveLight.FocusLightOnTeam(ti);

                    CameraFocusController.Instance.ShakeCamera(1f, 3f);

                    // 먼저 가짜 이름(김경호) 보여주고
                    teamTexts[ti].text += "김경호";

                    yield return new WaitForSeconds(3f);

                    // 이펙트 & 사운드
                    teamMemberEffects[ti].gameObject.SetActive(true);
                    teamMemberEffects[ti].Play();
                    effect.Play();

                    // 텍스트를 실제 이름으로 교체
                    teamTexts[ti].text = ReplaceLastLine(teamTexts[ti].text, name);
                    teamsToShow.Remove(name);

                    // 팝 애니메이션
                    teamTexts[ti].transform
                        .DOScale(1.1f, 0.08f)
                        .SetEase(Ease.OutBack)
                        .OnComplete(() => teamTexts[ti].transform.DOScale(1f, 0.08f));

                    teamMemberEffects[ti].transform.position =
                        new Vector3(teamMemberEffects[ti].transform.position.x,
                                    teamMemberEffects[ti].transform.position.y - 0.45f);
                }
                else
                {
                    // 기존 룰렛 방식
                    CameraFocusController.Instance.FocusOnTeam(teamTexts[ti].transform, 2.8f, 0.4f);
                    randomMoveLight.FocusLightOnTeam(ti);
                    teamMemberEffects[ti].gameObject.SetActive(true);
                    teamMemberEffects[ti].Play();

                    yield return StartCoroutine(PlayNameRoulette(ti, name));

                    effect.Play();

                    if (!string.IsNullOrEmpty(teamTexts[ti].text))
                        teamTexts[ti].text += "\n";

                    //teamTexts[ti].text += name;
                    //teamsToShow.Remove(name);

                    teamTexts[ti].transform
                        .DOScale(1.1f, 0.08f)
                        .SetEase(Ease.OutBack)
                        .OnComplete(() => teamTexts[ti].transform.DOScale(1f, 0.08f));

                    teamMemberEffects[ti].transform.position =
                        new Vector3(teamMemberEffects[ti].transform.position.x,
                                    teamMemberEffects[ti].transform.position.y - 0.45f);

                    yield return new WaitForSeconds(0.3f);
                }
            }

            // 팀 완성 팡!
            teamEffects[ti].gameObject.SetActive(true);
            teamEffects[ti].Play();
            SoundManager.Instance.Play("Pop");
            teamTexts[ti].transform
                .DOShakeScale(0.6f, 0.6f, 10, 90);

            yield return new WaitForSeconds(0.5f);
            CameraFocusController.Instance.ResetFocus(0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        randomMoveLight.GoLight();

        yield return new WaitForSeconds(0.5f);
        CompletedText.SetActive(true);
        ExitButton.SetActive(true);
    }

    private IEnumerator PlayNameRoulette(int teamIndex, string finalName)
    {
        float duration = UnityEngine.Random.value +0.8f;
        float elapsed = 0f;
        float interval = 0.05f;

        var candidates = teamsToShow.ToList();

        candidates.Add("<color=#FFD700><size=120%>김홍일 강사님</size></color>");
        candidates.Add("<color=#FFD700><size=120%>김경호 강사님</size></color>");

        if (!candidates.Contains(finalName))
            candidates.Add(finalName); // 혹시 빠졌을 경우 대비

        while (elapsed < duration)
        {
            string randomName = PickWeightedName(candidates);
            teamTexts[teamIndex].text = ReplaceLastLine(teamTexts[teamIndex].text, randomName);

            teamMemberEffects[teamIndex].Play();
            effect.Play();

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // 최종 이름으로 고정
        teamTexts[teamIndex].text = AppendFinalName(teamTexts[teamIndex].text, finalName);
    }

    private string ReplaceLastLine(string original, string newLine)
    {
        var lines = original.Split('\n').ToList();
        if (lines.Count > 0)
            lines[lines.Count - 1] = newLine;
        return string.Join("\n", lines);
    }

    private string AppendFinalName(string currentText, string finalName)
    {
        var lines = currentText.Split('\n').ToList();
        if (lines.Contains(finalName)) return currentText; // 이미 있음
        lines[lines.Count - 1] = finalName;
        return string.Join("\n", lines);
    }

    string PickWeightedName(List<string> candidates)
    {
        string picked;
        while (true)
        {
            picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            if (picked.Contains("강사님"))
            {
                if (UnityEngine.Random.value < 0.05f)
                    return picked;
            }
            else
            {
                return picked;
            }
        }
    }
}
