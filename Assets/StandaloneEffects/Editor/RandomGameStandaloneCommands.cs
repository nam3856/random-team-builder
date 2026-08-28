using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RandomGameStandaloneValidator
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string VendorRoot = "Assets/Retro Arsenal";
    private const string PrefabRoot = "Assets/StandaloneEffects/Prefabs";
    private const string MemberClipPath = "Assets/scorefill 1.wav";
    private const string CompletionClipPath = "Assets/scorefillend.wav";
    private const int PresentationCapacity = 6;
    private static readonly Color[] TeamColors =
    {
        new Color(0.18f, 0.62f, 1f, 1f),
        new Color(0.20f, 1f, 0.42f, 1f),
        new Color(0.74f, 0.30f, 1f, 1f),
        new Color(1f, 0.22f, 0.20f, 1f),
        new Color(1f, 0.78f, 0.15f, 1f),
        new Color(0.334905684f, 1f, 0.993205845f, 1f)
    };

    [CliCommand(
        "randomgame_validate_standalone",
        "Validate the saved RandomGame scene without requiring FEEL or Retro Arsenal",
        MainThreadRequired = true)]
    public static string ValidateStandalone()
    {
        ValidationReport report = new ValidationReport
        {
            scenePath = ScenePath,
            errors = new List<string>()
        };

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            report.errors.Add("Target scene is missing: " + ScenePath);
            report.FinalizeReport();
            return JsonUtility.ToJson(report, true);
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
        if (openedForValidation)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            PopulateReport(scene, report);
            report.FinalizeReport();
            return JsonUtility.ToJson(report, true);
        }
        catch (Exception exception)
        {
            report.errors.Add("Validator exception: " + exception.GetType().Name + ": " + exception.Message);
            report.FinalizeReport();
            return JsonUtility.ToJson(report, true);
        }
        finally
        {
            if (openedForValidation && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void PopulateReport(Scene scene, ValidationReport report)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            report.errors.Add("Target scene could not be loaded: " + ScenePath);
            return;
        }

        List<GameObject> gameObjects = GetSceneGameObjects(scene).ToList();
        report.missingScriptCount = gameObjects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
        report.missingPrefabCount = gameObjects.Count(IsMissingPrefabSafely);
        report.vendorFolderPresent = AssetDatabase.IsValidFolder(VendorRoot);
        report.vendorSceneObjectCount = gameObjects.Count(go => IsVendorPath(GetPrefabSourcePath(go)));
        report.moreMountainsComponentCount = gameObjects
            .SelectMany(go => go.GetComponents<MonoBehaviour>())
            .Count(component => component != null &&
                                component.GetType().Namespace != null &&
                                component.GetType().Namespace.StartsWith("MoreMountains", StringComparison.Ordinal));

        string[] sceneDependencies = AssetDatabase.GetDependencies(ScenePath, true);
        report.vendorDependencyCount = sceneDependencies.Count(IsVendorPath);
        report.serializedVendorMarkerCount = CountSerializedVendorMarkers();
        report.unresolvedGuids = FindUnresolvedSerializedGuids();
        report.legacyGuidCount = report.unresolvedGuids.Length;

        if (report.missingScriptCount != 0)
            report.errors.Add("Missing Script count is " + report.missingScriptCount + "; expected 0.");
        if (report.missingPrefabCount != 0)
            report.errors.Add("Missing Prefab count is " + report.missingPrefabCount + "; expected 0.");
        if (report.vendorFolderPresent)
            report.errors.Add("Paid package folder still exists: " + VendorRoot + ".");
        if (report.vendorSceneObjectCount != 0)
            report.errors.Add("Scene contains " + report.vendorSceneObjectCount + " paid-package prefab objects.");
        if (report.vendorDependencyCount != 0)
            report.errors.Add("Scene has " + report.vendorDependencyCount + " paid-package asset dependencies.");
        if (report.serializedVendorMarkerCount != 0)
            report.errors.Add("Saved scene contains " + report.serializedVendorMarkerCount + " Retro Arsenal path/name markers.");
        if (report.legacyGuidCount != 0)
            report.errors.Add("Saved scene contains " + report.legacyGuidCount + " unresolved serialized asset GUIDs.");
        if (report.moreMountainsComponentCount != 0)
            report.errors.Add("Scene contains " + report.moreMountainsComponentCount + " MoreMountains components.");

        TeamRandomizer randomizer = FindExactlyOne<TeamRandomizer>(scene, report.errors);
        RandomMoveLight randomMoveLight = FindExactlyOne<RandomMoveLight>(scene, report.errors);
        SoundManager soundManager = FindExactlyOne<SoundManager>(scene, report.errors);
        MusicPlayer musicPlayer = FindExactlyOne<MusicPlayer>(scene, report.errors);

        if (randomizer != null)
        {
            SerializedObject randomizerSo = new SerializedObject(randomizer);
            ValidateEffectBindings(randomizerSo, "teamEffects", false, report);
            ValidateEffectBindings(randomizerSo, "teamMemberEffects", true, report);
            ValidateEffectLayout(randomizerSo, report);
            ValidateFlexibleTeamConfiguration(randomizer, randomizerSo, report);
            ValidateResponsiveLayout(randomizer, randomizerSo, report);
            ValidateAudio(randomizerSo, soundManager, musicPlayer, report);
        }

        if (randomMoveLight != null)
            ValidateTeamTargets(randomMoveLight, randomizer, report);

        ValidateNativeComponents(scene, report);
        ValidateGeneratedPrefabs(report);
    }

    private static void ValidateEffectBindings(
        SerializedObject randomizerSo,
        string fieldName,
        bool member,
        ValidationReport report)
    {
        SerializedProperty property = randomizerSo.FindProperty(fieldName);
        int count = property != null && property.isArray ? property.arraySize : -1;
        if (member) report.teamMemberEffectsCount = count;
        else report.teamEffectsCount = count;

        if (count != PresentationCapacity)
        {
            report.errors.Add(fieldName + " length is " + count + "; expected " + PresentationCapacity + ".");
            return;
        }

        int nullCount = 0;
        for (int i = 0; i < count; i++)
        {
            ParticleSystem effect = property.GetArrayElementAtIndex(i).objectReferenceValue as ParticleSystem;
            if (effect == null)
            {
                nullCount++;
                continue;
            }

            string expectedName = member
                ? "Member Effect Team " + (i + 1)
                : "Team Completion Effect Team " + (i + 1);
            string expectedPath = member ? GetMemberPrefabPath(i) : GetTeamPrefabPath(i);
            int expectedStages = member ? 2 : 3;

            if (effect.gameObject.name != expectedName)
                report.errors.Add(fieldName + "[" + i + "] is named " + effect.gameObject.name + "; expected " + expectedName + ".");
            if (!string.Equals(GetPrefabSourcePath(effect.gameObject), expectedPath, StringComparison.OrdinalIgnoreCase))
                report.errors.Add(expectedName + " is not an instance of " + expectedPath + ".");

            ParticleSystem[] stages = effect.GetComponentsInChildren<ParticleSystem>(true);
            if (stages.Length != expectedStages)
                report.errors.Add(expectedName + " has " + stages.Length + " particle stages; expected " + expectedStages + ".");
            int loopingStages = stages.Count(stage => stage.main.loop);
            if (loopingStages != 0)
                report.errors.Add(expectedName + " has " + loopingStages + " looping particle stages; expected one-shot effects only.");
            if (stages.Any(stage => !HasExpectedStartColor(stage, TeamColors[i])))
                report.errors.Add(expectedName + " does not use the team " + (i + 1) + " palette color on every stage.");
        }

        if (member)
            report.nullTeamMemberEffects = nullCount;
        else
            report.nullTeamEffects = nullCount;

        if (nullCount != 0)
            report.errors.Add(fieldName + " contains " + nullCount + " null bindings.");
    }

    private static void ValidateEffectLayout(SerializedObject randomizerSo, ValidationReport report)
    {
        SerializedProperty completionEffects = randomizerSo.FindProperty("teamEffects");
        SerializedProperty memberEffects = randomizerSo.FindProperty("teamMemberEffects");
        if (completionEffects == null || memberEffects == null ||
            !completionEffects.isArray || !memberEffects.isArray ||
            completionEffects.arraySize != PresentationCapacity ||
            memberEffects.arraySize != PresentationCapacity)
        {
            report.effectLayoutMismatchCount = -1;
            report.effectSortingMismatchCount = -1;
            return;
        }

        for (int index = 0; index < PresentationCapacity; index++)
        {
            ParticleSystem completion = completionEffects.GetArrayElementAtIndex(index).objectReferenceValue as ParticleSystem;
            ParticleSystem member = memberEffects.GetArrayElementAtIndex(index).objectReferenceValue as ParticleSystem;
            if (completion == null || member == null)
                continue;

            Vector3 completionPosition = completion.transform.position;
            Vector3 memberPosition = member.transform.position;
            bool positionMatches = Vector2.Distance(completionPosition, memberPosition) <= 0.02f &&
                                   Mathf.Abs(completionPosition.z) <= 0.001f;
            bool scaleMatches = Vector3.Distance(completion.transform.localScale, Vector3.one) <= 0.001f;
            if (!positionMatches || !scaleMatches)
            {
                report.effectLayoutMismatchCount++;
                report.errors.Add("Team " + (index + 1) +
                                  " completion effect must align with its member effect in XY, use Z=0, and have unit scale.");
            }

            ParticleSystemRenderer[] memberRenderers = member.GetComponentsInChildren<ParticleSystemRenderer>(true);
            ParticleSystemRenderer[] completionRenderers = completion.GetComponentsInChildren<ParticleSystemRenderer>(true);
            bool sortingMatches = memberRenderers.Length > 0 && completionRenderers.Length > 0;
            if (sortingMatches)
            {
                int sortingLayerId = memberRenderers[0].sortingLayerID;
                int sortingOrder = memberRenderers[0].sortingOrder;
                sortingMatches = memberRenderers.All(renderer =>
                                     renderer.sortingLayerID == sortingLayerId &&
                                     renderer.sortingOrder == sortingOrder) &&
                                 completionRenderers.All(renderer =>
                                     renderer.sortingLayerID == sortingLayerId &&
                                     renderer.sortingOrder == sortingOrder);
            }

            if (!sortingMatches)
            {
                report.effectSortingMismatchCount++;
                report.errors.Add("Team " + (index + 1) +
                                  " member/completion particle renderers do not share the saved sorting settings.");
            }
        }
    }

    private static void ValidateTeamTargets(
        RandomMoveLight randomMoveLight,
        TeamRandomizer randomizer,
        ValidationReport report)
    {
        SerializedProperty targets = new SerializedObject(randomMoveLight).FindProperty("teamTargets");
        report.teamTargetsCount = targets != null && targets.isArray ? targets.arraySize : -1;
        if (report.teamTargetsCount != PresentationCapacity)
        {
            report.errors.Add("RandomMoveLight.teamTargets length is " + report.teamTargetsCount +
                              "; expected " + PresentationCapacity + ".");
            return;
        }

        SerializedProperty teamTexts = randomizer != null
            ? new SerializedObject(randomizer).FindProperty("teamTexts")
            : null;
        int nullCount = 0;
        for (int i = 0; i < PresentationCapacity; i++)
        {
            Transform target = targets.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            TMP_Text teamText = teamTexts != null && teamTexts.isArray && teamTexts.arraySize > i
                ? teamTexts.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text
                : null;
            if (target == null)
            {
                nullCount++;
                continue;
            }
            if (teamText == null || target != teamText.transform)
                report.errors.Add("teamTargets[" + i + "] does not match teamTexts[" + i + "].transform.");
        }

        report.nullTeamTargets = nullCount;
        if (nullCount != 0)
            report.errors.Add("RandomMoveLight.teamTargets contains " + nullCount + " null bindings.");
    }

    private static void ValidateFlexibleTeamConfiguration(
        TeamRandomizer randomizer,
        SerializedObject randomizerSo,
        ValidationReport report)
    {
        SerializedProperty teamTexts = randomizerSo.FindProperty("teamTexts");
        SerializedProperty teamTitles = randomizerSo.FindProperty("TeamTitles");
        SerializedProperty players = randomizerSo.FindProperty("players");
        report.teamTextsCount = teamTexts != null && teamTexts.isArray ? teamTexts.arraySize : -1;
        report.teamTitlesCount = teamTitles != null && teamTitles.isArray ? teamTitles.arraySize : -1;
        report.playerCount = players != null && players.isArray ? players.arraySize : -1;
        report.supportedTeamCapacity = randomizer.SupportedTeamCapacity;

        if (report.teamTextsCount != PresentationCapacity)
            report.errors.Add("teamTexts length is " + report.teamTextsCount +
                              "; expected " + PresentationCapacity + ".");
        if (report.teamTitlesCount != PresentationCapacity)
            report.errors.Add("TeamTitles length is " + report.teamTitlesCount +
                              "; expected " + PresentationCapacity + ".");
        if (report.supportedTeamCapacity != PresentationCapacity)
            report.errors.Add("Supported team capacity is " + report.supportedTeamCapacity +
                              "; expected " + PresentationCapacity + ".");

        TMP_InputField teamCountInput = randomizerSo.FindProperty("teamCountInputField")
            ?.objectReferenceValue as TMP_InputField;
        Toggle detailedToggle = randomizerSo.FindProperty("detailedTeamSizesToggle")
            ?.objectReferenceValue as Toggle;
        TMP_InputField detailedInput = randomizerSo.FindProperty("detailedTeamSizesInputField")
            ?.objectReferenceValue as TMP_InputField;
        GridLayoutGroup gridLayout = randomizerSo.FindProperty("teamGridLayout")
            ?.objectReferenceValue as GridLayoutGroup;
        GameObject gridSpacer = randomizerSo.FindProperty("teamGridSpacer")
            ?.objectReferenceValue as GameObject;

        report.teamCountInputBound = teamCountInput != null;
        report.detailedTeamSizesToggleBound = detailedToggle != null;
        report.detailedTeamSizesInputBound = detailedInput != null;
        report.teamGridLayoutBound = gridLayout != null;
        report.teamGridSpacerBound = gridSpacer != null;

        if (!report.teamCountInputBound)
            report.errors.Add("teamCountInputField is not assigned.");
        if (!report.detailedTeamSizesToggleBound)
            report.errors.Add("detailedTeamSizesToggle is not assigned.");
        if (!report.detailedTeamSizesInputBound)
            report.errors.Add("detailedTeamSizesInputField is not assigned.");
        if (!report.teamGridLayoutBound)
            report.errors.Add("teamGridLayout is not assigned.");
        if (!report.teamGridSpacerBound)
            report.errors.Add("teamGridSpacer is not assigned.");

        report.defaultTeamCount = teamCountInput != null && int.TryParse(teamCountInput.text, out int parsedCount)
            ? parsedCount
            : -1;
        report.detailedTeamSizesEnabled = detailedToggle != null && detailedToggle.isOn;
        report.configuredTeamSizesText = detailedInput != null ? detailedInput.text : string.Empty;

        bool automaticValid = TeamAllocation.TryBuildTeamSizes(
            report.playerCount,
            report.defaultTeamCount,
            PresentationCapacity,
            false,
            string.Empty,
            out int[] automaticSizes,
            out string automaticError);
        bool detailedDefaultsValid = TeamAllocation.TryBuildTeamSizes(
            report.playerCount,
            report.defaultTeamCount,
            PresentationCapacity,
            true,
            report.configuredTeamSizesText,
            out int[] detailedSizes,
            out string detailedError);

        report.configuredTeamSizes = detailedDefaultsValid ? detailedSizes : automaticSizes;
        report.configuredMemberTotal = report.configuredTeamSizes?.Sum() ?? 0;
        report.teamConfigurationValid = automaticValid && detailedDefaultsValid &&
                                        report.defaultTeamCount == 5 &&
                                        !report.detailedTeamSizesEnabled &&
                                        report.teamCountInputBound &&
                                        report.detailedTeamSizesToggleBound &&
                                        report.detailedTeamSizesInputBound &&
                                        report.teamGridLayoutBound &&
                                        report.teamGridSpacerBound;

        if (!automaticValid)
            report.errors.Add("Default automatic team configuration is invalid: " + automaticError);
        if (!detailedDefaultsValid)
            report.errors.Add("Saved detailed team sizes are invalid: " + detailedError);
        if (report.defaultTeamCount != 5)
            report.errors.Add("Default team count is " + report.defaultTeamCount + "; expected 5.");
        if (report.detailedTeamSizesEnabled)
            report.errors.Add("Detailed team sizes must be disabled by default.");
    }

    private static void ValidateResponsiveLayout(
        TeamRandomizer randomizer,
        SerializedObject randomizerSo,
        ValidationReport report)
    {
        GridLayoutGroup gridLayout = randomizerSo.FindProperty("teamGridLayout")
            ?.objectReferenceValue as GridLayoutGroup;
        GameObject settingsPanel = randomizerSo.FindProperty("SettingCanvas")
            ?.objectReferenceValue as GameObject;
        GameObject completedText = randomizerSo.FindProperty("CompletedText")
            ?.objectReferenceValue as GameObject;
        GameObject exitButton = randomizerSo.FindProperty("ExitButton")
            ?.objectReferenceValue as GameObject;

        Canvas resultCanvas = gridLayout != null ? gridLayout.GetComponentInParent<Canvas>() : null;
        Canvas settingsCanvas = settingsPanel != null ? settingsPanel.GetComponentInParent<Canvas>(true) : null;
        Canvas exitCanvas = exitButton != null ? exitButton.GetComponentInParent<Canvas>(true) : null;
        CanvasScaler settingsScaler = settingsCanvas != null ? settingsCanvas.GetComponent<CanvasScaler>() : null;
        Camera resultCamera = resultCanvas != null ? resultCanvas.worldCamera : null;

        report.responsiveLayoutReady = randomizer.ResponsiveLayoutReady;
        report.resultCanvasBound = resultCanvas != null;
        report.resultCameraBound = resultCamera != null;
        report.settingsCanvasBound = settingsCanvas != null;
        report.settingsScalerBound = settingsScaler != null;
        report.completionTextBound = completedText != null;
        report.safeAreaExitButtonBound = exitButton != null && exitCanvas == settingsCanvas;

        if (!report.responsiveLayoutReady)
            report.errors.Add("TeamRandomizer responsive layout dependencies are incomplete.");
        if (!report.resultCanvasBound || resultCanvas.renderMode != RenderMode.WorldSpace)
            report.errors.Add("The responsive result layout requires its existing World Space Canvas.");
        if (!report.resultCameraBound || !resultCamera.orthographic)
            report.errors.Add("The responsive result layout requires an orthographic world camera.");
        if (!report.settingsCanvasBound || settingsCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            report.errors.Add("The responsive settings layout requires a Screen Space Overlay Canvas.");
        if (!report.settingsScalerBound)
            report.errors.Add("The settings Canvas has no CanvasScaler for runtime responsive configuration.");
        if (!report.completionTextBound)
            report.errors.Add("CompletedText is not assigned for safe-area placement.");
        if (!report.safeAreaExitButtonBound)
            report.errors.Add("ExitButton is not assigned to the settings overlay Canvas.");

        string[] names = { "landscape-16-9", "landscape-4-3", "ultrawide-21-9", "portrait-9-16", "portrait-safe-area" };
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

        for (int profileIndex = 0; profileIndex < names.Length; profileIndex++)
        {
            float aspect = widths[profileIndex] / (float)heights[profileIndex];
            Rect safeWorldRect = ResponsiveUiLayout.GetSafeWorldRect(
                widths[profileIndex],
                heights[profileIndex],
                safeAreas[profileIndex],
                Vector2.zero,
                5f);
            Rect teamWorldRect = ResponsiveUiLayout.GetTeamWorldRect(safeWorldRect);
            Rect completionWorldRect = ResponsiveUiLayout.GetCompletionWorldRect(safeWorldRect);
            ResponsiveProfileReport profile = new ResponsiveProfileReport
            {
                name = names[profileIndex],
                width = widths[profileIndex],
                height = heights[profileIndex],
                minimumScale = 1f,
                success = teamWorldRect.width > 0f && teamWorldRect.height > 0f &&
                          completionWorldRect.width > 0f && completionWorldRect.height > 0f
            };

            for (int teamCount = TeamAllocation.MinimumTeamCount;
                 teamCount <= PresentationCapacity;
                 teamCount++)
            {
                int columns = ResponsiveUiLayout.GetColumnCount(teamCount, aspect);
                int rows = ResponsiveUiLayout.GetRowCount(teamCount, columns);
                float scale = ResponsiveUiLayout.GetUniformFitScale(
                    new Vector2(columns * 3f, rows * 3f),
                    teamWorldRect.size);
                profile.minimumScale = Mathf.Min(profile.minimumScale, scale);
                profile.maximumColumns = Mathf.Max(profile.maximumColumns, columns);
                profile.teamCountsValidated++;
                profile.success &= columns >= 1 && columns <= 3 &&
                                   rows * columns >= teamCount &&
                                   scale > 0f && scale <= 1f;
            }

            if (aspect < ResponsiveUiLayout.NarrowAspectThreshold)
                profile.success &= ResponsiveUiLayout.GetColumnCount(PresentationCapacity, aspect) == 2;
            else
                profile.success &= ResponsiveUiLayout.GetColumnCount(PresentationCapacity, aspect) == 3;

            report.responsiveProfiles.Add(profile);
            if (!profile.success)
                report.errors.Add("Responsive layout profile failed: " + profile.name + ".");
        }

        report.layoutProfileCount = report.responsiveProfiles.Count;
    }

    private static void ValidateNativeComponents(Scene scene, ValidationReport report)
    {
        ValidateNativeCount<NativeTmpTextReveal>(scene, 6, report);
        ValidateNativeCount<NativeLightPulse>(scene, 4, report);
        ValidateNativeCount<NativeImageFade>(scene, 1, report);
        ValidateNativeCount<NativeCompletionTextFeedback>(scene, 1, report);
    }

    private static void ValidateNativeCount<T>(Scene scene, int expected, ValidationReport report)
        where T : Component
    {
        int count = GetSceneComponents<T>(scene).Count();
        report.nativeComponentCounts.Add(new NamedCount
        {
            name = typeof(T).Name,
            count = count,
            expected = expected
        });
        if (count != expected)
            report.errors.Add(typeof(T).Name + " count is " + count + "; expected " + expected + ".");
    }

    private static void ValidateAudio(
        SerializedObject randomizerSo,
        SoundManager soundManager,
        MusicPlayer musicPlayer,
        ValidationReport report)
    {
        AudioClip memberClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MemberClipPath);
        AudioClip completionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CompletionClipPath);
        report.memberAudioPath = MemberClipPath;
        report.completionAudioPath = CompletionClipPath;

        AudioSource memberSource = randomizerSo.FindProperty("effect")?.objectReferenceValue as AudioSource;
        report.memberAudioValid = memberSource != null && memberSource.clip == memberClip;
        if (!report.memberAudioValid)
            report.errors.Add("TeamRandomizer.effect is not bound to " + MemberClipPath + ".");

        if (soundManager != null)
        {
            SerializedProperty clips = new SerializedObject(soundManager).FindProperty("popSfx");
            report.soundManagerAudioCount = clips != null && clips.isArray ? clips.arraySize : -1;
            report.soundManagerAudioValid = report.soundManagerAudioCount == 2 &&
                                            clips.GetArrayElementAtIndex(0).objectReferenceValue == completionClip &&
                                            clips.GetArrayElementAtIndex(1).objectReferenceValue == memberClip;
            if (!report.soundManagerAudioValid)
                report.errors.Add("SoundManager.popSfx must be exactly [scorefillend.wav, scorefill 1.wav].");
        }

        AudioSource musicSource = musicPlayer != null ? musicPlayer.GetComponent<AudioSource>() : null;
        report.musicClipCleared = musicSource != null && musicSource.clip == null;
        if (!report.musicClipCleared)
            report.errors.Add("MusicPlayer still has a BGM clip reference or no AudioSource.");
    }

    private static void ValidateGeneratedPrefabs(ValidationReport report)
    {
        for (int i = 0; i < PresentationCapacity; i++)
        {
            ValidateGeneratedPrefab(GetMemberPrefabPath(i), 2, TeamColors[i], report);
            ValidateGeneratedPrefab(GetTeamPrefabPath(i), 3, TeamColors[i], report);
        }
    }

    private static void ValidateGeneratedPrefab(
        string path,
        int expectedStages,
        Color expectedColor,
        ValidationReport report)
    {
        PrefabReport prefabReport = new PrefabReport
        {
            path = path,
            expectedStageCount = expectedStages
        };
        report.generatedPrefabs.Add(prefabReport);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        prefabReport.exists = prefab != null;
        if (prefab == null)
        {
            report.errors.Add("Generated prefab is missing: " + path);
            return;
        }

        ParticleSystem[] stages = prefab.GetComponentsInChildren<ParticleSystem>(true);
        prefabReport.stageCount = stages.Length;
        prefabReport.loopingStageCount = stages.Count(stage => stage.main.loop);
        prefabReport.teamColorValid = stages.All(stage => HasExpectedStartColor(stage, expectedColor));
        prefabReport.vendorDependencyCount = EditorUtility
            .CollectDependencies(new UnityEngine.Object[] { prefab })
            .Select(AssetDatabase.GetAssetPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(IsVendorPath);
        prefabReport.success = prefabReport.stageCount == expectedStages &&
                               prefabReport.loopingStageCount == 0 &&
                               prefabReport.teamColorValid &&
                               prefabReport.vendorDependencyCount == 0;

        if (prefabReport.stageCount != expectedStages)
            report.errors.Add(path + " has " + prefabReport.stageCount + " particle stages; expected " + expectedStages + ".");
        if (prefabReport.loopingStageCount != 0)
            report.errors.Add(path + " has " + prefabReport.loopingStageCount + " looping stages.");
        if (!prefabReport.teamColorValid)
            report.errors.Add(path + " does not use its assigned team palette color on every stage.");
        if (prefabReport.vendorDependencyCount != 0)
            report.errors.Add(path + " has " + prefabReport.vendorDependencyCount + " paid-package dependencies.");
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

    private static T FindExactlyOne<T>(Scene scene, List<string> errors) where T : Component
    {
        T[] matches = GetSceneComponents<T>(scene).ToArray();
        if (matches.Length == 1)
            return matches[0];
        errors.Add("Expected exactly one " + typeof(T).Name + ", found " + matches.Length + ".");
        return null;
    }

    private static IEnumerable<T> GetSceneComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static IEnumerable<GameObject> GetSceneGameObjects(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject);
    }

    private static string GetPrefabSourcePath(GameObject gameObject)
    {
        if (gameObject == null || !PrefabUtility.IsPartOfPrefabInstance(gameObject))
            return string.Empty;
        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
        return source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
    }

    private static bool IsMissingPrefabSafely(GameObject gameObject)
    {
        try
        {
            return PrefabUtility.IsPrefabAssetMissing(gameObject);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVendorPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string normalized = path.Replace('\\', '/').TrimEnd('/');
        return normalized.Equals(VendorRoot, StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith(VendorRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountSerializedVendorMarkers()
    {
        string fullPath = Path.GetFullPath(ScenePath);
        if (!File.Exists(fullPath))
            return 0;
        string text = File.ReadAllText(fullPath);
        return CountOccurrences(text, "Retro Arsenal");
    }

    private static string[] FindUnresolvedSerializedGuids()
    {
        string fullPath = Path.GetFullPath(ScenePath);
        if (!File.Exists(fullPath))
            return Array.Empty<string>();

        string text = File.ReadAllText(fullPath);
        return Regex.Matches(text, @"guid:\s*([0-9a-fA-F]{32})")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Where(guid => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(guid => guid, StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountOccurrences(string text, string marker)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += marker.Length;
        }
        return count;
    }

    private static string GetMemberPrefabPath(int index)
    {
        return PrefabRoot + "/MemberEffect_Team" + (index + 1) + ".prefab";
    }

    private static string GetTeamPrefabPath(int index)
    {
        return PrefabRoot + "/TeamCompletionEffect_Team" + (index + 1) + ".prefab";
    }

    [Serializable]
    private sealed class ValidationReport
    {
        public bool success;
        public string scenePath;
        public int missingScriptCount;
        public int missingPrefabCount;
        public int teamEffectsCount;
        public int teamMemberEffectsCount;
        public int teamTargetsCount;
        public int supportedTeamCapacity;
        public int defaultTeamCount;
        public int playerCount;
        public int teamTextsCount;
        public int teamTitlesCount;
        public bool detailedTeamSizesEnabled;
        public string configuredTeamSizesText;
        public int[] configuredTeamSizes = Array.Empty<int>();
        public int configuredMemberTotal;
        public bool teamConfigurationValid;
        public bool teamCountInputBound;
        public bool detailedTeamSizesToggleBound;
        public bool detailedTeamSizesInputBound;
        public bool teamGridLayoutBound;
        public bool teamGridSpacerBound;
        public bool responsiveLayoutReady;
        public bool resultCanvasBound;
        public bool resultCameraBound;
        public bool settingsCanvasBound;
        public bool settingsScalerBound;
        public bool completionTextBound;
        public bool safeAreaExitButtonBound;
        public int layoutProfileCount;
        public List<ResponsiveProfileReport> responsiveProfiles = new List<ResponsiveProfileReport>();
        public int nullTeamEffects;
        public int nullTeamMemberEffects;
        public int nullTeamTargets;
        public int effectLayoutMismatchCount;
        public int effectSortingMismatchCount;
        public bool vendorFolderPresent;
        public int vendorSceneObjectCount;
        public int vendorDependencyCount;
        public int serializedVendorMarkerCount;
        public int legacyGuidCount;
        public string[] unresolvedGuids = Array.Empty<string>();
        public int moreMountainsComponentCount;
        public List<NamedCount> nativeComponentCounts = new List<NamedCount>();
        public bool memberAudioValid;
        public bool soundManagerAudioValid;
        public bool musicClipCleared;
        public int soundManagerAudioCount;
        public string memberAudioPath;
        public string completionAudioPath;
        public List<PrefabReport> generatedPrefabs = new List<PrefabReport>();
        public List<string> errors;

        public void FinalizeReport()
        {
            success = errors != null && errors.Count == 0;
        }
    }

    [Serializable]
    private sealed class NamedCount
    {
        public string name;
        public int count;
        public int expected;
    }

    [Serializable]
    private sealed class ResponsiveProfileReport
    {
        public string name;
        public int width;
        public int height;
        public int teamCountsValidated;
        public int maximumColumns;
        public float minimumScale;
        public bool success;
    }

    [Serializable]
    private sealed class PrefabReport
    {
        public string path;
        public bool exists;
        public int stageCount;
        public int expectedStageCount;
        public int loopingStageCount;
        public bool teamColorValid;
        public int vendorDependencyCount;
        public bool success;
    }
}
