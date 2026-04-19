using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Phase 1-A 학습용 아레나 자동 생성기.
///
/// 사용법:
/// 1. 빈 씬 열기
/// 2. 빈 GameObject 생성 → 이 스크립트 부착
/// 3. 인스펙터에서 파라미터 설정
/// 4. 컴포넌트 우상단 점 3개 메뉴 → "Build Arena" 클릭
///
/// 병렬 학습을 위해 N×N 그리드로 아레나 복제 가능.
/// 한 씬에 여러 환경이 있으면 ML-Agents가 자동으로 병렬 학습.
/// </summary>
public class ArenaBuilder : MonoBehaviour
{
    [Header("아레나 크기")]
    [Tooltip("한 변의 길이(m). NPCMovementAgent의 arenaRadius는 이 값의 절반이어야 함.")]
    [SerializeField] private float arenaSize = 20f;

    [Tooltip("낙하 판정 Y 좌표. NPCMovementAgent.fallY와 일치 권장.")]
    [SerializeField] private float fallY = -3f;

    [Header("프리팹 (필수)")]
    [Tooltip("BehaviorParameters/NPCMovementAgent/Rigidbody/DecisionRequester가 모두 세팅된 NPC 프리팹.\n" +
             "Behavior Name, ObservationSize, ContinuousActions 등은 Prefab에 저장된 값을 그대로 사용함.")]
    [SerializeField] private GameObject npcAgentPrefab;

    [Tooltip("TrainingTarget + trigger Collider가 세팅된 타겟 프리팹. 비우면 기본 Sphere가 자동 생성됨.")]
    [SerializeField] private GameObject targetPrefab;

    [Header("병렬 학습")]
    [Tooltip("N×N 그리드로 아레나 복제. 1이면 단일 환경, 4면 16개 병렬.")]
    [Range(1, 8)]
    [SerializeField] private int gridSize = 4;

    [Tooltip("인접 아레나 간 간격. 보통 arenaSize + 5 정도.")]
    [SerializeField] private float arenaSpacing = 25f;

    [Header("에이전트")]
    [Tooltip("BehaviorParameters에 설정할 이름. 학습 YAML의 behaviors 키와 일치해야 함.")]
    [SerializeField] private string behaviorName = "MovementAgent";

    [Tooltip("매 N step 마다 행동 결정 요청.")]
    [Range(1, 20)]
    [SerializeField] private int decisionPeriod = 5;

    [Header("재질 (옵션)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material agentMaterial;
    [SerializeField] private Material targetMaterial;

#if UNITY_EDITOR
    [ContextMenu("Build Arena")]
    public void BuildArena()
    {
        ClearArena();

        GameObject container = new GameObject("Arenas");
        container.transform.SetParent(transform, false);

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

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[ArenaBuilder] {count}개 아레나 생성 완료.");
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

        // 바닥 — 윗면이 y=0에 오도록 y=-0.5에 두께 1 큐브
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(arena.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(arenaSize, 1f, arenaSize);
        if (floorMaterial != null)
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

        // NPC — Prefab 인스턴스화 (BehaviorParameters 등 모든 컴포넌트 설정은 Prefab에 저장된 값 사용)
        if (npcAgentPrefab == null)
        {
            Debug.LogError("[ArenaBuilder] NPC Agent Prefab이 설정되지 않았습니다. Inspector에서 할당 후 다시 실행하세요.");
            return;
        }
        GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(npcAgentPrefab, arena.transform);
        npc.name = "NPCAgent";
        npc.transform.localPosition = new Vector3(0f, 1f, 0f);

        // NPCMovementAgent의 arenaRadius, fallY를 현재 Arena 크기에 맞춰 인스턴스 오버라이드
        var agent = npc.GetComponent<NPCMovementAgent>();
        if (agent != null)
        {
            var agentSO = new SerializedObject(agent);
            agentSO.FindProperty("arenaRadius").floatValue = arenaSize * 0.5f;
            agentSO.FindProperty("fallY").floatValue = fallY;
            agentSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // DecisionPeriod 인스턴스 오버라이드
        var req = npc.GetComponent<DecisionRequester>();
        if (req != null) req.DecisionPeriod = decisionPeriod;

        // behaviorName 검증 — Prefab 값과 ArenaBuilder 설정이 어긋나면 학습 시 YAML과 매칭 실패
        var behavior = npc.GetComponent<BehaviorParameters>();
        if (behavior != null && behavior.BehaviorName != behaviorName)
        {
            Debug.LogWarning($"[ArenaBuilder] Prefab의 BehaviorName('{behavior.BehaviorName}')이 " +
                             $"ArenaBuilder 설정('{behaviorName}')과 다릅니다. 일치시켜야 학습 YAML과 매칭됩니다.");
        }

        // 타겟 — Prefab 지정 시 인스턴스화, 없으면 기본 Sphere 자동 생성 (역호환)
        if (targetPrefab != null)
        {
            GameObject target = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, arena.transform);
            target.name = "Target";
            target.transform.localPosition = new Vector3(3f, 0.5f, 3f);
        }
        else
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "Target";
            target.transform.SetParent(arena.transform, false);
            target.transform.localPosition = new Vector3(3f, 0.5f, 3f);
            // 트리거로 변경해서 물리 상호작용 방지 (Agent가 거리로만 판정)
            var tcol = target.GetComponent<Collider>();
            tcol.isTrigger = true;
            if (targetMaterial != null)
                target.GetComponent<MeshRenderer>().sharedMaterial = targetMaterial;
            target.AddComponent<TrainingTarget>();
        }
    }
#endif
}
