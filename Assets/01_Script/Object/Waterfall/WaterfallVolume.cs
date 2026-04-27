using UnityEngine;

//폭포 오브젝트에 부착하여 사용.
public class WaterfallVolume : MonoBehaviour
{
    [Header("Size")]
    public float width = 3f;
    public float height = 8f;

    [Header("Appearance")]
    public Color waterColor = new Color(0.2f, 0.5f, 0.7f, 0.9f);
    public Color deepColor = new Color(0.08f, 0.25f, 0.45f, 0.95f);
    [Range(2, 6)]
    public int toonSteps = 3;

    [Header("Flow")]
    public float flowSpeed = 2f;
    public float flowNoiseScale = 3f;
    [Range(0f, 0.3f)]
    public float flowDistortion = 0.1f;

    [Header("Streaks")]
    public float streakScale = 8f;
    [Range(1f, 20f)]
    public float streakSharpness = 6f;
    public Color streakColor = new Color(0.6f, 0.85f, 0.95f, 1f);

    [Header("Mesh Resolution")]
    [Range(4, 64)]
    public int subdivisions = 32;
     
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material waterfallMaterial;

    void Start()
    { 
        Mesh mesh = CreateSubdividedQuad(subdivisions, subdivisions);
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Shader waterfallShader = Shader.Find("Custom/Waterfall");
        if (waterfallShader == null)
        {
            Debug.LogError("WaterfallVolume: Custom/Waterfall 셰이더를 찾을 수 없습니다.");
            return;
        }

        waterfallMaterial = new Material(waterfallShader);
        UpdateMaterialProperties();
        meshRenderer.material = waterfallMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;


        transform.localScale = new Vector3(width, height, 1f);
    }

    void UpdateMaterialProperties()
    {
        if (waterfallMaterial == null) return;
        waterfallMaterial.SetColor("_Color", waterColor);
        waterfallMaterial.SetColor("_Color2", deepColor);
        waterfallMaterial.SetFloat("_ToonSteps", toonSteps);
        waterfallMaterial.SetFloat("_FlowSpeed", flowSpeed);
        waterfallMaterial.SetFloat("_FlowNoiseScale", flowNoiseScale);
        waterfallMaterial.SetFloat("_FlowDistortion", flowDistortion);
        waterfallMaterial.SetFloat("_StreakScale", streakScale);
        waterfallMaterial.SetFloat("_StreakSharpness", streakSharpness);
        waterfallMaterial.SetColor("_StreakColor", streakColor);
    }

   

    Mesh CreateSubdividedQuad(int resX, int resY)
    {
        Mesh mesh = new Mesh();
        mesh.name = "WaterfallQuad";

        int vertCount = (resX + 1) * (resY + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals  = new Vector3[vertCount];
        Vector2[] uvs      = new Vector2[vertCount];

        for (int y = 0; y <= resY; y++)
        {
            for (int x = 0; x <= resX; x++)
            {
                int i = y * (resX + 1) + x;
                float px = (float)x / resX - 0.5f;
                float py = (float)y / resY - 0.5f;
                vertices[i] = new Vector3(px, py, 0f);
                normals[i]  = -Vector3.forward; // 앞면이 Z- 방향
                uvs[i]      = new Vector2((float)x / resX, (float)y / resY);
            }
        }

        int[] triangles = new int[resX * resY * 6];
        int t = 0;
        for (int y = 0; y < resY; y++)
        {
            for (int x = 0; x < resX; x++)
            {
                int i = y * (resX + 1) + x;
                triangles[t++] = i;
                triangles[t++] = i + resX + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + resX + 1;
                triangles[t++] = i + resX + 2;
            }
        }

        mesh.vertices  = vertices;
        mesh.normals   = normals;
        mesh.uv        = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    void OnDestroy()
    {
        if (waterfallMaterial != null)
            Destroy(waterfallMaterial);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(waterColor.r, waterColor.g, waterColor.b, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
         
        Vector3 gizmoSize = new Vector3(1f, 1f, 0.05f);
        if (!Application.isPlaying)
            gizmoSize = new Vector3(width, height, 0.05f);

        Gizmos.DrawWireCube(Vector3.zero, gizmoSize);
         
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
        float arrowY = Application.isPlaying ? 0.3f : height * 0.3f;
        float arrowLen = Application.isPlaying ? 0.3f : height * 0.3f;
        Gizmos.DrawLine(new Vector3(0, arrowY, 0), new Vector3(0, arrowY - arrowLen, 0));
        Gizmos.DrawLine(new Vector3(0, arrowY - arrowLen, 0), new Vector3(0.05f, arrowY - arrowLen + 0.1f, 0));
        Gizmos.DrawLine(new Vector3(0, arrowY - arrowLen, 0), new Vector3(-0.05f, arrowY - arrowLen + 0.1f, 0));
    }
#endif
}
