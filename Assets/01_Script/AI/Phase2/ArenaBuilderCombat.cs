using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Phase 2-A 학습용 아레나 생성기.
///
/// 구성: Floor + NPCCombatAgent(Prefab) + DummyEnemy(Prefab or 기본 Capsule 자동 생성).
/// 병렬 학습을 위해 gridSize x gridSize 복제.
///
/// 사용법:
/// 1. 빈 씬에 빈 GameObject -> 이 스크립트 부착
/// 2. NPC Combat Agent Prefab 할당 (BehaviorName=CombatAgentSword 필수)
/// 3. Build Arena 실행
///
/// Phase 2-B 이후 확장: dummyEnemyPrefab 자리에 스크립트 AI/Self-play 에이전트 교체.
/// </summary>
public class ArenaBuilderCombat : MonoBehaviour
{
    [Header("아레나")]
    [SerializeField] private float arenaSize = 20f;
    [SerializeField] private float fallY = -3f;

    [Header("프리팹 (필수)")]
    [Tooltip("NPCCombatAgent + BehaviorParameters(CombatAgentSword) 이 세팅된 Prefab.")]
    [SerializeField] private GameObject npcAgentPrefab;

    [Tooltip("DummyEnemy 스크립트가 붙은 Prefab. 비우면 기본 Capsule 자동 생성.")]
    [SerializeField] private GameObject dummyEnemyPrefab;

    [Header("병렬 학습")]
    [Range(1, 8)]
    [SerializeField] private int gridSize = 4;
    [SerializeField] private float arenaSpacing = 25f;

    [Header("에이전트")]
    [SerializeField] private string behaviorName = "CombatAgentSword";
    [Range(1, 20)]
    [SerializeField] private int decisionPeriod = 5;

    [Header("재질 (옵션)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material enemyMaterial;

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
        Debug.Log($"[ArenaBuilderCombat] {count}개 아레나 생성 완료.");
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
        GameObject arenaGo = new GameObject($"Arena_{index:D2}");
        arenaGo.transform.SetParent(parent, false);
        arenaGo.transform.localPosition = offset;
        Transform arena = arenaGo.transform;

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(arena, false);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(arenaSize, 1f, arenaSize);
        if (floorMaterial != null)
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

        // NPC Combat Agent
        if (npcAgentPrefab == null)
        {
            Debug.LogError("[ArenaBuilderCombat] NPC Agent Prefab이 설정되지 않았습니다.");
            return;
        }
        GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(npcAgentPrefab, arena);
        npc.name = "NPCCombatAgent";
        npc.transform.localPosition = new Vector3(0f, 1f, 0f);

        var agent = npc.GetComponent<NPCCombatAgent>();
        if (agent != null)
        {
            var so = new SerializedObject(agent);
            so.FindProperty("arenaRadius").floatValue = arenaSize * 0.5f;
            so.FindProperty("fallY").floatValue = fallY;
            so.FindProperty("arenaCenter").objectReferenceValue = arena;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var req = npc.GetComponent<DecisionRequester>();
        if (req != null) req.DecisionPeriod = decisionPeriod;

        var behavior = npc.GetComponent<BehaviorParameters>();
        if (behavior != null && behavior.BehaviorName != behaviorName)
        {
            Debug.LogWarning($"[ArenaBuilderCombat] Prefab의 BehaviorName('{behavior.BehaviorName}')이 " +
                             $"ArenaBuilder 설정('{behaviorName}')과 다릅니다.");
        }

        // Dummy Enemy
        GameObject dummy;
        if (dummyEnemyPrefab != null)
        {
            dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyEnemyPrefab, arena);
        }
        else
        {
            dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.transform.SetParent(arena, false);
            if (enemyMaterial != null)
                dummy.GetComponent<MeshRenderer>().sharedMaterial = enemyMaterial;

            // 기본 Capsule에 필요한 컴포넌트 부착
            if (dummy.GetComponent<Rigidbody>() == null) dummy.AddComponent<Rigidbody>();
            if (dummy.GetComponent<DummyEnemy>() == null) dummy.AddComponent<DummyEnemy>();
        }
        dummy.name = "DummyEnemy";
        dummy.transform.localPosition = new Vector3(3f, 0.5f, 3f);

        var dummyComp = dummy.GetComponent<DummyEnemy>();
        if (dummyComp != null)
        {
            dummyComp.Configure(arena, arenaSize * 0.5f);
            // Agent에 적 참조 주입
            var agentSO = new SerializedObject(agent);
            agentSO.FindProperty("enemy").objectReferenceValue = dummyComp;
            agentSO.ApplyModifiedPropertiesWithoutUndo();
        }
    }
#endif
}
