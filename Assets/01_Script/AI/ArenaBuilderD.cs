using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Phase 1-D 학습용 아레나 자동 생성기. (복잡 맵: 구멍 + 플랫폼)
///
/// 레벨:
///   - Level 2 (Holes): Floor를 타일 그리드로 분할 + 외곽 타일 랜덤 제거 (구멍).
///     중앙 safeZoneRadius는 보존되어 NPC 스폰 안전.
///   - Level 3 (Platforms): 일반 Floor + 높이 1.5~2.5m 플랫폼 2~3개. 타겟을 가장 높은 플랫폼 위에 배치.
///     → 점프 없이 도달 불가능하도록 강제.
///   - Mixed: 아레나마다 Level 2/3 랜덤 선택 (level3Ratio로 비율 조정).
///
/// 프리팹/Agent는 Phase 1-BC와 동일 (NPCMovementAgentBC, BehaviorName=MovementAgentBC).
/// Curriculum learning: BC의 .onnx로 --initialize-from 권장.
///
/// 주의:
///   - 플랫폼은 이름에 "Floor" 포함하여 NPCMovementAgentBC.OnCollisionEnter의 페널티 회피.
///   - Level 3 타겟은 TrainingTarget.enabled=false 로 정적 배치 (Respawn 시 공중 이동 방지).
/// </summary>
public class ArenaBuilderD : MonoBehaviour
{
    public enum Difficulty { Level2_Holes, Level3_Platforms, Mixed }

    [Header("아레나 크기")]
    [Tooltip("한 변의 길이(m). NPCMovementAgentBC의 arenaRadius는 이 값의 절반이어야 함.")]
    [SerializeField] private float arenaSize = 20f;

    [Tooltip("낙하 판정 Y 좌표. NPCMovementAgentBC.fallY와 일치 권장.")]
    [SerializeField] private float fallY = -3f;

    [Header("프리팹 (필수)")]
    [Tooltip("NPCMovementAgentBC + RayPerceptionSensorComponent3D + BehaviorParameters 등이 세팅된 NPC 프리팹. Phase 1-BC와 동일.")]
    [SerializeField] private GameObject npcAgentPrefab;

    [Tooltip("TrainingTarget + trigger Collider가 세팅된 타겟 프리팹. 비우면 기본 Sphere 자동 생성.")]
    [SerializeField] private GameObject targetPrefab;

    [Header("병렬 학습")]
    [Range(1, 8)]
    [SerializeField] private int gridSize = 4;

    [SerializeField] private float arenaSpacing = 25f;

    [Header("에이전트")]
    [Tooltip("BehaviorParameters에 설정할 이름. 학습 YAML의 behaviors 키와 일치해야 함.")]
    [SerializeField] private string behaviorName = "MovementAgentBC";

    [Range(1, 20)]
    [SerializeField] private int decisionPeriod = 5;

    [Header("난이도")]
    [SerializeField] private Difficulty difficulty = Difficulty.Mixed;

    [Range(0f, 1f)]
    [Tooltip("Mixed 모드에서 Level 3(플랫폼) 비율. 나머지는 Level 2(구멍).")]
    [SerializeField] private float level3Ratio = 0.5f;

    [Header("Level 2: 구멍")]
    [Tooltip("Floor를 몇 x 몇 타일로 분할할지.")]
    [Range(4, 20)]
    [SerializeField] private int floorTileCount = 10;

    [Tooltip("제거할 타일 개수 (구멍 개수).")]
    [Range(0, 30)]
    [SerializeField] private int holeCount = 8;

    [Tooltip("중앙 안전 존 반경 (타일 단위). 이 범위 타일은 구멍이 생기지 않아 NPC 스폰 안전.")]
    [Range(1, 5)]
    [SerializeField] private int safeZoneRadius = 2;

    [Header("Level 3: 플랫폼")]
    [Tooltip("생성할 플랫폼 개수.")]
    [Range(1, 6)]
    [SerializeField] private int platformCount = 3;

    [Tooltip("플랫폼 높이 범위 (m). jumpForce=7 기준 ~2.5m까지 올라갈 수 있음.")]
    [SerializeField] private Vector2 platformHeightRange = new Vector2(1.5f, 2.5f);

    [Tooltip("플랫폼 크기 범위 (x/z 기준).")]
    [SerializeField] private Vector2 platformSizeRange = new Vector2(2f, 4f);

