using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NativeCompletionTextFeedback : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField, Min(0f)] private float secondsPerCharacter = 0.02f;
    [SerializeField, Min(0f)] private float maximumRevealDuration = 1f;
    [SerializeField, Min(0f)] private float colorPulseDuration = 1f;
    [SerializeField] private Color goldColor = new Color(1f, 0.92156863f, 0.015686275f, 1f);
    [SerializeField] private bool playOnStart = true;

    private Coroutine feedbackCoroutine;
    private Color restoreColor;
    private bool hasRestoreColor;

    private void Awake()
    {
        ResolveTarget();
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (!ResolveTarget())
            return;

        StopFeedback(restoreOriginal: true, showAllCharacters: true);

        restoreColor = targetText.color;
        hasRestoreColor = true;

        bool hasText = !string.IsNullOrEmpty(targetText.text);
        targetText.maxVisibleCharacters = hasText ? 0 : int.MaxValue;

        if (!hasText && colorPulseDuration <= 0f)
        {
            RestoreOriginalColor();
            return;
        }

        feedbackCoroutine = StartCoroutine(PlayFeedback(hasText));
    }

    private IEnumerator PlayFeedback(bool hasText)
    {
        int characterCount = 0;

        if (hasText)
        {
            targetText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            characterCount = targetText.textInfo.characterCount;

            if (characterCount == 0)
            {
                // A newly-created UI text may not have valid TMP_TextInfo until
                // the Canvas has advanced once. It remains fully hidden here.
                yield return null;
                targetText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                characterCount = targetText.textInfo.characterCount;

                if (characterCount == 0)
                    characterCount = Mathf.Max(1, targetText.text.Length);
            }
        }

        float revealDuration = Mathf.Min(characterCount * secondsPerCharacter, maximumRevealDuration);
        float totalDuration = Mathf.Max(revealDuration, colorPulseDuration);
        float elapsed = 0f;

        Color pulseColor = goldColor;
        pulseColor.a = restoreColor.a;

        if (totalDuration <= 0f)
        {
            targetText.maxVisibleCharacters = int.MaxValue;
            RestoreOriginalColor();
            feedbackCoroutine = null;
            yield break;
        }

        while (elapsed < totalDuration)
        {
            if (revealDuration > 0f)
            {
                float revealProgress = Mathf.Clamp01(elapsed / revealDuration);
                targetText.maxVisibleCharacters = Mathf.FloorToInt(revealProgress * characterCount);
            }

            if (colorPulseDuration > 0f)
            {
                float colorProgress = Mathf.Clamp01(elapsed / colorPulseDuration);
                float pulse = 1f - Mathf.Abs((colorProgress * 2f) - 1f);
                targetText.color = Color.Lerp(restoreColor, pulseColor, pulse);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        targetText.maxVisibleCharacters = int.MaxValue;
        RestoreOriginalColor();
        feedbackCoroutine = null;
    }

    private void OnDisable()
    {
        StopFeedback(restoreOriginal: true, showAllCharacters: true);
    }

    private bool ResolveTarget()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        return targetText != null;
    }

    private void StopFeedback(bool restoreOriginal, bool showAllCharacters)
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        if (showAllCharacters && targetText != null)
            targetText.maxVisibleCharacters = int.MaxValue;

        if (restoreOriginal)
            RestoreOriginalColor();
    }

    private void RestoreOriginalColor()
    {
        if (hasRestoreColor && targetText != null)
            targetText.color = restoreColor;

        hasRestoreColor = false;
    }
}
