using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip titleBGM;
    [SerializeField] private AudioClip charSelectBGM;
    [SerializeField] private AudioClip inGameBGM;

    [SerializeField] private float fadeTime = 1.0f;
    [SerializeField] private float maxVolume = 0.2f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource.loop = true;
        audioSource.volume = maxVolume;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "TitleScene":
                PlayBGM(titleBGM);
                break;

            case "CharSelectScene":
                PlayBGM(charSelectBGM);
                break;

            case "GameScene":
                PlayBGM(inGameBGM);
                break;
        }
    }

    private void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource.clip == clip && audioSource.isPlaying)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeBGM(clip));
    }

    private IEnumerator FadeBGM(AudioClip nextClip)
    {
        float startVolume = audioSource.volume;

        // 페이드 아웃
        while (audioSource.volume > 0f)
        {
            audioSource.volume -= startVolume * Time.deltaTime / fadeTime;
            yield return null;
        }

        audioSource.Stop();
        audioSource.clip = nextClip;
        audioSource.Play();

        // 페이드 인
        while (audioSource.volume < maxVolume)
        {
            audioSource.volume += maxVolume * Time.deltaTime / fadeTime;
            yield return null;
        }

        audioSource.volume = maxVolume;
        fadeCoroutine = null;
    }
}
