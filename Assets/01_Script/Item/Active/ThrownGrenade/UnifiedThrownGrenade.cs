using Mirror;
using UnityEngine;

/// <summary>
/// 연막탄(연막 구체) 본체.
/// - 온라인: SyncVar로 파라미터 동기화, 서버에서만 NetworkServer.Destroy.
/// - 오프라인: SyncVar는 단순 필드처럼 동작. OnStartClient/OnStartServer가 호출되지 않으므로 Awake에서 초기화.
///   파괴는 본인이 Destroy.
///
/// 분기는 (1) 초기화 진입점, (2) 수명 만료 시 Destroy 채널 두 군데에서만 처리.
/// </summary>
public class UnifiedThrownGrenade : NetworkBehaviour
{
    [Header("Smoke Appearance")]
    [SyncVar] public Color smokeColor = Color.white;
    [SyncVar] public Color shadowColor = new Color(0.6f, 0.6f, 0.65f, 1f);
    [SyncVar, Range(0f, 1f)] public float smokeAlpha = 0.85f;

    [Header("Size & Shape")]
    [SyncVar] public float maxRadius = 10f;
    [SyncVar] public float expandDuration = 2f;

    [Header("Lifetime")]
    [SyncVar] public float lifetime = 10f;
    [SyncVar] public float fadeDuration = 3f;

    [Header("Noise")]
    [SyncVar] public float noiseScale = 0.4f; // 일렁임의 강도. 너무 크면 구름이 각져 보임
    [SyncVar] public float noiseSpeed = 0.3f; // 일렁이는 속도
    [SyncVar] public float displacement = 0.65f; // 낮을수록 구에 가깝고, 높을수록 불규칙

    [Header("Toon")]
    [SyncVar, Range(2, 6)] public int toonSteps = 2;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material smokeMaterial;

    private float elapsedTime = 0f;
    private Camera mainCam;
    private bool camWasInside = false;

    private bool _initialized;


    // 초기화 진입점
    private void Awake()
    {
        // 오프라인: NetworkBehaviour의 OnStart 콜백이 호출되지 않으므로 여기서 초기화
        if (AuthorityGuard.IsOffline)
        {
            InitVisuals();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!_initialized) InitVisuals();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!_initialized) InitVisuals();
    }

    private void InitVisuals()
    {
        _initialized = true;
        mainCam = Camera.main;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(sphere);

        meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = sphereMesh;

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Shader smokeShader = Shader.Find("Custom/SmokeCloud");
        if (smokeShader == null)
        {
            Debug.LogError("[UnifiedThrownGrenade] Custom/SmokeCloud 셰이더를 찾을 수 없습니다.");
            return;
        }

        smokeMaterial = new Material(smokeShader);
        UpdateMaterialProperties();
        meshRenderer.material = smokeMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        transform.localScale = Vector3.zero;
    }

    private void UpdateMaterialProperties()
    {
        if (smokeMaterial == null) return;

        Color c = smokeColor;
        c.a = smokeAlpha;
        smokeMaterial.SetColor("_Color", c);
        smokeMaterial.SetColor("_ShadowColor", shadowColor);
        smokeMaterial.SetFloat("_NoiseScale", noiseScale);
        smokeMaterial.SetFloat("_NoiseSpeed", noiseSpeed);
        smokeMaterial.SetFloat("_DisplacementStrength", displacement);
        smokeMaterial.SetFloat("_ToonSteps", toonSteps);
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        if (elapsedTime < expandDuration)
        {
            float t = elapsedTime / expandDuration;
            t = 1f - (1f - t) * (1f - t); // EaseOutQuad
            float diameter = Mathf.Lerp(0f, maxRadius * 2f, t);
            transform.localScale = Vector3.one * diameter;
        }
        else if (elapsedTime < lifetime - fadeDuration)
        {
            transform.localScale = Vector3.one * maxRadius * 2f;
        }

        // 소멸 단계
        float fadeStart = lifetime - fadeDuration;
        if (elapsedTime >= fadeStart && elapsedTime < lifetime)
        {
            float fadeT = (elapsedTime - fadeStart) / fadeDuration;
            float alpha = Mathf.Lerp(smokeAlpha, 0f, fadeT);
            Color c = smokeColor;
            c.a = alpha;
            if (smokeMaterial != null)
                smokeMaterial.SetColor("_Color", c);

            float scale = Mathf.Lerp(maxRadius * 2f, maxRadius * 1.5f, fadeT);
            transform.localScale = Vector3.one * scale;
        }
        else if (elapsedTime >= lifetime)
        {
            // 권위 측에서만 파괴: 온라인은 서버, 오프라인은 본인
            bool hasAuthority = AuthorityGuard.IsOffline || isServer;
            if (hasAuthority)
            {
                if (SmokeFogController.Instance != null)
                    SmokeFogController.Instance.ExitSmoke(null); // 레거시 호환

                if (AuthorityGuard.IsOffline) Destroy(gameObject);
                else NetworkServer.Destroy(gameObject);
            }
            return;
        }

        // 카메라 내부 감지 (클라이언트 로컬)
        if (mainCam != null && SmokeFogController.Instance != null)
        {
            float dist = Vector3.Distance(mainCam.transform.position, transform.position);
            float effectiveRadius = (transform.localScale.x / 2f) * 0.85f;
            bool isInside = dist < effectiveRadius;

            if (isInside && !camWasInside)
            {
                SmokeFogController.Instance.EnterSmoke(null);
                camWasInside = true;
            }
            else if (!isInside && camWasInside)
            {
                SmokeFogController.Instance.ExitSmoke(null);
                camWasInside = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (smokeMaterial != null)
            Destroy(smokeMaterial);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0.3f);
        Gizmos.DrawWireSphere(transform.position, maxRadius);
    }
#endif
}