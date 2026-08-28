using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TeamRandomizer : MonoBehaviour
{
    [Header("UI & 이펙트")]
    [SerializeField] private TMP_Text[] teamTexts;
    [SerializeField] private ParticleSystem[] teamEffects;
    [SerializeField] private ParticleSystem[] teamMemberEffects;
    [SerializeField] private AudioSource effect;
    [SerializeField] private RandomMoveLight randomMoveLight;
    [SerializeField] private GameObject[] TeamTitles;
    [SerializeField] private MusicPlayer musicPlayer;
    [SerializeField] private GridLayoutGroup teamGridLayout;
    [SerializeField] private GameObject teamGridSpacer;

    [SerializeField] private GameObject SettingCanvas;
    [SerializeField] private Button shuffleButton;
    [SerializeField] private GameObject CompletedText;
    [SerializeField] private Subscription subscription;

    [Header("옵션")]
    [SerializeField] private Toggle noDuplicateToggle;
    [SerializeField] private Toggle fixedSeedToggle;
    [SerializeField] private TMP_InputField seedInputField;

    [Header("팀 구성")]
    [SerializeField] private TMP_InputField teamCountInputField;
    [SerializeField] private Toggle detailedTeamSizesToggle;
    [SerializeField] private TMP_InputField detailedTeamSizesInputField;

    [Header("플레이어 명단")]
    [SerializeField] private List<string> players = new();

    [Header("깍두기")]
    [SerializeField] private List<string> extras = new();

    [Header("Exit Button")]
    [SerializeField] private GameObject ExitButton;

    [Header("For Debugging")]
    [SerializeField] private TextMeshProUGUI ErrorText;

    private readonly List<List<string>> teams = new();
    private readonly List<string> teamsToShow = new();

    private System.Random rng;
    private int lastPresentedTeamCount = -1;
    private int currentTeamCount = TeamAllocation.MinimumTeamCount;
    private int responsiveColumnCount;
    private float responsiveTeamScale = 1f;
    private float responsiveAspect = 16f / 9f;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Rect lastSafeArea;
    private RectTransform teamGridRect;
    private Vector3 teamGridBaseLocalPosition;
    private Vector3 teamGridBaseLocalScale = Vector3.one;
    private RectTransform completedTextRect;
    private Vector3 completedTextBaseLocalScale = Vector3.one;
    private Canvas resultCanvas;
    private Camera resultCamera;
    private Vector2 resultCameraBasePosition;
    private float resultCameraBaseOrthographicSize = 5f;
    private Canvas settingsCanvas;
    private RectTransform settingsCanvasRect;
    private RectTransform settingsPanelRect;
    private RectTransform resultExitButtonRect;
    private bool settingsStructureConfigured;
    [NonSerialized] private string resultCsvPathOverride;

    public int SupportedTeamCapacity => GetPresentationCapacity();
    public int ResponsiveColumnCount => responsiveColumnCount;
    public float ResponsiveTeamScale => responsiveTeamScale;
    public float ResponsiveAspect => responsiveAspect;
    public bool ResponsiveLayoutReady =>
        teamGridLayout != null && CompletedText != null && SettingCanvas != null && ExitButton != null;

    private void Awake()
    {
        CacheResponsiveLayoutReferences();

        if (fixedSeedToggle != null)
        {
            fixedSeedToggle.onValueChanged.AddListener(OnFixedSeedToggleChanged);
            OnFixedSeedToggleChanged(fixedSeedToggle.isOn);
        }

        if (teamCountInputField != null)
            teamCountInputField.onEndEdit.AddListener(OnTeamCountChanged);

        if (detailedTeamSizesToggle != null)
            detailedTeamSizesToggle.onValueChanged.AddListener(OnDetailedTeamSizesToggleChanged);

        if (detailedTeamSizesInputField != null)
            detailedTeamSizesInputField.onEndEdit.AddListener(OnDetailedTeamSizesChanged);

        if (shuffleButton != null)
            shuffleButton.onClick.AddListener(ShuffleTeams);

        RefreshTeamConfigurationUi(false);
    }

    private void Update()
    {
        Rect safeArea = Screen.safeArea;
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight && safeArea == lastSafeArea)
            return;

        RefreshResponsiveLayout();
    }

    private void CacheResponsiveLayoutReferences()
    {
        teamGridRect = teamGridLayout != null ? teamGridLayout.transform as RectTransform : null;
        if (teamGridRect != null)
        {
            teamGridBaseLocalPosition = teamGridRect.localPosition;
            teamGridBaseLocalScale = teamGridRect.localScale;
            resultCanvas = teamGridRect.GetComponentInParent<Canvas>();
        }

        completedTextRect = CompletedText != null ? CompletedText.transform as RectTransform : null;
        if (completedTextRect != null)
            completedTextBaseLocalScale = completedTextRect.localScale;

        resultCamera = resultCanvas != null && resultCanvas.worldCamera != null
            ? resultCanvas.worldCamera
            : Camera.main;
        if (resultCamera != null)
        {
            resultCameraBasePosition = resultCamera.transform.position;
            resultCameraBaseOrthographicSize = resultCamera.orthographicSize;
        }

        settingsPanelRect = SettingCanvas != null ? SettingCanvas.transform as RectTransform : null;
        settingsCanvas = SettingCanvas != null ? SettingCanvas.GetComponentInParent<Canvas>(true) : null;
        settingsCanvasRect = settingsCanvas != null ? settingsCanvas.transform as RectTransform : null;
        resultExitButtonRect = ExitButton != null ? ExitButton.transform as RectTransform : null;

        ConfigureSettingsStructure();
    }

    private void ConfigureSettingsStructure()
    {
        if (settingsStructureConfigured)
            return;

        settingsStructureConfigured = true;
        if (settingsCanvas != null)
        {
            CanvasScaler scaler = settingsCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        if (settingsPanelRect == null)
            return;

        HashSet<Transform> rows = new HashSet<Transform>();
        AddSettingsRow(rows, fixedSeedToggle);
        AddSettingsRow(rows, teamCountInputField);
        AddSettingsRow(rows, detailedTeamSizesToggle);
        foreach (Transform row in rows)
            ConfigureSettingsRow(row);

        if (noDuplicateToggle != null && noDuplicateToggle.transform is RectTransform duplicateRect)
            duplicateRect.sizeDelta = new Vector2(560f, 48f);

        VerticalLayoutGroup verticalLayout = settingsPanelRect.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null)
            verticalLayout.spacing = 8f;

        foreach (Button button in settingsPanelRect.GetComponentsInChildren<Button>(true))
        {
            if (button.transform is RectTransform buttonRect)
                buttonRect.sizeDelta = new Vector2(240f, 72f);
        }

        foreach (TMP_Text text in settingsPanelRect.GetComponentsInChildren<TMP_Text>(true))
            ConfigureResponsiveSettingsText(text);

        if (ErrorText != null && ErrorText.transform is RectTransform errorRect)
        {
            errorRect.anchorMin = new Vector2(0.5f, 0f);
            errorRect.anchorMax = new Vector2(0.5f, 0f);
            errorRect.pivot = new Vector2(0.5f, 0f);
            errorRect.sizeDelta = new Vector2(1000f, 90f);
            ErrorText.alignment = TextAlignmentOptions.Center;
            ErrorText.textWrappingMode = TextWrappingModes.Normal;
            ConfigureResponsiveSettingsText(ErrorText);
        }
    }

    private static void AddSettingsRow(ISet<Transform> rows, Component control)
    {
        if (control != null && control.transform.parent != null)
            rows.Add(control.transform.parent);
    }

    private static void ConfigureSettingsRow(Transform row)
    {
        if (row == null || !(row is RectTransform rowRect))
            return;

        rowRect.sizeDelta = new Vector2(560f, 64f);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            if (row.GetComponent<LayoutGroup>() != null)
                return;
            layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        if (layout == null)
            return;

        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int index = 0; index < row.childCount; index++)
        {
            Transform child = row.GetChild(index);
            if (!(child is RectTransform childRect))
                continue;

            if (child.GetComponent<TMP_InputField>() != null)
                childRect.sizeDelta = new Vector2(210f, 48f);
            else if (child.GetComponent<Toggle>() != null)
                childRect.sizeDelta = new Vector2(250f, 48f);
            else if (child.GetComponent<TMP_Text>() != null)
                childRect.sizeDelta = new Vector2(240f, 48f);
            else if (child.GetComponentInChildren<TMP_Text>(true) != null)
                childRect.sizeDelta = new Vector2(250f, 48f);
            else
                childRect.sizeDelta = new Vector2(16f, 1f);
        }
    }

    private static void ConfigureResponsiveSettingsText(TMP_Text text)
    {
        if (text == null)
            return;

        float originalSize = Mathf.Max(14f, text.fontSize);
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(originalSize, 16f);
        text.fontSizeMax = Mathf.Max(originalSize, 24f);
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void OnDestroy()
    {
        if (fixedSeedToggle != null)
            fixedSeedToggle.onValueChanged.RemoveListener(OnFixedSeedToggleChanged);
        if (teamCountInputField != null)
            teamCountInputField.onEndEdit.RemoveListener(OnTeamCountChanged);
        if (detailedTeamSizesToggle != null)
            detailedTeamSizesToggle.onValueChanged.RemoveListener(OnDetailedTeamSizesToggleChanged);
        if (detailedTeamSizesInputField != null)
            detailedTeamSizesInputField.onEndEdit.RemoveListener(OnDetailedTeamSizesChanged);
        if (shuffleButton != null)
            shuffleButton.onClick.RemoveListener(ShuffleTeams);
    }

    private void OnFixedSeedToggleChanged(bool isOn)
    {
        if (seedInputField != null)
            seedInputField.gameObject.SetActive(isOn);
    }

    private void OnTeamCountChanged(string value)
    {
        if (!TryParseTeamCount(value, out int teamCount, out string errorMessage))
        {
            SetError(errorMessage);
            return;
        }

        if (teamCount != lastPresentedTeamCount)
        {
            WriteBalancedSizes(teamCount);
            lastPresentedTeamCount = teamCount;
        }

        ApplyPresentationLayout(teamCount, true);
        ValidateConfigurationPreview();
    }

    private void OnDetailedTeamSizesToggleChanged(bool isOn)
    {
        if (detailedTeamSizesInputField != null)
            detailedTeamSizesInputField.gameObject.SetActive(isOn);

        if (TryParseTeamCount(teamCountInputField?.text, out int teamCount, out _))
        {
            if (string.IsNullOrWhiteSpace(detailedTeamSizesInputField?.text))
                WriteBalancedSizes(teamCount);
            ApplyPresentationLayout(teamCount, true);
        }

        ValidateConfigurationPreview();
    }

    private void OnDetailedTeamSizesChanged(string value)
    {
        ValidateConfigurationPreview();
    }

    private void RefreshTeamConfigurationUi(bool overwriteDetailedSizes)
    {
        bool detailed = detailedTeamSizesToggle != null && detailedTeamSizesToggle.isOn;
        if (detailedTeamSizesInputField != null)
            detailedTeamSizesInputField.gameObject.SetActive(detailed);

        if (!TryParseTeamCount(teamCountInputField?.text, out int teamCount, out string errorMessage))
        {
            SetError(errorMessage);
            return;
        }

        if (overwriteDetailedSizes || string.IsNullOrWhiteSpace(detailedTeamSizesInputField?.text))
            WriteBalancedSizes(teamCount);

        lastPresentedTeamCount = teamCount;
        ApplyPresentationLayout(teamCount, true);
        ValidateConfigurationPreview();
    }

    private void ValidateConfigurationPreview()
    {
        if (TryReadTeamConfiguration(out _, out _, out string errorMessage))
            SetError(string.Empty);
        else
            SetError(errorMessage);
    }

    private void WriteBalancedSizes(int teamCount)
    {
        if (detailedTeamSizesInputField == null)
            return;

        if (TeamAllocation.TryBuildTeamSizes(
                players?.Count ?? 0,
                teamCount,
                GetPresentationCapacity(),
                false,
                string.Empty,
                out int[] balancedSizes,
                out _))
        {
            detailedTeamSizesInputField.SetTextWithoutNotify(string.Join(",", balancedSizes));
        }
    }

    public void ShuffleTeams()
    {
        if (shuffleButton != null)
            shuffleButton.interactable = false;
        if (SettingCanvas != null)
            SettingCanvas.SetActive(false);
        SetError(string.Empty);

        if (!TryCreateRandom(out rng, out string errorMessage))
        {
            FailShuffle(errorMessage);
            return;
        }

        if (players == null || players.Count == 0)
        {
            FailShuffle("플레이어 명단이 비어 있습니다.");
            return;
        }

        if (extras == null)
            extras = new List<string>();
        if (extras.Count > 3)
        {
            FailShuffle("깍두기는 0~3명이어야 합니다.");
            return;
        }

        if (!TryReadTeamConfiguration(out int teamCount, out int[] teamSizes, out errorMessage))
        {
            FailShuffle(errorMessage);
            return;
        }

        if (!TeamAllocation.TryGenerateTeams(
                players,
                extras,
                teamSizes,
                rng,
                out List<List<string>> generatedTeams,
                out errorMessage))
        {
            FailShuffle(errorMessage);
            return;
        }

        teams.Clear();
        teams.AddRange(generatedTeams);
        teamsToShow.Clear();
        teamsToShow.AddRange(teams.SelectMany(team => team));

        ApplyPresentationLayout(teamCount, true);
        SaveResultsToCsv();
        StartCoroutine(PlayTeamReveal());
    }

    private bool TryCreateRandom(out System.Random random, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (fixedSeedToggle != null && fixedSeedToggle.isOn)
        {
            if (!int.TryParse(seedInputField?.text, out int seed))
            {
                random = null;
                errorMessage = "시드값은 정수로 입력해야 합니다.";
                return false;
            }

            random = new System.Random(seed);
            return true;
        }

        random = new System.Random();
        return true;
    }

    private bool TryReadTeamConfiguration(
        out int teamCount,
        out int[] teamSizes,
        out string errorMessage)
    {
        teamSizes = Array.Empty<int>();
        if (!TryParseTeamCount(teamCountInputField?.text, out teamCount, out errorMessage))
            return false;

        bool useDetailedSizes = detailedTeamSizesToggle != null && detailedTeamSizesToggle.isOn;
        return TeamAllocation.TryBuildTeamSizes(
            players?.Count ?? 0,
            teamCount,
            GetPresentationCapacity(),
            useDetailedSizes,
            detailedTeamSizesInputField?.text,
            out teamSizes,
            out errorMessage);
    }

    private bool TryParseTeamCount(string value, out int teamCount, out string errorMessage)
    {
        int capacity = GetPresentationCapacity();
        if (!int.TryParse(value, out teamCount))
        {
            errorMessage = "팀 수는 정수로 입력해야 합니다.";
            return false;
        }

        if (teamCount < TeamAllocation.MinimumTeamCount || teamCount > capacity)
        {
            errorMessage = $"팀 수는 {TeamAllocation.MinimumTeamCount}~{capacity} 사이여야 합니다.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private int GetPresentationCapacity()
    {
        int capacity = new[]
        {
            teamTexts?.Length ?? 0,
            TeamTitles?.Length ?? 0,
            teamEffects?.Length ?? 0,
            teamMemberEffects?.Length ?? 0,
            randomMoveLight != null ? randomMoveLight.TeamTargetCount : 0
        }.Min();

        for (int index = 0; index < capacity; index++)
        {
            if (teamTexts[index] == null || TeamTitles[index] == null ||
                teamEffects[index] == null || teamMemberEffects[index] == null)
                return index;
        }

        return capacity;
    }

    private void FailShuffle(string message)
    {
        SetError(message);
        if (shuffleButton != null)
            shuffleButton.interactable = true;
        if (SettingCanvas != null)
            SettingCanvas.SetActive(true);
    }

    private void SetError(string message)
    {
        if (ErrorText != null)
            ErrorText.text = message ?? string.Empty;
    }

    private void ApplyPresentationLayout(int teamCount, bool resetContent)
    {
        if (teamTexts == null)
            return;

        currentTeamCount = Mathf.Clamp(teamCount, 1, teamTexts.Length);

        for (int index = 0; index < teamTexts.Length; index++)
        {
            bool active = index < currentTeamCount;
            TMP_Text teamText = teamTexts[index];
            if (teamText != null && teamText.transform.parent != null)
                teamText.transform.parent.gameObject.SetActive(active);

            if (resetContent && teamText != null)
                teamText.text = string.Empty;
            if (TeamTitles != null && index < TeamTitles.Length && TeamTitles[index] != null)
                TeamTitles[index].SetActive(false);
        }

        RefreshResponsiveLayout();

        if (!resetContent)
            return;

        ResetEffects(teamEffects);
        ResetEffects(teamMemberEffects);
        if (CompletedText != null)
            CompletedText.SetActive(false);
        if (ExitButton != null)
            ExitButton.SetActive(false);
    }

    private void RefreshResponsiveLayout()
    {
        lastScreenWidth = Mathf.Max(1, Screen.width);
        lastScreenHeight = Mathf.Max(1, Screen.height);
        lastSafeArea = Screen.safeArea;
        ApplyResponsiveLayoutForViewport(lastScreenWidth, lastScreenHeight, lastSafeArea);
    }

    private void ApplyResponsiveLayoutForViewport(int width, int height, Rect safeArea)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        responsiveAspect = width / (float)height;
        responsiveColumnCount = ResponsiveUiLayout.GetColumnCount(currentTeamCount, responsiveAspect);

        if (teamGridLayout != null)
        {
            teamGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            teamGridLayout.constraintCount = responsiveColumnCount;
        }

        if (teamGridSpacer != null)
            teamGridSpacer.SetActive(currentTeamCount == 5 && responsiveColumnCount == 3);

        Rect safeWorldRect = ResponsiveUiLayout.GetSafeWorldRect(
            width,
            height,
            safeArea,
            resultCameraBasePosition,
            resultCameraBaseOrthographicSize);

        ApplyResponsiveTeamGrid(safeWorldRect);
        ApplyResponsiveCompletionText(safeWorldRect);
        ApplyResponsiveSettingsLayout(width, height, safeArea);
        Canvas.ForceUpdateCanvases();
        SyncEffectsToTeamTexts();
    }

    private void ApplyResponsiveTeamGrid(Rect safeWorldRect)
    {
        if (teamGridRect == null || teamGridLayout == null)
            return;

        teamGridRect.localPosition = teamGridBaseLocalPosition;
        teamGridRect.localScale = teamGridBaseLocalScale;
        LayoutRebuilder.ForceRebuildLayoutImmediate(teamGridRect);
        Canvas.ForceUpdateCanvases();

        Rect availableRect = ResponsiveUiLayout.GetTeamWorldRect(safeWorldRect);
        responsiveTeamScale = 1f;
        if (TryGetTeamVisualBounds(out Bounds baseBounds))
        {
            responsiveTeamScale = ResponsiveUiLayout.GetUniformFitScale(
                new Vector2(baseBounds.size.x, baseBounds.size.y),
                availableRect.size);
        }

        teamGridRect.localScale = new Vector3(
            teamGridBaseLocalScale.x * responsiveTeamScale,
            teamGridBaseLocalScale.y * responsiveTeamScale,
            teamGridBaseLocalScale.z);
        LayoutRebuilder.ForceRebuildLayoutImmediate(teamGridRect);
        Canvas.ForceUpdateCanvases();

        if (!TryGetTeamVisualBounds(out Bounds scaledBounds))
            return;

        float targetCenterX = availableRect.center.x;
        float targetCenterY = availableRect.yMax - scaledBounds.extents.y;
        Vector3 delta = new Vector3(
            targetCenterX - scaledBounds.center.x,
            targetCenterY - scaledBounds.center.y,
            0f);

        if (scaledBounds.min.y + delta.y < availableRect.yMin)
            delta.y += availableRect.yMin - (scaledBounds.min.y + delta.y);

        teamGridRect.position += delta;
    }

    private bool TryGetTeamVisualBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        int[] plannedSizes = GetPlannedVisibleTeamSizes();

        for (int index = 0; index < currentTeamCount && index < (teamTexts?.Length ?? 0); index++)
        {
            TMP_Text teamText = teamTexts[index];
            if (teamText == null || !teamText.gameObject.activeInHierarchy)
                continue;

            EncapsulateRect(teamText.transform.parent as RectTransform, ref bounds, ref found);
            EncapsulateRect(teamText.rectTransform, ref bounds, ref found);

            if (TeamTitles != null && index < TeamTitles.Length && TeamTitles[index] != null)
                EncapsulateRect(TeamTitles[index].transform as RectTransform, ref bounds, ref found);

            int lineCount = index < plannedSizes.Length ? Mathf.Max(1, plannedSizes[index]) : 1;
            Vector3 finalLinePosition = teamText.transform.position +
                                        teamText.transform.TransformVector(Vector3.down * (45f * (lineCount - 1)));
            if (!found)
            {
                bounds = new Bounds(finalLinePosition, Vector3.zero);
                found = true;
            }
            else
            {
                bounds.Encapsulate(finalLinePosition);
            }
        }

        return found;
    }

    private int[] GetPlannedVisibleTeamSizes()
    {
        bool useDetailedSizes = detailedTeamSizesToggle != null && detailedTeamSizesToggle.isOn;
        if (!TeamAllocation.TryBuildTeamSizes(
                players?.Count ?? 0,
                currentTeamCount,
                GetPresentationCapacity(),
                useDetailedSizes,
                detailedTeamSizesInputField?.text,
                out int[] sizes,
                out _))
        {
            TeamAllocation.TryBuildTeamSizes(
                players?.Count ?? 0,
                currentTeamCount,
                GetPresentationCapacity(),
                false,
                string.Empty,
                out sizes,
                out _);
        }

        sizes ??= Enumerable.Repeat(1, currentTeamCount).ToArray();
        int extraAllowance = extras == null || extras.Count == 0
            ? 0
            : Mathf.CeilToInt(extras.Count / (float)Mathf.Max(1, currentTeamCount));
        if (extraAllowance > 0)
        {
            for (int index = 0; index < sizes.Length; index++)
                sizes[index] += extraAllowance;
        }

        return sizes;
    }

    private static void EncapsulateRect(RectTransform rect, ref Bounds bounds, ref bool found)
    {
        if (rect == null)
            return;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        for (int index = 0; index < corners.Length; index++)
        {
            if (!found)
            {
                bounds = new Bounds(corners[index], Vector3.zero);
                found = true;
            }
            else
            {
                bounds.Encapsulate(corners[index]);
            }
        }
    }

    private void ApplyResponsiveCompletionText(Rect safeWorldRect)
    {
        if (completedTextRect == null)
            return;

        completedTextRect.localScale = completedTextBaseLocalScale;
        Vector2 contentSize = GetWorldRectSize(completedTextRect);
        Rect completionRect = ResponsiveUiLayout.GetCompletionWorldRect(safeWorldRect);
        float scale = ResponsiveUiLayout.GetUniformFitScale(contentSize, completionRect.size);
        completedTextRect.localScale = new Vector3(
            completedTextBaseLocalScale.x * scale,
            completedTextBaseLocalScale.y * scale,
            completedTextBaseLocalScale.z);
        completedTextRect.position = new Vector3(
            completionRect.center.x,
            completionRect.center.y,
            completedTextRect.position.z);
    }

    private static Vector2 GetWorldRectSize(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return new Vector2(
            Vector3.Distance(corners[0], corners[3]),
            Vector3.Distance(corners[0], corners[1]));
    }

    private void ApplyResponsiveSettingsLayout(int width, int height, Rect safeArea)
    {
        if (settingsCanvasRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        Rect normalizedSafeArea = ResponsiveUiLayout.NormalizeSafeArea(width, height, safeArea);
        Vector2 canvasSize = settingsCanvasRect.rect.size;

        if (settingsPanelRect != null)
        {
            float scale = ResponsiveUiLayout.GetSettingsPanelScale(
                canvasSize,
                normalizedSafeArea,
                new Vector2(1000f, 620f));
            settingsPanelRect.localScale = new Vector3(scale, scale, 1f);
            settingsPanelRect.anchoredPosition = new Vector2(
                (normalizedSafeArea.center.x - 0.5f) * canvasSize.x,
                (normalizedSafeArea.center.y - 0.5f) * canvasSize.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(settingsPanelRect);
        }

        if (ErrorText != null && ErrorText.transform is RectTransform errorRect)
        {
            errorRect.sizeDelta = new Vector2(canvasSize.x * normalizedSafeArea.width * 0.9f, 90f);
            errorRect.anchoredPosition = new Vector2(
                (normalizedSafeArea.center.x - 0.5f) * canvasSize.x,
                normalizedSafeArea.yMin * canvasSize.y + 18f);
        }

        if (resultExitButtonRect != null)
        {
            resultExitButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
            resultExitButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
            resultExitButtonRect.pivot = new Vector2(0f, 1f);
            resultExitButtonRect.localScale = Vector3.one;
            resultExitButtonRect.sizeDelta = new Vector2(180f, 72f);
            resultExitButtonRect.anchoredPosition = new Vector2(
                (normalizedSafeArea.xMin - 0.5f) * canvasSize.x + 24f,
                (normalizedSafeArea.yMax - 0.5f) * canvasSize.y - 24f);
        }
    }

    private void SyncEffectsToTeamTexts()
    {
        int memberCount = Math.Min(teamTexts?.Length ?? 0, teamMemberEffects?.Length ?? 0);
        for (int index = 0; index < memberCount; index++)
        {
            if (teamTexts[index] == null || teamMemberEffects[index] == null)
                continue;
            teamMemberEffects[index].transform.position = GetEffectAnchor(index);
            teamMemberEffects[index].transform.localScale = Vector3.one * Mathf.Clamp(responsiveTeamScale, 0.55f, 1f);
        }

        int completionCount = Math.Min(teamTexts?.Length ?? 0, teamEffects?.Length ?? 0);
        for (int index = 0; index < completionCount; index++)
        {
            if (teamTexts[index] == null || teamEffects[index] == null)
                continue;
            teamEffects[index].transform.position = GetEffectAnchor(index);
            teamEffects[index].transform.localScale = Vector3.one * Mathf.Clamp(responsiveTeamScale, 0.55f, 1f);
        }
    }

    private Vector3 GetEffectAnchor(int teamIndex)
    {
        TMP_Text teamText = teamTexts[teamIndex];
        Vector3 anchor = teamText.transform.position +
                         teamText.transform.TransformVector(Vector3.down * 45f);
        anchor.z = 0f;
        return anchor;
    }

    private static void ResetEffects(ParticleSystem[] effects)
    {
        if (effects == null)
            return;

        foreach (ParticleSystem particle in effects)
        {
            if (particle == null)
                continue;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.gameObject.SetActive(false);
        }
    }

    private void PositionMemberEffect(int teamIndex, int memberIndex)
    {
        if (teamMemberEffects == null || teamTexts == null ||
            teamIndex < 0 || teamIndex >= teamMemberEffects.Length ||
            teamIndex >= teamTexts.Length || teamMemberEffects[teamIndex] == null ||
            teamTexts[teamIndex] == null)
            return;

        Vector3 position = GetEffectAnchor(teamIndex) +
                           teamTexts[teamIndex].transform.TransformVector(Vector3.down * (45f * memberIndex));
        position.z = 0f;
        teamMemberEffects[teamIndex].transform.position = position;
    }

    private void SaveResultsToCsv()
    {
        string dataPath = Application.dataPath;
        string outputFolder = Path.GetDirectoryName(dataPath);
        string filePath = string.IsNullOrWhiteSpace(resultCsvPathOverride)
            ? Path.Combine(outputFolder ?? dataPath, "Result.csv")
            : resultCsvPathOverride;

        try
        {
            int memberColumnCount = Math.Max(4, teams.Count == 0 ? 0 : teams.Max(team => team.Count));
            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                IEnumerable<string> header = new[] { "Team" }
                    .Concat(Enumerable.Range(1, memberColumnCount).Select(index => "Member" + index));
                writer.WriteLine(string.Join(",", header));

                for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
                {
                    var columns = new List<string> { "Team" + (teamIndex + 1) };
                    for (int memberIndex = 0; memberIndex < memberColumnCount; memberIndex++)
                    {
                        string member = memberIndex < teams[teamIndex].Count
                            ? teams[teamIndex][memberIndex]
                            : string.Empty;
                        columns.Add(EscapeCsv(member));
                    }
                    writer.WriteLine(string.Join(",", columns));
                }
            }

            Debug.Log($"Result.csv saved to {filePath}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save Result.csv: {exception}");
            SetError($"Result.csv 저장에 실패했습니다: {exception.Message}");
        }
    }

    private static string EscapeCsv(string value)
    {
        string safeValue = value ?? string.Empty;
        if (safeValue.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            return safeValue;
        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
    }

    private IEnumerator PlayTeamReveal()
    {
        subscription?.GoSub();
        musicPlayer?.Play();
        randomMoveLight?.GoLight();
        yield return new WaitForSeconds(30f);

        for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
        {
            List<string> group = teams[teamIndex];
            teamTexts[teamIndex].text = string.Empty;
            TeamTitles[teamIndex].SetActive(true);

            for (int memberIndex = 0; memberIndex < group.Count; memberIndex++)
            {
                string name = group[memberIndex];
                bool isLastMember = teamIndex == teams.Count - 1 && memberIndex == group.Count - 1;
                PositionMemberEffect(teamIndex, memberIndex);

                if (isLastMember)
                {
                    CameraFocusController.Instance?.FocusOnTeam(
                        teamTexts[teamIndex].transform,
                        ResponsiveUiLayout.GetFocusOrthographicSize(2.8f, responsiveAspect),
                        0.4f);
                    randomMoveLight?.FocusLightOnTeam(teamIndex);
                    CameraFocusController.Instance?.ShakeCamera(1f, 3f);

                    teamTexts[teamIndex].text += "김경호";
                    yield return new WaitForSeconds(3f);

                    teamMemberEffects[teamIndex].gameObject.SetActive(true);
                    teamMemberEffects[teamIndex].Play();
                    effect?.Play();

                    teamTexts[teamIndex].text = ReplaceLastLine(teamTexts[teamIndex].text, name);
                    teamsToShow.Remove(name);
                    PlayTextPop(teamTexts[teamIndex].transform);
                }
                else
                {
                    CameraFocusController.Instance?.FocusOnTeam(
                        teamTexts[teamIndex].transform,
                        ResponsiveUiLayout.GetFocusOrthographicSize(2.8f, responsiveAspect),
                        0.4f);
                    randomMoveLight?.FocusLightOnTeam(teamIndex);
                    teamMemberEffects[teamIndex].gameObject.SetActive(true);
                    teamMemberEffects[teamIndex].Play();

                    yield return StartCoroutine(PlayNameRoulette(teamIndex, name));
                    effect?.Play();

                    if (!string.IsNullOrEmpty(teamTexts[teamIndex].text))
                        teamTexts[teamIndex].text += "\n";

                    PlayTextPop(teamTexts[teamIndex].transform);
                    yield return new WaitForSeconds(0.3f);
                }
            }

            teamEffects[teamIndex].gameObject.SetActive(true);
            teamEffects[teamIndex].Play();
            SoundManager.Instance?.Play("Pop");
            teamTexts[teamIndex].transform.DOShakeScale(0.6f, 0.6f, 10, 90);

            yield return new WaitForSeconds(0.5f);
            CameraFocusController.Instance?.ResetFocus(0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        randomMoveLight?.GoLight();
        yield return new WaitForSeconds(0.5f);

        if (CompletedText != null)
            CompletedText.SetActive(true);
        if (ExitButton != null)
            ExitButton.SetActive(true);
    }

    private static void PlayTextPop(Transform target)
    {
        target.DOScale(1.1f, 0.08f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => target.DOScale(1f, 0.08f));
    }

    private IEnumerator PlayNameRoulette(int teamIndex, string finalName)
    {
        float duration = UnityEngine.Random.value + 0.8f;
        float elapsed = 0f;
        const float interval = 0.05f;

        List<string> candidates = teamsToShow.ToList();
        candidates.Add("<color=#FFD700><size=120%>김홍일 강사님</size></color>");
        candidates.Add("<color=#FFD700><size=120%>김경호 강사님</size></color>");

        if (!candidates.Contains(finalName))
            candidates.Add(finalName);

        while (elapsed < duration)
        {
            string randomName = PickWeightedName(candidates);
            teamTexts[teamIndex].text = ReplaceLastLine(teamTexts[teamIndex].text, randomName);
            teamMemberEffects[teamIndex].Play();
            effect?.Play();

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        teamTexts[teamIndex].text = AppendFinalName(teamTexts[teamIndex].text, finalName);
        teamsToShow.Remove(finalName);
    }

    private static string ReplaceLastLine(string original, string newLine)
    {
        List<string> lines = (original ?? string.Empty).Split('\n').ToList();
        if (lines.Count > 0)
            lines[lines.Count - 1] = newLine;
        return string.Join("\n", lines);
    }

    private static string AppendFinalName(string currentText, string finalName)
    {
        List<string> lines = (currentText ?? string.Empty).Split('\n').ToList();
        if (lines.Contains(finalName))
            return currentText;
        lines[lines.Count - 1] = finalName;
        return string.Join("\n", lines);
    }

    private static string PickWeightedName(IReadOnlyList<string> candidates)
    {
        while (true)
        {
            string picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (!picked.Contains("강사님") || UnityEngine.Random.value < 0.05f)
                return picked;
        }
    }
}
