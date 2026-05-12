using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UltimateCutInUI : MonoBehaviour
{
    public static UltimateCutInUI Instance { get; private set; }

    [Header("Refs")]
    public CanvasGroup rootGroup;
    public Image portraitImage;
    public RectTransform portraitRect;
    public RectTransform topBar;
    public RectTransform bottomBar;

    [Header("Timing")]
    public float slideInTime = 0.18f;
    public float holdTime = 1.0f;
    public float slideOutTime = 0.22f;

    [Header("Layout")]
    public Vector2 portraitHiddenPos = new Vector2(900f, 0f);
    public Vector2 portraitShownPos = new Vector2(-260f, 0f);
    public float barHeight = 120f;

    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Play(Sprite portrait, float duration)
    {
        if (portraitImage == null || portraitRect == null || topBar == null || bottomBar == null) return;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayRoutine(portrait, duration));
    }

    private IEnumerator PlayRoutine(Sprite portrait, float duration)
    {
        portraitImage.enabled = portrait != null;
        if (portrait != null) portraitImage.sprite = portrait;

        float safeDuration = Mathf.Max(0f, duration);
        float inTime = Mathf.Max(0.01f, slideInTime);
        float outTime = Mathf.Max(0.01f, slideOutTime);
        float middleTime = Mathf.Max(0f, safeDuration - inTime - outTime);
        if (safeDuration <= 0f) middleTime = holdTime;

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(true);

        SetPose(0f);
        yield return AnimatePose(0f, 1f, inTime);
        yield return new WaitForSecondsRealtime(middleTime);
        yield return AnimatePose(1f, 0f, outTime);

        HideImmediate();
        routine = null;
    }

    private IEnumerator AnimatePose(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetPose(Mathf.SmoothStep(from, to, t));
            yield return null;
        }
        SetPose(to);
    }

    private void SetPose(float t)
    {
        portraitRect.anchoredPosition = Vector2.Lerp(portraitHiddenPos, portraitShownPos, t);
        topBar.anchoredPosition = Vector2.Lerp(new Vector2(0f, barHeight), Vector2.zero, t);
        bottomBar.anchoredPosition = Vector2.Lerp(new Vector2(0f, -barHeight), Vector2.zero, t);
    }

    private void HideImmediate()
    {
        if (rootGroup != null) rootGroup.alpha = 0f;
        if (portraitRect != null) portraitRect.anchoredPosition = portraitHiddenPos;
        if (topBar != null) topBar.anchoredPosition = new Vector2(0f, barHeight);
        if (bottomBar != null) bottomBar.anchoredPosition = new Vector2(0f, -barHeight);
    }
}
