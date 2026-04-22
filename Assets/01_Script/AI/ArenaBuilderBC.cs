using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Phase 1-BC 학습용 아레나 자동 생성기.
///
/// 구성: Floor + NPC(Prefab) + Target + 랜덤 장애물 박스 N개.
///
/// 사용법:
/// 1. 빈 씬에 빈 GameObject → 이 스크립트 부착
/// 2. Inspector에서 NPC Agent Prefab / Target Prefab 할당
/// 3. "Build Arena" ContextMenu 실행
///
/// difficulty 확장 예정:
///   - 현재(레벨1): 박스 장애물만
///   - 레벨2: Floor에 구멍 (일부 Floor 큐브 제거)
///   - 레벨3: 점프로 올라가야 타겟 도달 가능한 계단식 배치
/// </summary>
public class ArenaBuilderBC : MonoBehaviour
{
    [Header("아레나 크기")]
    [Tooltip("한 변의 길이(m). NPCMovementAgentBC의 arenaRadius는 이 값의 절반이어야 함.")]
    [SerializeField] private float arenaSize = 20f;

    [Tooltip("낙하 판정 Y 좌표. NPCMovementAgentBC.fallY와 일치 권장.")]
    [SerializeField] private float fallY = -3f;

    [Header("프리팹 (필수)")]
    [Tooltip("NPCMovementAgentBC + RayPerceptionSensorComponent3D + BehaviorParameters 등이 세팅된 NPC 프리팹.")]
    [SerializeField] private GameObject npcAgentPrefab;

    [Tooltip("TrainingTarget + trigger Collider가 세팅된 타겟 프리팹. 비우면 기본 Sphere 자동 생성.")]
    [SerializeField] private GameObject targetPrefab;

    [Tooltip("장애물 프리팹. 비우면 기본 Cube 자동 생성.")]
    [SerializeField] private GameObject obstaclePrefab;

    [Header("병렬 학습")]
    [Range(1, 8)]
    [SerializeField] private int gridSize = 4;

    [SerializeField] private float arenaSpacing = 25f;

    [Header("에이전트")]
    [Tooltip("BehaviorParameters에 설정할 이름. 학습 YAML의 behaviors 키와 일치해야 함.")]
    [SerializeField] private string behaviorName = "MovementAgentBC";

    [Range(1, 20)]
    [SerializeField] private int decisionPeriod = 5;

    [Header("장애물 (레벨 1: 박스 배치)")]
    [Tooltip("장애물 박스 개수 per 아레나.")]
    [Range(0, 12)]
    [SerializeField] private int obstacleCount = 4;

    [Tooltip("장애물 크기 범위 (x/z 기준). y는 1~2.5m에서 랜덤.")]
    [SerializeField] private Vector2 obstacleSizeRange = new Vector2(1f, 3f);

    [Tooltip("장애물 배치 랜덤 시드. 같은 값이면 같은 배치.")]
    [SerializeField] private int randomSeed = 42;

    [Header("재질 (옵션)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private Material obstacleMaterial;

#if UNITY_EDITOR
    [ContextMenu("Build Arena")]
    public void BuildArena()
    {
        ClearArena();

        GameObject container = new GameObject("Arenas");
        container.transform.SetParent(transform, false);

        // 기존 Random 상태 보존, 고정 시드로 재현 가능
        Random.State oldState = Random.state;
        Random.InitState(randomSeed);

        int count = 0;
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                Vector3 offset = new Vector3(
                    (i - (gridSize - 1) * 0.5f) * arenaSpacing,
                    0f,
                    (j - (gridSize - 1) * 0.5f) * arenaSpacing
                );
                BuildSingleArena(container.transform, offset, count);
                count++;
            }
        }

        Random.state = oldState;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[ArenaBuilderBC] {count}개 아레나 생성 완료 (장애물 {obstacleCount}개씩).");
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

    private void BuildSingleArena(Transform parent, Vector3 offset, int index)
    {
        GameObject arena = new GameObject($"Arena_{index:D2}");
        arena.transform.SetParent(parent, false);
        arena.transform.localPosition = offset;

        // 바닥
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(arena.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(arenaSize, 1f, arenaSize);
        if (floorMaterial != null)
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

        // NPC — Prefab 인스턴스화
        if (npcAgentPrefab == null)
        {
            Debug.LogError("[ArenaBuilderBC] NPC Agent Prefab이 설정되지 않았습니다. Inspector에서 할당 후 다시 실행하세요.");
            return;
        }
        GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(npcAgentPrefab, arena.transform);
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
            Debug.LogWarning($"[ArenaBuilderBC] Prefab의 BehaviorName('{behavior.BehaviorName}')이 " +
                             $"ArenaBuilder 설정('{behaviorName}')과 다릅니다.");
        }

        // 타겟
        GameObject target;
        if (targetPrefab != null)
        {
            target = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, arena.transform);
            target.name = "Target";
        }
        else
        {
            target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "Target";
            target.transform.SetParent(arena.transform, false);
            var tcol = target.GetComponent<Collider>();
            tcol.isTrigger = true;
            if (targetMaterial != null)
                target.GetComponent<MeshRenderer>().sharedMaterial = targetMaterial;
            target.AddComponent<TrainingTarget>();
        }
        target.transform.localPosition = new Vector3(3f, 0.5f, 3f);

        // 장애물 박스 (레벨 1)
        if (obstacleCount > 0)
        {
            GameObject obsRoot = new GameObject("Obstacles");
            obsRoot.transform.SetParent(arena.transform, false);

            float half = arenaSize * 0.5f - 1.5f; // 가장자리 여유
            for (int k = 0; k < obstacleCount; k++)
            {
                GameObject obs;
                if (obstaclePrefab != null)
                {
                    obs = (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefab, obsRoot.transform);
                }
                else
                {
                    obs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obs.transform.SetParent(obsRoot.transform, false);
                    if (obstacleMaterial != null)
                        obs.GetComponent<MeshRenderer>().sharedMaterial = obstacleMaterial;
                }
                obs.name = $"Obstacle_{k:D2}";

                float sx = Random.Range(obstacleSizeRange.x, obstacleSizeRange.y);
                float sz = Random.Range(obstacleSizeRange.x, obstacleSizeRange.y);
                float sy = Random.Range(1f, 2.5f);
                obs.transform.localScale = new Vector3(sx, sy, sz);

                // 랜덤 위치 — 아레나 중앙(0,0) 근처는 피함 (NPC/Target 초기 스폰 영향 최소화)
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
                } while (pos.magnitude < 2.5f && attempts < 10);

                obs.transform.localPosition = pos;
            }
        }
    }
#endif
}
