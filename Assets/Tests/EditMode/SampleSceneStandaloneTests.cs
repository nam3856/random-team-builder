using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RandomGame.Tests.EditMode
{
    public sealed class SampleSceneStandaloneTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private static readonly Color[] TeamColors =
        {
            new Color(0.18f, 0.62f, 1f, 1f),
            new Color(0.20f, 1f, 0.42f, 1f),
            new Color(0.74f, 0.30f, 1f, 1f),
            new Color(1f, 0.22f, 0.20f, 1f),
            new Color(1f, 0.78f, 0.15f, 1f),
            new Color(0.334905684f, 1f, 0.993205845f, 1f)
        };
        private Scene scene;
        private bool openedByTest;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null,
                "The production scene must remain at " + ScenePath + ".");

            scene = SceneManager.GetSceneByPath(ScenePath);
            openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            if (openedByTest && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Scene_HasNoMissingScriptsPrefabsOrPaidPackageSerialization()
        {
            List<GameObject> gameObjects = GetAllGameObjects();
            int missingScriptCount = gameObjects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            string[] missingPrefabObjects = gameObjects
                .Where(gameObject => PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.MissingAsset)
                .Select(GetHierarchyPath)
                .ToArray();

            Assert.That(missingScriptCount, Is.Zero, "SampleScene contains missing MonoBehaviour scripts.");
            Assert.That(missingPrefabObjects, Is.Empty,
                "SampleScene contains missing prefab instances: " + string.Join(", ", missingPrefabObjects));

            string serializedScene = File.ReadAllText(Path.GetFullPath(ScenePath));
            StringAssert.DoesNotContain("MoreMountains.", serializedScene);
            StringAssert.DoesNotContain("Retro Arsenal", serializedScene);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Feel"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Retro Arsenal"), Is.False);
        }

        [Test]
        public void Scene_BindsExactlySixStandaloneEffectsAndMatchingTeamTargets()
        {
            Component teamRandomizer = FindSingleComponent("TeamRandomizer");
            Component randomMoveLight = FindSingleComponent("RandomMoveLight");
            SerializedObject serializedRandomizer = new SerializedObject(teamRandomizer);
            SerializedObject serializedLight = new SerializedObject(randomMoveLight);

            SerializedProperty teamEffects = AssertObjectArray(serializedRandomizer, "teamEffects", 6);
            SerializedProperty memberEffects = AssertObjectArray(serializedRandomizer, "teamMemberEffects", 6);
            SerializedProperty teamTargets = AssertObjectArray(serializedLight, "teamTargets", 6);
            SerializedProperty teamTexts = AssertObjectArray(serializedRandomizer, "teamTexts", 6);
            AssertObjectArray(serializedRandomizer, "TeamTitles", 6);

            AssertEffectBindings(
                memberEffects,
                "Member Effect Team ",
                "Member Effects",
                "Assets/StandaloneEffects/Prefabs/MemberEffect_Team",
                2);
            AssertEffectBindings(
                teamEffects,
                "Team Completion Effect Team ",
                "Team Completion Effects",
                "Assets/StandaloneEffects/Prefabs/TeamCompletionEffect_Team",
                3);

            HashSet<int> uniqueTargets = new HashSet<int>();
            for (int index = 0; index < 6; index++)
            {
                ParticleSystem completionEffect =
                    teamEffects.GetArrayElementAtIndex(index).objectReferenceValue as ParticleSystem;
                ParticleSystem memberEffect =
                    memberEffects.GetArrayElementAtIndex(index).objectReferenceValue as ParticleSystem;
                Component teamText = teamTexts.GetArrayElementAtIndex(index).objectReferenceValue as Component;
                Transform teamTarget = teamTargets.GetArrayElementAtIndex(index).objectReferenceValue as Transform;

                AssertEffectLayout(index, memberEffect, completionEffect);
                Assert.That(teamText, Is.Not.Null, "teamTexts[" + index + "] must be assigned.");
                Assert.That(teamTarget, Is.Not.Null, "teamTargets[" + index + "] must be assigned.");
                Assert.That(teamTarget, Is.SameAs(teamText.transform),
                    "Each light target must match the corresponding team text transform.");
                Assert.That(uniqueTargets.Add(teamTarget.GetInstanceID()), Is.True,
                    "Team light targets must be distinct.");
            }
        }

        [Test]
        public void Scene_BindsFlexibleTeamControlsWithExpectedDefaults()
        {
            Component teamRandomizer = FindSingleComponent("TeamRandomizer");
            SerializedObject serializedRandomizer = new SerializedObject(teamRandomizer);

            Component teamCountInput =
                FindProperty(serializedRandomizer, "teamCountInputField").objectReferenceValue as Component;
            Component detailedSizesToggle =
                FindProperty(serializedRandomizer, "detailedTeamSizesToggle").objectReferenceValue as Component;
            Component detailedSizesInput =
                FindProperty(serializedRandomizer, "detailedTeamSizesInputField").objectReferenceValue as Component;
            Component teamGridLayout =
                FindProperty(serializedRandomizer, "teamGridLayout").objectReferenceValue as Component;
            GameObject teamGridSpacer =
                FindProperty(serializedRandomizer, "teamGridSpacer").objectReferenceValue as GameObject;

            Assert.That(teamCountInput, Is.Not.Null, "The team-count input must be assigned.");
            Assert.That(detailedSizesToggle, Is.Not.Null, "The detailed team-size toggle must be assigned.");
            Assert.That(detailedSizesInput, Is.Not.Null, "The detailed team-size input must be assigned.");
            Assert.That(teamGridLayout, Is.Not.Null, "The team presentation grid must be assigned.");
            Assert.That(teamGridSpacer, Is.Not.Null, "The five-team layout spacer must be assigned.");

            Assert.That(teamCountInput.GetType().Name, Is.EqualTo("TMP_InputField"));
            Assert.That(detailedSizesToggle.GetType().Name, Is.EqualTo("Toggle"));
            Assert.That(detailedSizesInput.GetType().Name, Is.EqualTo("TMP_InputField"));
            Assert.That(teamGridLayout.GetType().Name, Is.EqualTo("GridLayoutGroup"));

            Assert.That(FindProperty(new SerializedObject(teamCountInput), "m_Text").stringValue, Is.EqualTo("5"));
            Assert.That(FindProperty(new SerializedObject(detailedSizesToggle), "m_IsOn").boolValue, Is.False);
            Assert.That(FindProperty(new SerializedObject(detailedSizesInput), "m_Text").stringValue,
                Is.EqualTo("4,4,4,3,3"));
        }

        [Test]
        public void Scene_BindsResponsiveResultAndSettingsDependencies()
        {
            Component teamRandomizer = FindSingleComponent("TeamRandomizer");
            SerializedObject serializedRandomizer = new SerializedObject(teamRandomizer);
            GridLayoutGroup grid = FindProperty(serializedRandomizer, "teamGridLayout")
                .objectReferenceValue as GridLayoutGroup;
            GameObject settingsPanel = FindProperty(serializedRandomizer, "SettingCanvas")
                .objectReferenceValue as GameObject;
            GameObject completedText = FindProperty(serializedRandomizer, "CompletedText")
                .objectReferenceValue as GameObject;
            GameObject exitButton = FindProperty(serializedRandomizer, "ExitButton")
                .objectReferenceValue as GameObject;

            Assert.That(grid, Is.Not.Null);
            Assert.That(settingsPanel, Is.Not.Null);
            Assert.That(completedText, Is.Not.Null);
            Assert.That(exitButton, Is.Not.Null);

            Canvas resultCanvas = grid.GetComponentInParent<Canvas>();
            Canvas settingsCanvas = settingsPanel.GetComponentInParent<Canvas>(true);
            Assert.That(resultCanvas, Is.Not.Null);
            Assert.That(resultCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(resultCanvas.worldCamera, Is.Not.Null);
            Assert.That(resultCanvas.worldCamera.orthographic, Is.True);
            Assert.That(settingsCanvas, Is.Not.Null);
            Assert.That(settingsCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(settingsCanvas.GetComponent<CanvasScaler>(), Is.Not.Null);
            Assert.That(exitButton.GetComponentInParent<Canvas>(true), Is.SameAs(settingsCanvas));

            Assert.That(teamRandomizer.GetType().GetProperty("ResponsiveLayoutReady"), Is.Not.Null);
            Assert.That(teamRandomizer.GetType().GetProperty("ResponsiveColumnCount"), Is.Not.Null);
            Assert.That(teamRandomizer.GetType().GetProperty("ResponsiveTeamScale"), Is.Not.Null);
            Assert.That(teamRandomizer.GetType().GetMethod(
                "ApplyResponsiveLayoutForViewport",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        }

        [Test]
        public void ResponsiveCalculator_AdaptsColumnsAndPartitionsSafeArea()
        {
            Type layoutType = Type.GetType("ResponsiveUiLayout, Assembly-CSharp", true);
            MethodInfo getColumns = layoutType.GetMethod("GetColumnCount", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getRows = layoutType.GetMethod("GetRowCount", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getSafeWorldRect = layoutType.GetMethod("GetSafeWorldRect", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getTeamWorldRect = layoutType.GetMethod("GetTeamWorldRect", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getCompletionWorldRect = layoutType.GetMethod("GetCompletionWorldRect", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getSettingsScale = layoutType.GetMethod("GetSettingsPanelScale", BindingFlags.Public | BindingFlags.Static);

            Assert.That(getColumns, Is.Not.Null);
            Assert.That(getRows, Is.Not.Null);
            Assert.That(getSafeWorldRect, Is.Not.Null);
            Assert.That(getTeamWorldRect, Is.Not.Null);
            Assert.That(getCompletionWorldRect, Is.Not.Null);
            Assert.That(getSettingsScale, Is.Not.Null);

            Assert.That((int)getColumns.Invoke(null, new object[] { 6, 16f / 9f }), Is.EqualTo(3));
            Assert.That((int)getColumns.Invoke(null, new object[] { 4, 4f / 3f }), Is.EqualTo(2));
            Assert.That((int)getColumns.Invoke(null, new object[] { 6, 9f / 16f }), Is.EqualTo(2));
            Assert.That((int)getColumns.Invoke(null, new object[] { 3, 1f }), Is.EqualTo(2));
            Assert.That((int)getRows.Invoke(null, new object[] { 5, 2 }), Is.EqualTo(3));

            Rect safeWorld = (Rect)getSafeWorldRect.Invoke(null, new object[]
            {
                1080,
                1920,
                new Rect(0f, 80f, 1080f, 1760f),
                Vector2.zero,
                5f
            });
            Rect teamWorld = (Rect)getTeamWorldRect.Invoke(null, new object[] { safeWorld });
            Rect completionWorld = (Rect)getCompletionWorldRect.Invoke(null, new object[] { safeWorld });

            Assert.That(teamWorld.xMin, Is.GreaterThanOrEqualTo(safeWorld.xMin));
            Assert.That(teamWorld.xMax, Is.LessThanOrEqualTo(safeWorld.xMax));
            Assert.That(teamWorld.yMax, Is.LessThanOrEqualTo(safeWorld.yMax));
            Assert.That(completionWorld.xMin, Is.GreaterThanOrEqualTo(safeWorld.xMin));
            Assert.That(completionWorld.xMax, Is.LessThanOrEqualTo(safeWorld.xMax));
            Assert.That(completionWorld.yMin, Is.GreaterThanOrEqualTo(safeWorld.yMin));
            Assert.That(completionWorld.yMax, Is.LessThanOrEqualTo(teamWorld.yMin));

            float landscapeScale = (float)getSettingsScale.Invoke(null, new object[]
            {
                new Vector2(1920f, 1080f),
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(1000f, 620f)
            });
            float portraitScale = (float)getSettingsScale.Invoke(null, new object[]
            {
                new Vector2(1080f, 1920f),
                new Rect(0f, 80f / 1920f, 1f, 1760f / 1920f),
                new Vector2(1000f, 620f)
            });
            Assert.That(landscapeScale, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(portraitScale, Is.LessThan(landscapeScale).And.GreaterThan(0.9f));
        }

        private static void AssertEffectLayout(
            int index,
            ParticleSystem memberEffect,
            ParticleSystem completionEffect)
        {
            Assert.That(memberEffect, Is.Not.Null);
            Assert.That(completionEffect, Is.Not.Null);

            Vector3 memberPosition = memberEffect.transform.position;
            Vector3 completionPosition = completionEffect.transform.position;
            Assert.That(Vector2.Distance(memberPosition, completionPosition), Is.LessThanOrEqualTo(0.02f),
                "Team " + (index + 1) + " completion effect must align with the member effect in XY.");
            Assert.That(completionPosition.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(completionEffect.transform.localScale, Is.EqualTo(Vector3.one),
                "Completion effects must retain visible unit scale after leaving the Canvas hierarchy.");

            ParticleSystemRenderer[] memberRenderers =
                memberEffect.GetComponentsInChildren<ParticleSystemRenderer>(true);
            ParticleSystemRenderer[] completionRenderers =
                completionEffect.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Assert.That(memberRenderers, Is.Not.Empty);
            Assert.That(completionRenderers, Is.Not.Empty);

            int sortingLayerId = memberRenderers[0].sortingLayerID;
            int sortingOrder = memberRenderers[0].sortingOrder;
            Assert.That(memberRenderers.All(renderer =>
                renderer.sortingLayerID == sortingLayerId && renderer.sortingOrder == sortingOrder), Is.True);
            Assert.That(completionRenderers.All(renderer =>
                renderer.sortingLayerID == sortingLayerId && renderer.sortingOrder == sortingOrder), Is.True,
                "Team " + (index + 1) + " completion effect must preserve the member effect sorting settings.");
        }

        [Test]
        public void Scene_UsesRepositoryOwnedMemberAndCompletionAudio()
        {
            Component teamRandomizer = FindSingleComponent("TeamRandomizer");
            SerializedObject serializedRandomizer = new SerializedObject(teamRandomizer);
            AudioSource memberAudioSource = FindProperty(serializedRandomizer, "effect").objectReferenceValue as AudioSource;

            Assert.That(memberAudioSource, Is.Not.Null);
            Assert.That(memberAudioSource.clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(memberAudioSource.clip), Is.EqualTo("Assets/scorefill 1.wav"));

            Component soundManager = FindSingleComponent("SoundManager");
            SerializedProperty clips = FindProperty(new SerializedObject(soundManager), "popSfx");
            Assert.That(clips.arraySize, Is.EqualTo(2));
            Assert.That(AssetDatabase.GetAssetPath(clips.GetArrayElementAtIndex(0).objectReferenceValue),
                Is.EqualTo("Assets/scorefillend.wav"));
            Assert.That(AssetDatabase.GetAssetPath(clips.GetArrayElementAtIndex(1).objectReferenceValue),
                Is.EqualTo("Assets/scorefill 1.wav"));
        }

        private void AssertEffectBindings(
            SerializedProperty bindings,
            string instanceNamePrefix,
            string expectedParentName,
            string prefabPathPrefix,
            int minimumStageCount)
        {
            HashSet<int> uniqueBindings = new HashSet<int>();

            for (int index = 0; index < bindings.arraySize; index++)
            {
                ParticleSystem effect = bindings.GetArrayElementAtIndex(index).objectReferenceValue as ParticleSystem;
                Assert.That(effect, Is.Not.Null, bindings.propertyPath + "[" + index + "] must be assigned.");
                Assert.That(uniqueBindings.Add(effect.GetInstanceID()), Is.True,
                    bindings.propertyPath + " entries must be distinct.");
                Assert.That(effect.gameObject.name, Is.EqualTo(instanceNamePrefix + (index + 1)));
                Assert.That(effect.transform.parent, Is.Not.Null);
                Assert.That(effect.transform.parent.name, Is.EqualTo(expectedParentName));

                string expectedPrefabPath = prefabPathPrefix + (index + 1) + ".prefab";
                string actualPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(effect.gameObject);
                Assert.That(actualPrefabPath, Is.EqualTo(expectedPrefabPath));

                ParticleSystem[] stages = effect.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(stages.Length, Is.GreaterThanOrEqualTo(minimumStageCount),
                    effect.name + " does not contain all native effect stages.");
                Assert.That(stages.All(stage => !stage.main.loop), Is.True,
                    effect.name + " must remain a one-shot effect.");
                Assert.That(stages.All(stage => HasExpectedStartColor(stage, TeamColors[index])), Is.True,
                    effect.name + " must use the team " + (index + 1) + " palette color on every stage.");
            }
        }

        private static bool HasExpectedStartColor(ParticleSystem stage, Color expected)
        {
            ParticleSystem.MinMaxGradient startColor = stage.main.startColor;
            return startColor.mode == ParticleSystemGradientMode.Color &&
                   Mathf.Abs(startColor.color.r - expected.r) < 0.001f &&
                   Mathf.Abs(startColor.color.g - expected.g) < 0.001f &&
                   Mathf.Abs(startColor.color.b - expected.b) < 0.001f &&
                   Mathf.Abs(startColor.color.a - expected.a) < 0.001f;
        }

        private Component FindSingleComponent(string typeName)
        {
            Component[] matches = GetAllGameObjects()
                .SelectMany(gameObject => gameObject.GetComponents<Component>())
                .Where(component => component != null && component.GetType().Name == typeName)
                .ToArray();

            Assert.That(matches.Length, Is.EqualTo(1),
                "Expected exactly one " + typeName + " in " + ScenePath + ".");
            return matches[0];
        }

        private List<GameObject> GetAllGameObjects()
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToList();
        }

        private static SerializedProperty AssertObjectArray(
            SerializedObject serializedObject,
            string fieldName,
            int expectedCount)
        {
            SerializedProperty property = FindProperty(serializedObject, fieldName);
            Assert.That(property.isArray, Is.True, fieldName + " must be an array.");
            Assert.That(property.arraySize, Is.EqualTo(expectedCount));

            for (int index = 0; index < property.arraySize; index++)
            {
                Assert.That(property.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null,
                    fieldName + "[" + index + "] must be assigned.");
            }

            return property;
        }

        private static SerializedProperty FindProperty(SerializedObject serializedObject, string fieldName)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null,
                serializedObject.targetObject.GetType().Name + " must serialize " + fieldName + ".");
            return property;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            Stack<string> names = new Stack<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }
    }
}
