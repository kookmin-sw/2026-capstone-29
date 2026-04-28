using UnityEngine;


// 물 오브젝트에 부착. 메쉬를 생성하고 카메라 진입을 감지.
public class WaterVolume : MonoBehaviour
{
    [Header("Water Appearance")]
    public Color waterColor = new Color(0.15f, 0.55f, 0.7f, 0.85f);
    public Color shallowColor = new Color(0.3f, 0.75f, 0.8f, 0.85f);

    [Header("Size")]
    public Vector3 size = new Vector3(10f, 3f, 10f);

    [Header("Wave")]
    public float waveSpeed = 1f;
    public float waveScale = 1.5f;
    public float waveHeight = 0.15f;

    [Header("Toon")]
    [Range(2, 6)]
    public int toonSteps = 3;

    [Header("Detection")]
    [Tooltip("수면 아래 이 비율 지점부터 수중 판정")]
    [Range(0f, 1f)]
    public float submersionThreshold = 0.3f;


    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material waterMaterial;
    private Camera mainCam;
    private bool camWasInside = false;

    void Start()
    {
        mainCam = Camera.main;


        Mesh waterMesh = CreateSubdividedPlane(32, 32);
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = waterMesh;

        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Shader waterShader = Shader.Find("Custom/WaterSurface");
        if (waterShader == null)
        {
            Debug.LogError("WaterVolume: Custom/WaterSurface 셰이더를 찾을 수 없습니다.");
            return;
        }

        waterMaterial = new Material(waterShader);
        UpdateMaterialProperties();
        meshRenderer.material = waterMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // 스케일 적용
        transform.localScale = size;
    }

    void UpdateMaterialProperties()
    {
        if (waterMaterial == null) return;
        waterMaterial.SetColor("_Color", waterColor);
        waterMaterial.SetColor("_ShallowColor", shallowColor);
        waterMaterial.SetFloat("_WaveSpeed", waveSpeed);
        waterMaterial.SetFloat("_WaveScale", waveScale);
        waterMaterial.SetFloat("_WaveHeight", waveHeight);
        waterMaterial.SetFloat("_ToonSteps", toonSteps);
    }

    void Update()
    {
        if (mainCam == null || WaterFogController.Instance == null) return;

        Vector3 camPos = mainCam.transform.position;
        Vector3 waterCenter = transform.position;

        // 수면 높이: 오브젝트 위치 + 사이즈 절반 (상단이 수면)
        float surfaceY = waterCenter.y + (size.y * 0.5f);
        // 수중 판정선: 수면에서 threshold만큼 아래
        float submergeY = surfaceY - (size.y * submersionThreshold);

        // XZ 범위 체크
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        bool inXZ = camPos.x > waterCenter.x - halfX &&
                    camPos.x < waterCenter.x + halfX &&
                    camPos.z > waterCenter.z - halfZ &&
                    camPos.z < waterCenter.z + halfZ;

        bool isInside = inXZ && camPos.y < submergeY;

        if (isInside && !camWasInside)
        {
            WaterFogController.Instance.EnterWater(this);
            camWasInside = true;
        }
        else if (!isInside && camWasInside)
        {
            WaterFogController.Instance.ExitWater(this);
            camWasInside = false;
        }
    }

    void OnDestroy()
    {
        if (waterMaterial != null)
            Destroy(waterMaterial);
    }


    // 세분화된 평면 메쉬 생성 (파도 변형에 충분한 정점 수 확보)
    Mesh CreateSubdividedPlane(int resX, int resZ)
    {
        Mesh mesh = new Mesh();
        mesh.name = "WaterPlane";

        int vertCount = (resX + 1) * (resZ + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals  = new Vector3[vertCount];
        Vector2[] uvs      = new Vector2[vertCount];

        for (int z = 0; z <= resZ; z++)
        {
            for (int x = 0; x <= resX; x++)
            {
                int i = z * (resX + 1) + x;
                float px = (float)x / resX - 0.5f;
                float pz = (float)z / resZ - 0.5f;
                vertices[i] = new Vector3(px, 0f, pz);
                normals[i]  = Vector3.up;
                uvs[i]      = new Vector2((float)x / resX, (float)z / resZ);
            }
        }

        int[] triangles = new int[resX * resZ * 6];
        int t = 0;
        for (int z = 0; z < resZ; z++)
        {
            for (int x = 0; x < resX; x++)
            {
                int i = z * (resX + 1) + x;
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

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(waterColor.r, waterColor.g, waterColor.b, 0.2f);
        Gizmos.DrawWireCube(transform.position, size);

        // 수면 표시
        float surfaceY = transform.position.y + (size.y * 0.5f);
        Gizmos.color = new Color(0.2f, 0.7f, 0.9f, 0.5f);
        Vector3 surfaceCenter = new Vector3(transform.position.x, surfaceY, transform.position.z);
        Gizmos.DrawWireCube(surfaceCenter, new Vector3(size.x, 0.02f, size.z));
    }
#endif
}
