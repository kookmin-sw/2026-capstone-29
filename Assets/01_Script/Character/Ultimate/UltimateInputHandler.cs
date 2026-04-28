using UnityEngine;

public class UltimateInputHandler : MonoBehaviour
{
    [Header("참조 (비워두면 자동 검색)")]
    [Tooltip("같은 GameObject 또는 자식의 UltimateSkillController를 자동 연결")]
    public UltimateSkillController controller;

    [Header("타겟 설정")]
    [Tooltip("얼티밋 시전 시 사용할 타겟. 비어있으면 아래 태그로 자동 검색")]
    public Transform target;

    [Tooltip("target이 비었을 때 자동 검색에 사용할 태그 (예: Enemy). 비워두면 자동 검색 안 함")]
    public string autoFindTag = "Enemy";

    [Tooltip("자동 검색 시 최대 거리 (0 = 무제한)")]
    public float autoFindRange = 0f;

    [Header("입력")]
    public KeyCode triggerKey = KeyCode.Q;

    // ─────────────────────────────────────────────────────────
    //  프리팹 안전 초기화
    // ─────────────────────────────────────────────────────────

    private void Reset()
    {
        AutoBindController();
    }

    private void Awake()
    {
        AutoBindController();
    }

    private void AutoBindController()
    {
        if (controller != null) return;

        // 같은 GameObject → 자식 → 부모 순으로 검색
        controller = GetComponent<UltimateSkillController>();
        if (controller == null)
            controller = GetComponentInChildren<UltimateSkillController>();
        if (controller == null)
            controller = GetComponentInParent<UltimateSkillController>();
    }

    // ─────────────────────────────────────────────────────────
    //  입력 처리
    // ─────────────────────────────────────────────────────────

    void Update()
    {
        if (controller == null) return;
        if (!Input.GetKeyDown(triggerKey)) return;

        TriggerNow();
    }

    /// <summary>UI 버튼 등에서 직접 호출 가능</summary>
    public void TriggerNow()
    {
        if (controller == null)
        {
            Debug.LogWarning("[UltimateInputHandler] UltimateSkillController가 연결되지 않았습니다.", this);
            return;
        }

        Transform finalTarget = target != null ? target : FindNearestByTag();
        controller.TriggerUltimate(finalTarget);
    }

    /// <summary>외부에서 타겟을 동적으로 지정 (락온 시스템 등)</summary>
    public void SetTarget(Transform t)
    {
        target = t;
    }

    // ─────────────────────────────────────────────────────────
    //  자동 타겟 검색 (태그 기반)
    // ─────────────────────────────────────────────────────────

    private Transform FindNearestByTag()
    {
        if (string.IsNullOrEmpty(autoFindTag)) return null;

        GameObject[] candidates;
        try
        {
            candidates = GameObject.FindGameObjectsWithTag(autoFindTag);
        }
        catch (UnityException)
        {
            // 태그 매니저에 등록되지 않은 태그
            Debug.LogWarning($"[UltimateInputHandler] '{autoFindTag}' 태그가 등록되지 않았습니다.", this);
            return null;
        }

        if (candidates == null || candidates.Length == 0) return null;

        Transform nearest = null;
        float minDistSq = float.MaxValue;
        Vector3 myPos = transform.position;
        float rangeSq = autoFindRange > 0f ? autoFindRange * autoFindRange : float.MaxValue;

        foreach (GameObject go in candidates)
        {
            if (go == null) continue;
            float distSq = (go.transform.position - myPos).sqrMagnitude;
            if (distSq <= rangeSq && distSq < minDistSq)
            {
                minDistSq = distSq;
                nearest = go.transform;
            }
        }

        return nearest;
    }
}
