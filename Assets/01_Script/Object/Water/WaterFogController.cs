using UnityEngine;
using System.Collections.Generic;



// 물 내부 진입 시 Global Shader 변수를 설정하여 WaterFogRenderFeature가 수중 안개를 그리도록 함.

public class WaterFogController : MonoBehaviour
{
    [Header("Fog Settings")]
    public Color fogColor = new Color(0.1f, 0.35f, 0.5f, 1f);
    [Range(0f, 1f)]
    public float maxDensity = 0.8f;
    public float fadeSpeed = 4f;
    public float noiseScale = 2f;
    public float noiseSpeed = 0.15f;

    [Header("Caustic Settings")]
    public float causticScale = 5f;
    public float causticSpeed = 0.4f;
    [Range(0f, 1f)]
    public float causticIntensity = 0.4f;

    private float currentDensity = 0f;
    private bool isInsideWater = false;
    private Color currentFogColor;

    private HashSet<WaterVolume> overlappingWaters = new HashSet<WaterVolume>();

    private static WaterFogController _instance;
    public static WaterFogController Instance => _instance;

    void Awake()
    {
        _instance = this;
        Shader.SetGlobalFloat("_WaterFogDensity", 0f);
    }

    public void EnterWater(WaterVolume water)
    {
        overlappingWaters.Add(water);
        isInsideWater = true;
        currentFogColor = water.waterColor;
        currentFogColor.a = 1f;
    }

    public void ExitWater(WaterVolume water)
    {
        overlappingWaters.Remove(water);
        if (overlappingWaters.Count == 0)
            isInsideWater = false;
    }

    void Update()
    {
        float target = isInsideWater ? maxDensity : 0f;
        currentDensity = Mathf.MoveTowards(currentDensity, target, fadeSpeed * Time.deltaTime);

        Shader.SetGlobalFloat("_WaterFogDensity", currentDensity);
        Shader.SetGlobalColor("_WaterFogColor", currentFogColor);
        Shader.SetGlobalFloat("_WaterFogNoiseScale", noiseScale);
        Shader.SetGlobalFloat("_WaterFogNoiseSpeed", noiseSpeed);
        Shader.SetGlobalFloat("_WaterCausticScale", causticScale);
        Shader.SetGlobalFloat("_WaterCausticSpeed", causticSpeed);
        Shader.SetGlobalFloat("_WaterCausticIntensity", causticIntensity);
    }

    void OnDestroy()
    {
        Shader.SetGlobalFloat("_WaterFogDensity", 0f);
    }
}
