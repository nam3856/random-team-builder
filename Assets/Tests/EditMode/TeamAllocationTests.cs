using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace RandomGame.Tests.EditMode
{
    public sealed class TeamAllocationTests
    {
        private static Type AllocationType
        {
            get
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("TeamAllocation", false))
                    .FirstOrDefault(candidate => candidate != null);

                Assert.That(type, Is.Not.Null, "The runtime TeamAllocation type must be compiled.");
                return type;
            }
        }

        [TestCase(2, "9,9")]
        [TestCase(3, "6,6,6")]
        [TestCase(4, "5,5,4,4")]
        [TestCase(5, "4,4,4,3,3")]
        [TestCase(6, "3,3,3,3,3,3")]
        public void AutomaticSizes_SupportEveryAvailableTeamCount(int teamCount, string expectedText)
        {
            bool success = TryBuildTeamSizes(18, teamCount, 6, false, string.Empty, out int[] sizes, out string error);
            int[] expected = expectedText.Split(',').Select(int.Parse).ToArray();

            Assert.That(success, Is.True, error);
            Assert.That(sizes, Is.EqualTo(expected));
        }

        [Test]
        public void DetailedSizes_AcceptCommaSpaceAndSemicolonSeparators()
        {
            bool success = TryBuildTeamSizes(18, 5, 6, true, "5, 4;3 3\t3", out int[] sizes, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(sizes, Is.EqualTo(new[] { 5, 4, 3, 3, 3 }));
        }

        [TestCase(1, 6, "4")]
        [TestCase(7, 6, "3 3 3 3 3 3 3")]
        public void TeamCount_OutsidePresentationCapacity_IsRejected(
            int teamCount,
            int capacity,
            string detailedSizes)
        {
            bool success = TryBuildTeamSizes(18, teamCount, capacity, true, detailedSizes, out int[] sizes, out string error);

            Assert.That(success, Is.False);
            Assert.That(sizes, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase("4 4 4 3", TestName = "Wrong entry count")]
        [TestCase("4 4 4 3 0", TestName = "Non-positive entry")]
        [TestCase("4 4 4 4 4", TestName = "Wrong player total")]
        [TestCase("4 4 four 3 3", TestName = "Non-integer entry")]
        public void InvalidDetailedSizes_AreRejected(string detailedSizes)
        {
            bool success = TryBuildTeamSizes(18, 5, 6, true, detailedSizes, out int[] sizes, out string error);

            Assert.That(success, Is.False);
            Assert.That(sizes, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void GenerateTeams_IsDeterministicAndPlacesExtrasOnDistinctLargerTeams()
        {
            string[] players = Enumerable.Range(1, 14)
                .Select(index => "P" + index.ToString("00"))
                .ToArray();
            string[] extras = { "P01", "P02" };
            int[] requestedSizes = { 4, 4, 4, 2 };

            bool firstSuccess = TryGenerateTeams(
                players,
                extras,
                requestedSizes,
                new System.Random(9321),
                out List<List<string>> first,
                out string firstError);
            bool secondSuccess = TryGenerateTeams(
                players,
                extras,
                requestedSizes,
                new System.Random(9321),
                out List<List<string>> second,
                out string secondError);

            Assert.That(firstSuccess, Is.True, firstError);
            Assert.That(secondSuccess, Is.True, secondError);
            Assert.That(Serialize(first), Is.EqualTo(Serialize(second)));
            Assert.That(first.Select(team => team.Count), Is.EqualTo(requestedSizes));
            Assert.That(
                first.SelectMany(team => team).OrderBy(value => value, StringComparer.Ordinal),
                Is.EqualTo(players.OrderBy(value => value, StringComparer.Ordinal)));

            int firstExtraTeam = first.FindIndex(team => team.Contains(extras[0]));
            int secondExtraTeam = first.FindIndex(team => team.Contains(extras[1]));
            Assert.That(firstExtraTeam, Is.InRange(0, 2));
            Assert.That(secondExtraTeam, Is.InRange(0, 2));
            Assert.That(firstExtraTeam, Is.Not.EqualTo(secondExtraTeam));
        }

        [Test]
        public void GenerateTeams_TrimsNamesBeforeUniquenessAndAllocation()
        {
            string[] players = { " Alice ", "Bob", "Carol", "Dave" };

            bool success = TryGenerateTeams(
                players,
                Array.Empty<string>(),
                new[] { 2, 2 },
                new System.Random(1),
                out List<List<string>> teams,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(teams.SelectMany(team => team), Does.Contain("Alice"));
            Assert.That(teams.SelectMany(team => team), Does.Not.Contain(" Alice "));
        }

        [Test]
        public void GenerateTeams_RejectsDuplicateOrBlankPlayers()
        {
            AssertGenerationFails(new[] { "A", " A ", "B", "C" }, Array.Empty<string>(), new[] { 2, 2 });
            AssertGenerationFails(new[] { "A", " ", "B", "C" }, Array.Empty<string>(), new[] { 2, 2 });
        }

        [Test]
        public void GenerateTeams_RejectsInvalidExtras()
        {
            string[] players = { "A", "B", "C", "D" };

            AssertGenerationFails(players, new[] { "A", " A " }, new[] { 2, 2 });
            AssertGenerationFails(players, new[] { "Missing" }, new[] { 2, 2 });
        }

        [Test]
        public void GenerateTeams_SpreadsMoreExtrasThanTeamsBeforeRepeating()
        {
            string[] players = { "A", "B", "C", "D" };
            string[] extras = { "A", "B", "C" };

            bool success = TryGenerateTeams(
                players,
                extras,
                new[] { 2, 2 },
                new System.Random(17),
                out List<List<string>> teams,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(teams.Select(team => team.Count), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(
                teams.Select(team => team.Count(extras.Contains)).OrderBy(count => count),
                Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void GenerateTeams_RejectsRequestedSizeTotalMismatch()
        {
            AssertGenerationFails(
                new[] { "A", "B", "C", "D" },
                Array.Empty<string>(),
                new[] { 3, 2 });
        }

        private static bool TryBuildTeamSizes(
            int playerCount,
            int teamCount,
            int presentationCapacity,
            bool useDetailedSizes,
            string detailedSizesText,
            out int[] sizes,
            out string error)
        {
            MethodInfo method = GetMethod("TryBuildTeamSizes", 7);
            object[] arguments =
            {
                playerCount,
                teamCount,
                presentationCapacity,
                useDetailedSizes,
                detailedSizesText,
                null,
                null
            };

            bool success = (bool)method.Invoke(null, arguments);
            sizes = (int[])arguments[5];
            error = (string)arguments[6];
            return success;
        }

        private static bool TryGenerateTeams(
            IReadOnlyList<string> players,
            IReadOnlyList<string> extras,
            IReadOnlyList<int> requestedSizes,
            System.Random random,
            out List<List<string>> teams,
            out string error)
        {
            MethodInfo method = GetMethod("TryGenerateTeams", 6);
            object[] arguments = { players, extras, requestedSizes, random, null, null };

            bool success = (bool)method.Invoke(null, arguments);
            teams = (List<List<string>>)arguments[4];
            error = (string)arguments[5];
            return success;
        }

        private static MethodInfo GetMethod(string name, int parameterCount)
        {
            MethodInfo method = AllocationType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(candidate =>
                    candidate.Name == name && candidate.GetParameters().Length == parameterCount);

            Assert.That(method, Is.Not.Null, $"TeamAllocation.{name} public API is missing.");
            return method;
        }

        private static void AssertGenerationFails(
            IReadOnlyList<string> players,
            IReadOnlyList<string> extras,
            IReadOnlyList<int> requestedSizes)
        {
            bool success = TryGenerateTeams(
                players,
                extras,
                requestedSizes,
                new System.Random(5),
                out List<List<string>> teams,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(teams, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        }

        private static string Serialize(IEnumerable<IEnumerable<string>> teams)
        {
            return string.Join("|", teams.Select(team => string.Join(",", team)));
        }
    }
}
