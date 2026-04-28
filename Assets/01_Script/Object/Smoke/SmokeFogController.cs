using UnityEngine;
using System.Collections.Generic;


//메인 카메라에 부착, 연막 내부 진입 시 Global Shader 변수를 설정하여 SmokeFogRenderFeature가 안개를 그림.

public class SmokeFogController : MonoBehaviour
{
    [Header("Fog Settings")]
    public Color fogColor = new Color(0.85f, 0.85f, 0.88f, 1f);
    [Range(0f, 1f)]
    public float maxDensity = 0.85f;
    public float fadeSpeed = 3f;
    public float noiseScale = 3f;
    public float noiseSpeed = 0.2f;

    private float currentDensity = 0f;
    private bool isInsideSmoke = false;
    private Color currentFogColor;

    private HashSet<SmokeGrenade> overlappingSmokes = new HashSet<SmokeGrenade>();

    private static SmokeFogController _instance;
    public static SmokeFogController Instance => _instance;

    void Awake()
    {
        _instance = this;
        // 초기화
        Shader.SetGlobalFloat("_SmokeFogDensity", 0f);
    }

    public void EnterSmoke(SmokeGrenade smoke)
    {
        if (smoke == null)
        {
            Debug.Log("연기 오브젝트 없음.");
            return;
        }
        overlappingSmokes.Add(smoke);
        isInsideSmoke = true;
        currentFogColor = smoke.smokeColor;
        currentFogColor.a = 1f;
    }

    public void ExitSmoke(SmokeGrenade smoke)
    {
        overlappingSmokes.Remove(smoke);
        if (overlappingSmokes.Count == 0)
            isInsideSmoke = false;
    }

    void Update()
    {

        float target = isInsideSmoke ? maxDensity : 0f;
        currentDensity = Mathf.MoveTowards(currentDensity, target, fadeSpeed * Time.deltaTime);



        // Global shader 변수 설정 (RenderFeature에서 읽음)
        Shader.SetGlobalFloat("_SmokeFogDensity", currentDensity);
        Shader.SetGlobalColor("_SmokeFogColor", currentFogColor);
        Shader.SetGlobalFloat("_SmokeFogNoiseScale", noiseScale);
        Shader.SetGlobalFloat("_SmokeFogNoiseSpeed", noiseSpeed);

    }

    void OnDestroy()
    {
        Shader.SetGlobalFloat("_SmokeFogDensity", 0f);
    }
}