    [Header("시드")]
    [Tooltip("배치 랜덤 시드. 같은 값이면 같은 배치.")]
    [SerializeField] private int randomSeed = 42;

    [Header("재질 (옵션)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private Material platformMaterial;

#if UNITY_EDITOR
    [ContextMenu("Build Arena")]
    public void BuildArena()
    {
        ClearArena();

        GameObject container = new GameObject("Arenas");
        container.transform.SetParent(transform, false);

        Random.State oldState = Random.state;
        Random.InitState(randomSeed);

        int count = 0;
        int l2 = 0, l3 = 0;
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                Vector3 offset = new Vector3(
                    (i - (gridSize - 1) * 0.5f) * arenaSpacing,
                    0f,
                    (j - (gridSize - 1) * 0.5f) * arenaSpacing
                );

                Difficulty d = difficulty == Difficulty.Mixed
                    ? (Random.value < level3Ratio ? Difficulty.Level3_Platforms : Difficulty.Level2_Holes)
                    : difficulty;

                BuildSingleArena(container.transform, offset, count, d);
                if (d == Difficulty.Level3_Platforms) l3++; else l2++;
                count++;
            }
        }

        Random.state = oldState;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[ArenaBuilderD] {count}개 아레나 생성 완료. Level2(구멍)={l2}, Level3(플랫폼)={l3}");
    }

    [ContextMenu("Clear Arena")]
    public void ClearArena()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private void BuildSingleArena(Transform parent, Vector3 offset, int index, Difficulty d)
    {
        GameObject arenaGo = new GameObject($"Arena_{index:D2}_{d}");
        arenaGo.transform.SetParent(parent, false);
        arenaGo.transform.localPosition = offset;
        Transform arena = arenaGo.transform;

        // 바닥
        if (d == Difficulty.Level2_Holes)
            BuildFloorWithHoles(arena);
        else
            BuildSolidFloor(arena);

        // NPC — Prefab 인스턴스화
        if (npcAgentPrefab == null)
        {
            Debug.LogError("[ArenaBuilderD] NPC Agent Prefab이 설정되지 않았습니다. Inspector에서 할당 후 다시 실행하세요.");
            return;
        }
        GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(npcAgentPrefab, arena);
        npc.name = "NPCAgent";
        npc.transform.localPosition = new Vector3(0f, 1f, 0f);

        var agent = npc.GetComponent<NPCMovementAgentBC>();
        if (agent != null)
        {
            var agentSO = new SerializedObject(agent);
            agentSO.FindProperty("arenaRadius").floatValue = arenaSize * 0.5f;
            agentSO.FindProperty("fallY").floatValue = fallY;
            agentSO.ApplyModifiedPropertiesWithoutUndo();
        }

        var req = npc.GetComponent<DecisionRequester>();
        if (req != null) req.DecisionPeriod = decisionPeriod;

        var behavior = npc.GetComponent<BehaviorParameters>();
        if (behavior != null && behavior.BehaviorName != behaviorName)
        {
            Debug.LogWarning($"[ArenaBuilderD] Prefab의 BehaviorName('{behavior.BehaviorName}')이 " +
                             $"ArenaBuilder 설정('{behaviorName}')과 다릅니다.");
        }

        // 타겟
        GameObject target;
        if (targetPrefab != null)
        {
            target = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, arena);
            target.name = "Target";
        }
        else
        {
            target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "Target";
            target.transform.SetParent(arena, false);
            var tcol = target.GetComponent<Collider>();
            tcol.isTrigger = true;
            if (targetMaterial != null)
                target.GetComponent<MeshRenderer>().sharedMaterial = targetMaterial;
            // Level 2만 TrainingTarget 자동 부여 (랜덤 이동). Level 3은 정적.
            if (d == Difficulty.Level2_Holes)
                target.AddComponent<TrainingTarget>();
        }

        if (d == Difficulty.Level3_Platforms)
        {
            BuildPlatformsAndPlaceTarget(arena, target);
            // Level 3에서는 TrainingTarget 비활성화 (플랫폼 위 고정 유지)
            var tt = target.GetComponent<TrainingTarget>();
            if (tt != null) tt.enabled = false;
        }
        else
        {
            target.transform.localPosition = new Vector3(3f, 0.5f, 3f);
        }

