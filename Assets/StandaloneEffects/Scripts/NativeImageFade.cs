using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NativeImageFade : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField, Min(0f)] private float duration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float endAlpha = 1f;
    [SerializeField] private bool playOnStart = true;

    private Coroutine fadeCoroutine;

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

        StopFade(applyEndAlpha: false);
        SetAlpha(startAlpha);

        if (duration <= 0f)
        {
            SetAlpha(endAlpha);
            return;
        }

        fadeCoroutine = StartCoroutine(Fade());
    }

    private IEnumerator Fade()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetAlpha(endAlpha);
        fadeCoroutine = null;
    }

    private void OnDisable()
    {
        StopFade(applyEndAlpha: true);
    }

    private bool ResolveTarget()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        return targetImage != null;
    }

    private void StopFade(bool applyEndAlpha)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (applyEndAlpha && targetImage != null)
            SetAlpha(endAlpha);
    }

    private void SetAlpha(float alpha)
    {
        Color color = targetImage.color;
        color.a = alpha;
        targetImage.color = color;
    }
}
