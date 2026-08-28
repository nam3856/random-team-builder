using System;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] popSfx;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Play(string name)
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null || !audioSource.isActiveAndEnabled)
            return;

        int clipIndex = string.Equals(name, "Pop", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        if (popSfx == null || clipIndex >= popSfx.Length || popSfx[clipIndex] == null)
            return;

        audioSource.PlayOneShot(popSfx[clipIndex]);
    }
}
