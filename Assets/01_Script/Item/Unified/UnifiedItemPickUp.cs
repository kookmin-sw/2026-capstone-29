using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어에게 부착되어 트리거로 아이템을 감지하는 통합 컴포넌트.
/// - 온라인: 기존 <see cref="ItemPickUp"/>과 동일하게 Command 경유.
/// - 오프라인: Command 없이 <see cref="IEquip.Save"/>를 직접 호출.
///
/// 기존 스크립트는 건드리지 않았음. 플레이어 프리팹에서
/// ItemPickUp을 제거하고 이 컴포넌트를 대신 붙이면 됨.
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

        // SetItem / UnifiedSetItem 양쪽 모두 Save()는 동일 API
        equip.Save(gameObject, item);
    }

    [Command]
    private void CmdPickUp(GameObject item)
    {
        if (item == null) return;
        IEquip equip = item.GetComponent<IEquip>();
        if (equip == null) return;

        // 서버에서 아이템 장착 처리
        equip.Save(gameObject, item);
    }
}
