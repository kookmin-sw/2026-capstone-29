using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어에게 부착되어 트리거 영역 내 아이템 후보들을 추적하고,
/// 외부 호출(예: E 키) 시점에 그 중 가장 가까운 아이템 하나를 획득한다.
///
/// - 온라인: 기존 ItemPickUp 과 동일하게 Command 경유.
/// - 오프라인: Command 없이 IEquip.Save 를 직접 호출.
///
/// 추가:
///   픽업이 성공적으로 처리될 때, 해당 아이템이 ItemSpawner 가 만들었던 것이라면
///   SpawnedItemOrigin 마커를 통해 스포너에 NotifyItemConsumed 를 호출한다.
///   (서버/오프라인에서만 실제 처리되므로 클라이언트 중복 호출은 안전하게 무시됨)
/// </summary>
public class UnifiedItemPickUp : NetworkBehaviour
{
    // 트리거 영역에 들어와 있는 픽업 후보들. (IEquip 가 붙은 GameObject 만 추적)
    private readonly HashSet<GameObject> _candidates = new HashSet<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        // 입력은 외부에서 받지만, 트리거 추적 자체도 본인 플레이어만 의미가 있다.
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;

        GameObject item = other.gameObject;
        if (item.GetComponent<IEquip>() == null) return;

        _candidates.Add(item);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;

        GameObject item = other.gameObject;
        if (_candidates.Count == 0) return;
        _candidates.Remove(item);
    }

    /// <summary>
    /// 트리거 영역 내 후보 중 가장 가까운 아이템 1개를 획득한다.
    /// 외부에서 키 입력 시점에 호출.
    /// </summary>
    /// <returns>획득을 시도한 경우 true, 후보가 없으면 false.</returns>
    public bool TryPickupNearest()
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return false;

        GameObject nearest = FindNearestCandidate();
        if (nearest == null) return false;

        // 후보 목록에서 즉시 제거 (이중 픽업 방지)
        _candidates.Remove(nearest);

        RequestPickUp(nearest);
        return true;
    }

    /// <summary>현재 트리거 영역 내에 픽업 가능한 아이템이 있는지.</summary>
    public bool HasCandidate()
    {
        // null 슬롯 정리하면서 카운트
        return FindNearestCandidate() != null;
    }

    private GameObject FindNearestCandidate()
    {
        GameObject best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 here = transform.position;

        // null 또는 IEquip 잃어버린 항목은 제거
        _candidates.RemoveWhere(go => go == null || go.GetComponent<IEquip>() == null);

        foreach (var go in _candidates)
        {
            float sqr = (go.transform.position - here).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = go;
            }
        }
        return best;
    }

    /// <summary>오프라인/온라인 공용 진입점.</summary>
    public void RequestPickUp(GameObject item)
    {
        if (item == null) return;

        if (AuthorityGuard.IsOffline)
        {
            PickUpLocal(item);
            return;
        }

        CmdPickUp(item);
    }

    private void PickUpLocal(GameObject item)
    {
        if (item == null) return;
        IEquip equip = item.GetComponent<IEquip>();
        if (equip == null) return;

        // 스포너 통지를 먼저 수행한다.
        // equip.Save() 가 아이템을 Destroy 하거나 비활성화 할 수 있으므로,
        // GameObject 참조가 살아있을 때 통지해두는 편이 안전하다.
        NotifySpawnerConsumed(item);

        // SetItem / UnifiedSetItem 양쪽 모두 Save()는 동일 API
        equip.Save(gameObject, item);
    }

    [Command]
    private void CmdPickUp(GameObject item)
    {
        if (item == null) return;
        IEquip equip = item.GetComponent<IEquip>();
        if (equip == null) return;

        // 서버 권한에서 스포너 통지 (NotifyItemConsumed 내부에서 서버 권한 재확인)
        NotifySpawnerConsumed(item);

        // 서버에서 아이템 장착 처리
        equip.Save(gameObject, item);
    }

    /// <summary>
    /// 아이템이 ItemSpawner 가 만든 것이라면, 그 스포너에 소비 사실을 알린다.
    /// 마커가 없으면(스포너 외부에서 수동 배치된 아이템) 조용히 무시한다.
    /// </summary>
    private static void NotifySpawnerConsumed(GameObject item)
    {
        if (item == null) return;

        var origin = item.GetComponent<SpawnedItemOrigin>();
        if (origin == null) return;
        if (origin.spawner == null) return;

        origin.spawner.NotifyItemConsumed(item);
    }
}