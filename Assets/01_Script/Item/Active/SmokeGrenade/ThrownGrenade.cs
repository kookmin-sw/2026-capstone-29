using Mirror;
using UnityEngine;


/// SmokeGrenade의 Mirror NetworkBehaviour 버전.
public class ThrownGrenade : NetworkBehaviour
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
    [SyncVar] public float noiseScale = 0.4f; // 일렁임의 강도 조절. 근데 너무 크게 잡으면 구름이 각져 보이니 잘 조정할 것
    [SyncVar] public float noiseSpeed = 0.3f; // 일렁이는 속도. 
    [SyncVar] public float displacement = 0.65f; //낮을수록 오브젝트가 구에 가까워지고, 커질수록 불규칙적이게 됨. 

    [Header("Toon")]
    [SyncVar, Range(2, 6)] public int toonSteps = 2;


    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material smokeMaterial;

    private float elapsedTime = 0f;
    private Camera mainCam;
    private bool camWasInside = false;


    public override void OnStartClient()
    {
        base.OnStartClient();
        InitVisuals();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (meshRenderer == null) InitVisuals();
    }

    private void InitVisuals()
    {
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
            Debug.LogError("[NetworkSmokeGrenade] Custom/SmokeCloud 셰이더를 찾을 수 없습니다.");
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


    void Update()
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

        // === 소멸 단계 ===
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
            // 서버에서만 파괴
            if (isServer)
            {
                if (SmokeFogController.Instance != null)
                    SmokeFogController.Instance.ExitSmoke(null); // 레거시 호환
                NetworkServer.Destroy(gameObject);
            }
            return;
        }

        // === 카메라 내부 감지 (클라이언트 로컬) ===
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

    void OnDestroy()
    {
        if (smokeMaterial != null)
            Destroy(smokeMaterial);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0.3f);
        Gizmos.DrawWireSphere(transform.position, maxRadius);
    }
#endif
}