        // NPCMovementAgentBC 의 target / arenaCenter 를 명시적으로 주입.
        // Initialize() 의 자동 탐색은 TrainingTarget 컴포넌트 의존이라 Level 3(정적 타겟)에서 실패함.
        // 여기서 직접 할당해 둬야 OnActionReceived의 null-guard를 통과해 이동/점프가 동작함.
        if (agent != null)
        {
            var agentSO2 = new SerializedObject(agent);
            agentSO2.FindProperty("target").objectReferenceValue = target.transform;
            agentSO2.FindProperty("arenaCenter").objectReferenceValue = arena;
            agentSO2.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private void BuildSolidFloor(Transform arena)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(arena, false);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(arenaSize, 1f, arenaSize);
        if (floorMaterial != null)
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
    }

    private void BuildFloorWithHoles(Transform arena)
    {
        GameObject floorRoot = new GameObject("Floor");
        floorRoot.transform.SetParent(arena, false);

        int N = floorTileCount;
        float tileSize = arenaSize / N;

        // 구멍 위치 선정 — 중앙 safeZone은 절대 비우지 않음
        bool[,] isHole = new bool[N, N];
        // center 계산: N이 짝수면 중앙 2x2 타일이 "중심", 홀수면 1개
        float centerIdx = (N - 1) * 0.5f;
        int holes = 0;
        int attempts = 0;
        while (holes < holeCount && attempts < 500)
        {
            int i = Random.Range(0, N);
            int j = Random.Range(0, N);
            attempts++;
            // safe zone 체크 — 중앙에서 safeZoneRadius 타일 이내면 스킵
            float di = Mathf.Abs(i - centerIdx);
            float dj = Mathf.Abs(j - centerIdx);
            if (di <= safeZoneRadius && dj <= safeZoneRadius) continue;
            if (isHole[i, j]) continue;
            isHole[i, j] = true;
            holes++;
        }

        // 타일 생성 (구멍 위치는 제외)
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                if (isHole[i, j]) continue;
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"FloorTile_{i:D2}_{j:D2}"; // "Floor" 포함 → OnCollisionEnter 페널티 회피
                tile.transform.SetParent(floorRoot.transform, false);
                float x = (i - centerIdx) * tileSize;
                float z = (j - centerIdx) * tileSize;
                tile.transform.localPosition = new Vector3(x, -0.5f, z);
                tile.transform.localScale = new Vector3(tileSize, 1f, tileSize);
                if (floorMaterial != null)
                    tile.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            }
        }
    }

    private void BuildPlatformsAndPlaceTarget(Transform arena, GameObject target)
    {
        // 일반 Floor (Level 3은 바닥 온전)
        // 이미 BuildSolidFloor로 생성됨

        GameObject pRoot = new GameObject("Platforms");
        pRoot.transform.SetParent(arena, false);

        float half = arenaSize * 0.5f - 3f;

        float maxTopY = 0f;
        Vector3 targetLocalPos = new Vector3(3f, 0.5f, 3f); // fallback

        for (int k = 0; k < platformCount; k++)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.transform.SetParent(pRoot.transform, false);
            p.name = $"FloorPlatform_{k:D2}"; // "Floor" 포함 → OnCollisionEnter 페널티 회피
            if (platformMaterial != null)
                p.GetComponent<MeshRenderer>().sharedMaterial = platformMaterial;

            float sx = Random.Range(platformSizeRange.x, platformSizeRange.y);
            float sz = Random.Range(platformSizeRange.x, platformSizeRange.y);
            float sy = Random.Range(platformHeightRange.x, platformHeightRange.y);
            p.transform.localScale = new Vector3(sx, sy, sz);

            // 위치 — 중앙 스폰 존(반경 3m)과 다른 플랫폼 피함
            Vector3 pos;
            int attempts = 0;
            do
            {
                pos = new Vector3(
                    Random.Range(-half, half),
                    sy * 0.5f,
                    Random.Range(-half, half)
                );
                attempts++;
            } while (new Vector2(pos.x, pos.z).magnitude < 3f && attempts < 20);

            p.transform.localPosition = pos;

            // 가장 높은 플랫폼에 타겟 배치
            float topY = sy; // 플랫폼 top의 y는 localPosition.y + sy/2 = sy (pos.y = sy/2라서)
            if (topY > maxTopY)
            {
                maxTopY = topY;
                targetLocalPos = new Vector3(pos.x, topY + 0.5f, pos.z); // target Sphere 반지름 0.5 가정
            }
        }

        target.transform.localPosition = targetLocalPos;
    }
#endif
}
