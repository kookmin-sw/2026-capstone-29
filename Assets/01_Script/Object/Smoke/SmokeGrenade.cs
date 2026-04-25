using UnityEngine;

// 연막 오브젝트에 자동 부착.

public class SmokeGrenade : MonoBehaviour
{
    [Header("Smoke Appearance")]
    public Color smokeColor = Color.white;
    public Color shadowColor = new Color(0.6f, 0.6f, 0.65f, 1f);
    [Range(0f, 1f)]
    public float smokeAlpha = 0.85f;

    [Header("Size & Shape")]
    public float maxRadius = 10f;
    public float expandDuration = 2f;

    [Header("Lifetime")]
    public float lifetime = 10f; 
    public float fadeDuration = 3f;

    [Header("Noise")]
    public float noiseScale = 0.4f; // 일렁임의 강도 조절. 근데 너무 크게 잡으면 구름이 각져 보이니 잘 조정할 것
    public float noiseSpeed = 0.3f; // 일렁이는 속도. 
    public float displacement = 0.65f; //낮을수록 오브젝트가 구에 가까워지고, 커질수록 불규칙적이게 됨. 

    [Header("Toon")]
    [Range(2, 6)]
    public int toonSteps = 4;


    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material smokeMaterial;

    private float elapsedTime = 0f;
    private Camera mainCam;
    private bool camWasInside = false;

    void Start()
    {
        mainCam = Camera.main;

        // Sphere 메쉬 빌려오기
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        // 기본 콜라이더 제거
        DestroyImmediate(sphere);

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = sphereMesh;

        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // 머티리얼
        Shader smokeShader = Shader.Find("Custom/SmokeCloud");
        if (smokeShader == null)
        {
            Debug.LogError("SmokeGrenade: Custom/SmokeCloud 셰이더 감지 불거.");
            return;
        }

        smokeMaterial = new Material(smokeShader);
        UpdateMaterialProperties();
        meshRenderer.material = smokeMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        transform.localScale = Vector3.zero;
    }

    void UpdateMaterialProperties()
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
            if (SmokeFogController.Instance != null)
                SmokeFogController.Instance.ExitSmoke(this);
            Destroy(gameObject);
            return;
        }

        // 카메라 내부 감지
        if (mainCam != null && SmokeFogController.Instance != null)
        {
            float dist = Vector3.Distance(mainCam.transform.position, transform.position);
            float effectiveRadius = (transform.localScale.x / 2f) * 0.85f;

            bool isInside = dist < effectiveRadius;

            if (isInside && !camWasInside)
            {

                SmokeFogController.Instance.EnterSmoke(this);
                camWasInside = true;
            }
            else if (!isInside && camWasInside)
            {
                SmokeFogController.Instance.ExitSmoke(this);
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
