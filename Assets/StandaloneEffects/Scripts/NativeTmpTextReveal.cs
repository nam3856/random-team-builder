using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NativeTmpTextReveal : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField, Min(0f)] private float secondsPerCharacter = 0.02f;
    [SerializeField, Min(0f)] private float maximumDuration = 1f;
    [SerializeField] private bool playOnStart = true;

    private Coroutine revealCoroutine;

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

        StopReveal(showAllCharacters: true);

        if (string.IsNullOrEmpty(targetText.text))
        {
            targetText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        // Keep the text hidden immediately, even before a newly-created UI text
        // has produced its first TMP_TextInfo during the canvas update.
        targetText.maxVisibleCharacters = 0;
        revealCoroutine = StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        targetText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

        int characterCount = targetText.textInfo.characterCount;

        if (characterCount == 0)
        {
            // TextMeshProUGUI can report zero until the Canvas has completed one
            // update. Preserve the hidden state and query again next frame.
            yield return null;
            targetText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            characterCount = targetText.textInfo.characterCount;

            if (characterCount == 0)
                characterCount = Mathf.Max(1, targetText.text.Length);
        }

        float duration = Mathf.Min(characterCount * secondsPerCharacter, maximumDuration);

        if (characterCount == 0 || duration <= 0f)
        {
            targetText.maxVisibleCharacters = int.MaxValue;
            revealCoroutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            targetText.maxVisibleCharacters = Mathf.FloorToInt(progress * characterCount);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        targetText.maxVisibleCharacters = int.MaxValue;
        revealCoroutine = null;
    }

    private void OnDisable()
    {
        StopReveal(showAllCharacters: true);
    }

    private bool ResolveTarget()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        return targetText != null;
    }

    private void StopReveal(bool showAllCharacters)
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        if (showAllCharacters && targetText != null)
            targetText.maxVisibleCharacters = int.MaxValue;
    }
}
