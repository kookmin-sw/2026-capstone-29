using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어에게 부착되어 트리거로 아이템을 감지하는 통합 컴포넌트.
/// - 온라인: 기존 <see cref="ItemPickUp"/>과 동일하게 Command 경유.
/// - 오프라인: Command 없이 <see cref="IEquip.Save"/>를 직접 호출.
///
/// 기존 스크립트는 건드리지 않았음. 플레이어 프리팹에서
/// ItemPickUp을 제거하고 이 컴포넌트를 대신 붙이면 됨.
///
/// 픽업이 성공적으로 처리될 때, 해당 아이템이 ItemSpawner 가 만들었던 것이라면
/// SpawnedItemOrigin 마커를 통해 스포너에 NotifyItemConsumed 를 호출한다.
/// (서버/오프라인에서만 실제 처리되므로 클라이언트 중복 호출은 안전하게 무시됨)
/// </summary>
public class UnifiedItemPickUp : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 자기 자신만 감지 (오프라인이면 무조건 본인, 온라인이면 isLocalPlayer)
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;

        GameObject item = other.gameObject;
        IEquip equip = item.GetComponent<IEquip>();
        if (equip == null) return;

        RequestPickUp(item);
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