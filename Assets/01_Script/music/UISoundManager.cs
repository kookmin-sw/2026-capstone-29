using UnityEngine;

public class UISoundManager : MonoBehaviour
{
    public static UISoundManager Instance;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayHoverSound()
    {
        PlaySound(hoverSound);
    }

    public void PlayClickSound()
    {
        PlaySound(clickSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;

        audioSource.PlayOneShot(clip);
    }
}
