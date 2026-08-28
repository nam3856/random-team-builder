using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RandomGame.Tests.PlayMode
{
    public sealed class TeamRandomizerFlowPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const float AcceleratedTimeScale = 100f;
        private const float RealtimeTimeoutSeconds = 25f;
        private const string CsvHeader = "Team,Member1,Member2,Member3,Member4";

        private float originalTimeScale;
        private bool capturedOriginalTimeScale;
        private string resultCsvPath;
        private Scene loadedScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            capturedOriginalTimeScale = true;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty,
                "Could not resolve the project root from Application.dataPath.");

            string testOutputFolder = Path.Combine(projectRoot, "Temp", "RandomGameFlowTests");
            Directory.CreateDirectory(testOutputFolder);
            resultCsvPath = Path.Combine(
                testOutputFolder,
                TestContext.CurrentContext.Test.MethodName + "-" + Guid.NewGuid().ToString("N") + ".csv");

            Time.timeScale = AcceleratedTimeScale;

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null, "Could not start loading " + ScenePath + ".");
            while (!loadOperation.isDone)
                yield return null;

            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.IsValid() && loadedScene.isLoaded, Is.True,
                "The production SampleScene must load for the flow test.");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(ScenePath));

            Component teamRandomizer = FindSingleSceneComponent("TeamRandomizer");
            SetPrivateField(teamRandomizer, "resultCsvPathOverride", resultCsvPath);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (capturedOriginalTimeScale)
                Time.timeScale = originalTimeScale;

            if (!string.IsNullOrEmpty(resultCsvPath) && File.Exists(resultCsvPath))
                File.Delete(resultCsvPath);

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                Scene cleanupScene = SceneManager.CreateScene("TeamRandomizerFlowPlayModeTests Cleanup");
                SceneManager.SetActiveScene(cleanupScene);
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(loadedScene);
                if (unloadOperation != null)
                {
                    while (!unloadOperation.isDone)
                        yield return null;
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ShuffleTeams_CompletesFiveTeamRevealAndWritesCsv()
        {
            Component teamRandomizer = FindSingleSceneComponent("TeamRandomizer");
            MethodInfo shuffleTeams = GetShuffleTeamsMethod(teamRandomizer);

            GameObject completedText = GetRequiredField<GameObject>(teamRandomizer, "CompletedText");
            ParticleSystem[] teamEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamEffects");
            ParticleSystem[] memberEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamMemberEffects");

            Assert.That(teamEffects, Has.Length.EqualTo(6));
            Assert.That(memberEffects, Has.Length.EqualTo(6));
            Assert.That(teamEffects, Has.All.Not.Null);
            Assert.That(memberEffects, Has.All.Not.Null);
            Assert.That(completedText.activeSelf, Is.False,
                "The completion message must start hidden so the test observes the real reveal flow.");

            shuffleTeams.Invoke(teamRandomizer, null);

            float deadline = Time.realtimeSinceStartup + RealtimeTimeoutSeconds;
            while (!completedText.activeSelf && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(completedText.activeSelf, Is.True,
                "The five-team reveal did not reach its completion state within " +
                RealtimeTimeoutSeconds + " realtime seconds at timeScale=" + AcceleratedTimeScale + ".");

            Assert.That(teamEffects.Take(5).All(effect => effect != null && effect.gameObject.activeSelf), Is.True,
                "The first five team completion effects must be activated by the default reveal flow.");
            Assert.That(memberEffects.Take(5).All(effect => effect != null && effect.gameObject.activeSelf), Is.True,
                "The first five member reveal effects must be activated by the default reveal flow.");
            Assert.That(teamEffects[5].gameObject.activeSelf, Is.False,
                "The available sixth team completion effect must remain inactive in the default five-team flow.");
            Assert.That(memberEffects[5].gameObject.activeSelf, Is.False,
                "The available sixth member effect must remain inactive in the default five-team flow.");

            Assert.That(File.Exists(resultCsvPath), Is.True,
                "ShuffleTeams() must write Result.csv at the project root.");
            string[] lines = File.ReadAllLines(resultCsvPath);
            Assert.That(lines, Has.Length.EqualTo(6),
                "Result.csv must contain one header and exactly five team rows.");
            Assert.That(lines[0], Is.EqualTo(CsvHeader));

            for (int teamIndex = 0; teamIndex < 5; teamIndex++)
            {
                string[] columns = lines[teamIndex + 1].Split(',');
                Assert.That(columns, Has.Length.EqualTo(5),
                    "Each CSV team row must contain Team plus four member columns.");
                Assert.That(columns[0], Is.EqualTo("Team" + (teamIndex + 1)));
                Assert.That(columns.Skip(1).Count(value => !string.IsNullOrWhiteSpace(value)),
                    Is.EqualTo(teamIndex < 3 ? 4 : 3),
                    "The CSV must preserve the expected 4/4/4/3/3 team distribution.");
            }
        }

        [UnityTest]
        public IEnumerator ShuffleTeams_TwoTeamAutomaticMode_ExpandsCsvAndUsesTwoPanels()
        {
            Component teamRandomizer = FindSingleSceneComponent("TeamRandomizer");
            MethodInfo shuffleTeams = GetShuffleTeamsMethod(teamRandomizer);
            Component teamCountInput = GetRequiredField<Component>(teamRandomizer, "teamCountInputField");
            GameObject completedText = GetRequiredField<GameObject>(teamRandomizer, "CompletedText");
            ParticleSystem[] teamEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamEffects");
            ParticleSystem[] memberEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamMemberEffects");
            Array teamTexts = GetRequiredField<Array>(teamRandomizer, "teamTexts");

            InvokeRequiredMethod(teamCountInput, "SetTextWithoutNotify", "2");
            InvokeRequiredEvent(teamCountInput, "onEndEdit", "2");
            shuffleTeams.Invoke(teamRandomizer, null);

            float deadline = Time.realtimeSinceStartup + RealtimeTimeoutSeconds;
            while (!completedText.activeSelf && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(completedText.activeSelf, Is.True,
                "The two-team automatic reveal did not complete within the realtime timeout.");
            Assert.That(teamEffects.Take(2).All(item => item != null && item.gameObject.activeSelf), Is.True);
            Assert.That(memberEffects.Take(2).All(item => item != null && item.gameObject.activeSelf), Is.True);
            Assert.That(teamEffects.Skip(2).All(item => item != null && !item.gameObject.activeSelf), Is.True);
            Assert.That(memberEffects.Skip(2).All(item => item != null && !item.gameObject.activeSelf), Is.True);

            Assert.That(File.Exists(resultCsvPath), Is.True);
            string[] lines = File.ReadAllLines(resultCsvPath);
            Assert.That(lines, Has.Length.EqualTo(3));
            string expectedHeader = "Team," + string.Join(",",
                Enumerable.Range(1, 9).Select(index => "Member" + index));
            Assert.That(lines[0], Is.EqualTo(expectedHeader));

            for (int teamIndex = 0; teamIndex < 2; teamIndex++)
            {
                string[] columns = lines[teamIndex + 1].Split(',');
                Assert.That(columns, Has.Length.EqualTo(10));
                Assert.That(columns.Skip(1).Count(value => !string.IsNullOrWhiteSpace(value)), Is.EqualTo(9));

                Component teamText = (Component)teamTexts.GetValue(teamIndex);
                string visibleNames = GetRequiredProperty<string>(teamText, "text");
                Assert.That(
                    visibleNames.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries),
                    Has.Length.EqualTo(9));
            }

            for (int index = 0; index < teamTexts.Length; index++)
            {
                Component teamText = (Component)teamTexts.GetValue(index);
                Assert.That(teamText.transform.parent.gameObject.activeSelf, Is.EqualTo(index < 2));
            }
        }

        [UnityTest]
        public IEnumerator ShuffleTeams_SixTeamDetailedMode_UsesRequestedDistribution()
        {
            Component teamRandomizer = FindSingleSceneComponent("TeamRandomizer");
            MethodInfo shuffleTeams = GetShuffleTeamsMethod(teamRandomizer);
            Component teamCountInput = GetRequiredField<Component>(teamRandomizer, "teamCountInputField");
            Component detailedSizesToggle = GetRequiredField<Component>(teamRandomizer, "detailedTeamSizesToggle");
            Component detailedSizesInput = GetRequiredField<Component>(teamRandomizer, "detailedTeamSizesInputField");
            GameObject completedText = GetRequiredField<GameObject>(teamRandomizer, "CompletedText");
            ParticleSystem[] teamEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamEffects");
            ParticleSystem[] memberEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamMemberEffects");
            int[] requestedSizes = { 2, 3, 4, 2, 3, 4 };

            InvokeRequiredMethod(teamCountInput, "SetTextWithoutNotify", "6");
            InvokeRequiredMethod(detailedSizesToggle, "SetIsOnWithoutNotify", true);
            InvokeRequiredMethod(detailedSizesInput, "SetTextWithoutNotify", "2,3,4,2,3,4");

            shuffleTeams.Invoke(teamRandomizer, null);

            float deadline = Time.realtimeSinceStartup + RealtimeTimeoutSeconds;
            while (!completedText.activeSelf && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(completedText.activeSelf, Is.True,
                "The six-team detailed reveal did not complete within the realtime timeout.");
            Assert.That(teamEffects.All(item => item != null && item.gameObject.activeSelf), Is.True,
                "Every completion effect must be used by a six-team reveal.");
            Assert.That(memberEffects.All(item => item != null && item.gameObject.activeSelf), Is.True,
                "Every member effect must be used by a six-team reveal.");

            Assert.That(File.Exists(resultCsvPath), Is.True);
            string[] lines = File.ReadAllLines(resultCsvPath);
            Assert.That(lines, Has.Length.EqualTo(7));
            Assert.That(lines[0], Is.EqualTo(CsvHeader));

            for (int teamIndex = 0; teamIndex < requestedSizes.Length; teamIndex++)
            {
                string[] columns = lines[teamIndex + 1].Split(',');
                Assert.That(columns, Has.Length.EqualTo(5));
                Assert.That(columns[0], Is.EqualTo("Team" + (teamIndex + 1)));
                Assert.That(columns.Skip(1).Count(value => !string.IsNullOrWhiteSpace(value)),
                    Is.EqualTo(requestedSizes[teamIndex]));
            }
        }

        [UnityTest]
        public IEnumerator ShuffleTeams_DetailedTotalMismatch_RestoresSettingsWithoutStartingReveal()
        {
            Component teamRandomizer = FindSingleSceneComponent("TeamRandomizer");
            MethodInfo shuffleTeams = GetShuffleTeamsMethod(teamRandomizer);

            Component teamCountInput = GetRequiredField<Component>(teamRandomizer, "teamCountInputField");
            Component detailedSizesToggle = GetRequiredField<Component>(teamRandomizer, "detailedTeamSizesToggle");
            Component detailedSizesInput = GetRequiredField<Component>(teamRandomizer, "detailedTeamSizesInputField");
            GameObject settingCanvas = GetRequiredField<GameObject>(teamRandomizer, "SettingCanvas");
            Component shuffleButton = GetRequiredField<Component>(teamRandomizer, "shuffleButton");
            GameObject completedText = GetRequiredField<GameObject>(teamRandomizer, "CompletedText");
            Component errorText = GetRequiredField<Component>(teamRandomizer, "ErrorText");
            ParticleSystem[] teamEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamEffects");
            ParticleSystem[] memberEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamMemberEffects");

            InvokeRequiredMethod(teamCountInput, "SetTextWithoutNotify", "5");
            InvokeRequiredMethod(detailedSizesToggle, "SetIsOnWithoutNotify", true);
            InvokeRequiredMethod(detailedSizesInput, "SetTextWithoutNotify", "4,4,4,3,2");

            shuffleTeams.Invoke(teamRandomizer, null);
            yield return null;

            Assert.That(settingCanvas.activeSelf, Is.True,
                "Invalid detailed sizes must restore the settings UI immediately.");
            Assert.That(GetRequiredProperty<bool>(shuffleButton, "interactable"), Is.True,
                "Invalid detailed sizes must re-enable the shuffle button.");
            Assert.That(completedText.activeSelf, Is.False,
                "An invalid configuration must not enter the completion state.");
            string errorMessage = GetRequiredProperty<string>(errorText, "text");
            Assert.That(errorMessage, Is.Not.Null.And.Not.Empty);
            StringAssert.Contains("17", errorMessage);
            StringAssert.Contains("18", errorMessage);
            Assert.That(File.Exists(resultCsvPath), Is.False,
                "An invalid configuration must not write Result.csv.");
            Assert.That(teamEffects.All(effect => effect != null && !effect.gameObject.activeSelf), Is.True,
                "An invalid configuration must not activate completion effects.");
            Assert.That(memberEffects.All(effect => effect != null && !effect.gameObject.activeSelf), Is.True,
                "An invalid configuration must not activate member effects.");
        }

        [UnityTest]
        public IEnumerator TeamConfigurationEvents_PreserveDetailsUntilTeamCountActuallyChanges()
        {
            Component teamRandomizer = FindSingleSceneComponent("TeamRandomizer");
            Component teamCountInput = GetRequiredField<Component>(teamRandomizer, "teamCountInputField");
            Component detailedSizesToggle = GetRequiredField<Component>(teamRandomizer, "detailedTeamSizesToggle");
            Component detailedSizesInput = GetRequiredField<Component>(teamRandomizer, "detailedTeamSizesInputField");
            Component teamGridLayout = GetRequiredField<Component>(teamRandomizer, "teamGridLayout");
            Array teamTexts = GetRequiredField<Array>(teamRandomizer, "teamTexts");
            const string manualSizes = "2,5,4,3,4";

            Assert.That(detailedSizesInput.gameObject.activeSelf, Is.False);
            SetRequiredProperty(detailedSizesToggle, "isOn", true);
            yield return null;
            Assert.That(detailedSizesInput.gameObject.activeSelf, Is.True,
                "The real Toggle.onValueChanged path must reveal the detailed sizes input.");

            InvokeRequiredMethod(detailedSizesInput, "SetTextWithoutNotify", manualSizes);
            InvokeRequiredEvent(teamCountInput, "onEndEdit", "5");
            yield return null;
            Assert.That(GetRequiredProperty<string>(detailedSizesInput, "text"), Is.EqualTo(manualSizes),
                "Ending the unchanged team-count field must preserve the user's detailed sizes.");

            InvokeRequiredMethod(teamCountInput, "SetTextWithoutNotify", "4");
            InvokeRequiredEvent(teamCountInput, "onEndEdit", "4");
            yield return null;

            Assert.That(GetRequiredProperty<string>(detailedSizesInput, "text"), Is.EqualTo("5,5,4,4"),
                "Changing the team count must seed a valid balanced detailed configuration.");
            Assert.That(GetRequiredProperty<int>(teamGridLayout, "constraintCount"), Is.EqualTo(2));

            for (int index = 0; index < teamTexts.Length; index++)
            {
                Component teamText = (Component)teamTexts.GetValue(index);
                Assert.That(teamText.transform.parent.gameObject.activeSelf, Is.EqualTo(index < 4),
                    "The team panel active state must follow the real team-count end-edit event.");
            }
        }

        [UnityTest]
        public IEnumerator ResponsiveLayout_AdaptsTwoThroughSixTeamsAcrossAspectProfiles()
        {
            Component teamRandomizer = FindSingleSceneComponent("TeamRandomizer");
            Component teamCountInput = GetRequiredField<Component>(teamRandomizer, "teamCountInputField");
            Component teamGridLayout = GetRequiredField<Component>(teamRandomizer, "teamGridLayout");
            GameObject gridSpacer = GetRequiredField<GameObject>(teamRandomizer, "teamGridSpacer");
            GameObject completedText = GetRequiredField<GameObject>(teamRandomizer, "CompletedText");
            Array teamTexts = GetRequiredField<Array>(teamRandomizer, "teamTexts");
            ParticleSystem[] memberEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamMemberEffects");
            ParticleSystem[] completionEffects = GetRequiredField<ParticleSystem[]>(teamRandomizer, "teamEffects");
            MethodInfo applyViewport = teamRandomizer.GetType().GetMethod(
                "ApplyResponsiveLayoutForViewport",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyViewport, Is.Not.Null);

            string[] profileNames = { "16:9", "4:3", "21:9", "9:16", "9:16-safe" };
            int[] widths = { 1920, 1600, 2560, 1080, 1080 };
            int[] heights = { 1080, 1200, 1080, 1920, 1920 };
            Rect[] safeAreas =
            {
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(0f, 0f, 1600f, 1200f),
                new Rect(0f, 0f, 2560f, 1080f),
                new Rect(0f, 0f, 1080f, 1920f),
                new Rect(0f, 80f, 1080f, 1760f)
            };

            Type layoutType = Type.GetType("ResponsiveUiLayout, Assembly-CSharp", true);
            MethodInfo getSafeWorldRect = layoutType.GetMethod("GetSafeWorldRect", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getTeamWorldRect = layoutType.GetMethod("GetTeamWorldRect", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getCompletionWorldRect = layoutType.GetMethod("GetCompletionWorldRect", BindingFlags.Public | BindingFlags.Static);

            for (int profileIndex = 0; profileIndex < profileNames.Length; profileIndex++)
            {
                float aspect = widths[profileIndex] / (float)heights[profileIndex];
                Rect safeWorld = (Rect)getSafeWorldRect.Invoke(null, new object[]
                {
                    widths[profileIndex], heights[profileIndex], safeAreas[profileIndex], Vector2.zero, 5f
                });
                Rect teamWorld = (Rect)getTeamWorldRect.Invoke(null, new object[] { safeWorld });
                Rect completionWorld = (Rect)getCompletionWorldRect.Invoke(null, new object[] { safeWorld });

                for (int teamCount = 2; teamCount <= 6; teamCount++)
                {
                    string countText = teamCount.ToString();
                    InvokeRequiredMethod(teamCountInput, "SetTextWithoutNotify", countText);
                    InvokeRequiredEvent(teamCountInput, "onEndEdit", countText);
                    applyViewport.Invoke(teamRandomizer, new object[]
                    {
                        widths[profileIndex], heights[profileIndex], safeAreas[profileIndex]
                    });

                    int expectedColumns = aspect < 1.15f
                        ? Math.Min(2, teamCount)
                        : teamCount == 2 || teamCount == 4 ? 2 : 3;
                    Assert.That(GetRequiredProperty<int>(teamGridLayout, "constraintCount"),
                        Is.EqualTo(expectedColumns),
                        profileNames[profileIndex] + " / " + teamCount + " teams must select responsive columns.");
                    Assert.That(gridSpacer.activeSelf,
                        Is.EqualTo(teamCount == 5 && expectedColumns == 3),
                        profileNames[profileIndex] + " must only use the legacy spacer for a three-column five-team row.");

                    float appliedScale = (float)teamRandomizer.GetType()
                        .GetProperty("ResponsiveTeamScale", BindingFlags.Instance | BindingFlags.Public)
                        .GetValue(teamRandomizer);
                    Assert.That(appliedScale, Is.GreaterThan(0.05f).And.LessThanOrEqualTo(1f));

                    for (int teamIndex = 0; teamIndex < teamTexts.Length; teamIndex++)
                    {
                        Component teamText = (Component)teamTexts.GetValue(teamIndex);
                        Assert.That(teamText.transform.parent.gameObject.activeSelf, Is.EqualTo(teamIndex < teamCount));
                        if (teamIndex >= teamCount)
                            continue;

                        AssertRectInside(teamText.transform.parent as RectTransform, teamWorld, 0.05f,
                            profileNames[profileIndex] + " team panel " + (teamIndex + 1));
                        AssertRectInside(teamText.transform as RectTransform, teamWorld, 0.05f,
                            profileNames[profileIndex] + " team text " + (teamIndex + 1));

                        Vector3 expectedEffectAnchor = teamText.transform.position +
                                                       teamText.transform.TransformVector(Vector3.down * 45f);
                        expectedEffectAnchor.z = 0f;
                        Assert.That(Vector3.Distance(memberEffects[teamIndex].transform.position, expectedEffectAnchor),
                            Is.LessThanOrEqualTo(0.01f),
                            profileNames[profileIndex] + " member effect " + (teamIndex + 1) + " drifted from its team.");
                        Assert.That(Vector3.Distance(completionEffects[teamIndex].transform.position, expectedEffectAnchor),
                            Is.LessThanOrEqualTo(0.01f),
                            profileNames[profileIndex] + " completion effect " + (teamIndex + 1) + " drifted from its team.");
                    }

                    AssertRectInside(completedText.transform as RectTransform, completionWorld, 0.05f,
                        profileNames[profileIndex] + " completion banner");
                }
            }

            yield return null;
        }

        private static void AssertRectInside(RectTransform rect, Rect available, float tolerance, string label)
        {
            Assert.That(rect, Is.Not.Null, label + " has no RectTransform.");
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = corners.Min(corner => corner.x);
            float xMax = corners.Max(corner => corner.x);
            float yMin = corners.Min(corner => corner.y);
            float yMax = corners.Max(corner => corner.y);
            Assert.That(xMin, Is.GreaterThanOrEqualTo(available.xMin - tolerance), label + " exceeds the left edge.");
            Assert.That(xMax, Is.LessThanOrEqualTo(available.xMax + tolerance), label + " exceeds the right edge.");
            Assert.That(yMin, Is.GreaterThanOrEqualTo(available.yMin - tolerance), label + " exceeds the bottom edge.");
            Assert.That(yMax, Is.LessThanOrEqualTo(available.yMax + tolerance), label + " exceeds the top edge.");
        }

        private static MethodInfo GetShuffleTeamsMethod(Component teamRandomizer)
        {
            MethodInfo shuffleTeams = teamRandomizer.GetType().GetMethod(
                "ShuffleTeams",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(shuffleTeams, Is.Not.Null,
                "TeamRandomizer must keep its public parameterless ShuffleTeams() entry point.");
            return shuffleTeams;
        }

        private static void InvokeRequiredMethod(Component component, string methodName, object argument)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { argument.GetType() },
                null);
            Assert.That(method, Is.Not.Null,
                component.GetType().Name + " must expose " + methodName + "().");
            method.Invoke(component, new[] { argument });
        }

        private static T GetRequiredProperty<T>(Component component, string propertyName)
        {
            PropertyInfo property = component.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                component.GetType().Name + " must expose property " + propertyName + ".");

            object value = property.GetValue(component);
            Assert.That(value, Is.InstanceOf<T>(),
                component.GetType().Name + "." + propertyName + " must be a " + typeof(T).Name + ".");
            return (T)value;
        }

        private static void SetRequiredProperty<T>(Component component, string propertyName, T value)
        {
            PropertyInfo property = component.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null.And.Property("CanWrite").True,
                component.GetType().Name + " must expose writable property " + propertyName + ".");
            property.SetValue(component, value);
        }

        private static void InvokeRequiredEvent(Component component, string propertyName, string argument)
        {
            PropertyInfo property = component.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                component.GetType().Name + " must expose event property " + propertyName + ".");

            object eventValue = property.GetValue(component);
            Assert.That(eventValue, Is.Not.Null);
            MethodInfo invoke = eventValue.GetType().GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(invoke, Is.Not.Null,
                component.GetType().Name + "." + propertyName + " must accept a string argument.");
            invoke.Invoke(eventValue, new object[] { argument });
        }

        private Component FindSingleSceneComponent(string typeName)
        {
            Component[] matches = loadedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SelectMany(transform => transform.GetComponents<Component>())
                .Where(component => component != null && component.GetType().Name == typeName)
                .ToArray();

            Assert.That(matches, Has.Length.EqualTo(1),
                "Expected exactly one " + typeName + " in " + ScenePath + ".");
            return matches[0];
        }

        private static T GetRequiredField<T>(Component component, string fieldName) where T : class
        {
            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                component.GetType().Name + " must keep serialized field " + fieldName + ".");

            object value = field.GetValue(component);
            Assert.That(value, Is.InstanceOf<T>(),
                component.GetType().Name + "." + fieldName + " must be a " + typeof(T).Name + ".");
            return (T)value;
        }

        private static void SetPrivateField(Component component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                component.GetType().Name + " must expose the test output field " + fieldName + ".");
            field.SetValue(component, value);
        }
    }
}
