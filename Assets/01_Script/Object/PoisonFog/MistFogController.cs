using UnityEngine;

/// <summary>
/// 맵 전역 안개(미스트) 제어 컨트롤러.
/// 메인 카메라에 부착. 아이템 사용 시 Global Shader 변수를 설정하여
/// MistFogRenderFeature가 안개를 그림.
///
/// 사용법:
///   MistFogController.Instance.ActivateMist();      // 안개 시작
///   MistFogController.Instance.ActivateMist(20f);    // 20초 후 자동 해제
///   MistFogController.Instance.DeactivateMist();     // 안개 해제
/// </summary>
public class MistFogController : MonoBehaviour
{
    [Header("Mist Appearance")]
    public Color mistColor = new Color(0.75f, 0.82f, 0.88f, 1f);

    [Header("Density")]
    [Range(0f, 1f)]
    public float maxDensity = 0.45f;

    [Header("Fade Timing")]
    public float fadeInSpeed = 0.15f;   // density per second (느리게 깔림)
    public float fadeOutSpeed = 0.25f;

    [Header("Noise")]
    public float noiseScale = 3.0f;
    public float noiseSpeed = 0.15f;
    public float layerScale2 = 0.6f;

    [Range(0.3f, 2.0f)]
    public float softness = 1.0f;

    private float currentDensity = 0f;
    private bool isActive = false;
    private float autoDeactivateTimer = -1f;

    private static MistFogController _instance;
    public static MistFogController Instance => _instance;

    void Awake()
    {
        _instance = this;
        Shader.SetGlobalFloat("_MistDensity", 0f);
    }

    /// <summary>
    /// 안개를 서서히 활성화합니다.
    /// </summary>
    /// <param name="autoDuration">
    /// 0보다 크면 해당 초 후 자동으로 DeactivateMist() 호출.
    /// </param>
    public void ActivateMist(float autoDuration = -1f)
    {
        isActive = true;

        if (autoDuration > 0f)
            autoDeactivateTimer = autoDuration;
        else
            autoDeactivateTimer = -1f;
    }

    /// <summary>
    /// 안개를 서서히 해제합니다.
    /// </summary>
    public void DeactivateMist()
    {
        isActive = false;
        autoDeactivateTimer = -1f;
    }

    /// <summary>
    /// 즉시 안개를 제거합니다 (페이드 없음).
    /// </summary>
    public void ClearMistImmediate()
    {
        isActive = false;
        currentDensity = 0f;
        autoDeactivateTimer = -1f;
        Shader.SetGlobalFloat("_MistDensity", 0f);
    }

    void Update()
    {
        // Auto deactivate timer
        if (autoDeactivateTimer > 0f)
        {
            autoDeactivateTimer -= Time.deltaTime;
            if (autoDeactivateTimer <= 0f)
                DeactivateMist();
        }

        // Fade toward target
        float target = isActive ? maxDensity : 0f;
        float speed = isActive ? fadeInSpeed : fadeOutSpeed;
        currentDensity = Mathf.MoveTowards(currentDensity, target, speed * Time.deltaTime);

        // Global shader 변수 설정 (RenderFeature에서 읽음)
        Shader.SetGlobalFloat("_MistDensity", currentDensity);
        Shader.SetGlobalColor("_MistColor", mistColor);
        Shader.SetGlobalFloat("_MistNoiseScale", noiseScale);
        Shader.SetGlobalFloat("_MistNoiseSpeed", noiseSpeed);
        Shader.SetGlobalFloat("_MistLayerScale2", layerScale2);
        Shader.SetGlobalFloat("_MistSoftness", softness);
    }

    void OnDestroy()
    {
        Shader.SetGlobalFloat("_MistDensity", 0f);
    }
}