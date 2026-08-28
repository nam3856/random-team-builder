using UnityEngine;

public static class ResponsiveUiLayout
{
    public const float NarrowAspectThreshold = 1.15f;
    public const float DefaultSettingsScale = 1.5f;

    public static int GetColumnCount(int teamCount, float aspect)
    {
        int safeTeamCount = Mathf.Max(1, teamCount);
        if (aspect < NarrowAspectThreshold)
            return Mathf.Min(2, safeTeamCount);

        return safeTeamCount == 2 || safeTeamCount == 4
            ? 2
            : Mathf.Min(3, safeTeamCount);
    }

    public static int GetRowCount(int teamCount, int columnCount)
    {
        return Mathf.CeilToInt(Mathf.Max(1, teamCount) / (float)Mathf.Max(1, columnCount));
    }

    public static Rect GetSafeWorldRect(
        int screenWidth,
        int screenHeight,
        Rect safeArea,
        Vector2 cameraCenter,
        float orthographicSize)
    {
        float width = Mathf.Max(1, screenWidth);
        float height = Mathf.Max(1, screenHeight);
        Rect effectiveSafeArea = SanitizeSafeArea(width, height, safeArea);
        float worldHeight = Mathf.Max(0.01f, orthographicSize * 2f);
        float worldWidth = worldHeight * width / height;

        float xMin = cameraCenter.x + (effectiveSafeArea.xMin / width - 0.5f) * worldWidth;
        float xMax = cameraCenter.x + (effectiveSafeArea.xMax / width - 0.5f) * worldWidth;
        float yMin = cameraCenter.y + (effectiveSafeArea.yMin / height - 0.5f) * worldHeight;
        float yMax = cameraCenter.y + (effectiveSafeArea.yMax / height - 0.5f) * worldHeight;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    public static Rect GetTeamWorldRect(Rect safeWorldRect)
    {
        float horizontalMargin = safeWorldRect.width * 0.045f;
        float topMargin = safeWorldRect.height * 0.045f;
        float completionReserve = Mathf.Clamp(safeWorldRect.height * 0.22f, 1.35f, 2.2f);
        float bottomGap = Mathf.Min(0.2f, safeWorldRect.height * 0.02f);

        return new Rect(
            safeWorldRect.xMin + horizontalMargin,
            safeWorldRect.yMin + completionReserve + bottomGap,
            Mathf.Max(0.01f, safeWorldRect.width - horizontalMargin * 2f),
            Mathf.Max(0.01f, safeWorldRect.height - completionReserve - bottomGap - topMargin));
    }

    public static Rect GetCompletionWorldRect(Rect safeWorldRect)
    {
        float horizontalMargin = safeWorldRect.width * 0.06f;
        float bottomMargin = Mathf.Min(0.22f, safeWorldRect.height * 0.025f);
        float reservedHeight = Mathf.Clamp(safeWorldRect.height * 0.22f, 1.35f, 2.2f);

        return new Rect(
            safeWorldRect.xMin + horizontalMargin,
            safeWorldRect.yMin + bottomMargin,
            Mathf.Max(0.01f, safeWorldRect.width - horizontalMargin * 2f),
            Mathf.Max(0.01f, reservedHeight - bottomMargin * 2f));
    }

    public static float GetUniformFitScale(Vector2 contentSize, Vector2 availableSize, float maximumScale = 1f)
    {
        float widthScale = availableSize.x / Mathf.Max(0.01f, contentSize.x);
        float heightScale = availableSize.y / Mathf.Max(0.01f, contentSize.y);
        return Mathf.Clamp(Mathf.Min(maximumScale, widthScale, heightScale), 0.05f, maximumScale);
    }

    public static float GetSettingsPanelScale(
        Vector2 logicalCanvasSize,
        Rect normalizedSafeArea,
        Vector2 contentSize)
    {
        float safeWidth = logicalCanvasSize.x * Mathf.Clamp01(normalizedSafeArea.width);
        float safeHeight = logicalCanvasSize.y * Mathf.Clamp01(normalizedSafeArea.height);
        float widthScale = safeWidth * 0.92f / Mathf.Max(1f, contentSize.x);
        float heightScale = safeHeight * 0.88f / Mathf.Max(1f, contentSize.y);
        return Mathf.Clamp(Mathf.Min(DefaultSettingsScale, widthScale, heightScale), 0.45f, DefaultSettingsScale);
    }

    public static float GetFocusOrthographicSize(float defaultSize, float aspect)
    {
        return Mathf.Max(defaultSize, 2f / Mathf.Max(0.1f, aspect));
    }

    public static Rect NormalizeSafeArea(int screenWidth, int screenHeight, Rect safeArea)
    {
        float width = Mathf.Max(1, screenWidth);
        float height = Mathf.Max(1, screenHeight);
        Rect effectiveSafeArea = SanitizeSafeArea(width, height, safeArea);
        return new Rect(
            effectiveSafeArea.x / width,
            effectiveSafeArea.y / height,
            effectiveSafeArea.width / width,
            effectiveSafeArea.height / height);
    }

    private static Rect SanitizeSafeArea(float width, float height, Rect safeArea)
    {
        if (safeArea.width <= 0f || safeArea.height <= 0f)
            return new Rect(0f, 0f, width, height);

        float xMin = Mathf.Clamp(safeArea.xMin, 0f, width);
        float xMax = Mathf.Clamp(safeArea.xMax, xMin, width);
        float yMin = Mathf.Clamp(safeArea.yMin, 0f, height);
        float yMax = Mathf.Clamp(safeArea.yMax, yMin, height);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
