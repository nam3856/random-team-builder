using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D))]
public sealed class NativeLightPulse : MonoBehaviour
{
    [SerializeField] private Light2D targetLight;
    [SerializeField, Min(0f)] private float duration = 1.2f;
    [SerializeField, Min(0f)] private float peakIntensity = 2f;

    private Coroutine pulseCoroutine;
    private float restoreIntensity;
    private bool hasRestoreIntensity;

    private void Awake()
    {
        ResolveTarget();
    }

    public void Play()
    {
        if (!ResolveTarget())
            return;

        StopPulse(restoreOriginal: true);

        restoreIntensity = targetLight.intensity;
        hasRestoreIntensity = true;
        targetLight.intensity = 0f;

        if (duration <= 0f)
        {
            RestoreOriginalIntensity();
            return;
        }

        pulseCoroutine = StartCoroutine(Pulse());
    }

    private IEnumerator Pulse()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f - Mathf.Abs((progress * 2f) - 1f);
            targetLight.intensity = Mathf.Lerp(0f, peakIntensity, pulse);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        targetLight.intensity = 0f;
        RestoreOriginalIntensity();
        pulseCoroutine = null;
    }

    private void OnDisable()
    {
        StopPulse(restoreOriginal: true);
    }

    private bool ResolveTarget()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light2D>();

        return targetLight != null;
    }

    private void StopPulse(bool restoreOriginal)
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (restoreOriginal)
            RestoreOriginalIntensity();
    }

    private void RestoreOriginalIntensity()
    {
        if (hasRestoreIntensity && targetLight != null)
            targetLight.intensity = restoreIntensity;

        hasRestoreIntensity = false;
    }
}
