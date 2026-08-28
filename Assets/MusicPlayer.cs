using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip musicClip;

    private void Awake()
    {
        ResolveAudioSource();
    }

    public void Play()
    {
        ResolveAudioSource();

        if (audioSource == null)
            return;

        if (musicClip != null)
            audioSource.clip = musicClip;

        if (audioSource.clip == null)
        {
            audioSource.Stop();
            return;
        }

        audioSource.Play();
    }

    private void ResolveAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }
}
