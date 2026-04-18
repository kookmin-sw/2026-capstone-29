using Mirror;
using UnityEngine;

/// <summary>
/// 각 필드 아이템에 부착되어, 픽업 시 ItemManager(또는 UnifiedItemManager)에
/// 효과를 등록하는 통합 컴포넌트.
/// - 온라인: 기존 <see cref="SetItem"/>과 동일하게 Command → 서버 스폰 → Rpc 알림.
/// - 오프라인: Command 없이 직접 세팅 + Instantiate + Destroy.
///
/// 기존 <see cref="SetItem"/> 스크립트는 유지. 필드 아이템 프리팹에서
/// SetItem을 제거하고 이 컴포넌트를 붙이면 됨.
/// </summary>
public class UnifiedSetItem : NetworkBehaviour, IEquip
{
    [SerializeField] private ScriptableObject itemAsset;

    public void Save(GameObject user, GameObject item)
    {
        if (user == null || item == null) return;

        if (AuthorityGuard.IsOffline)
        {
            SaveLocal(user, item);
            return;
        }

        CmdSave(user, item);
    }

    // -----------------------------
    // 오프라인 경로
    // -----------------------------
    private void SaveLocal(GameObject user, GameObject item)
    {
        // UnifiedItemManager 우선, 없으면 기존 ItemManager fallback
        var unified = user.GetComponent<UnifiedItemManager>();
        if (unified != null)
        {
            ApplyToUnified(user, item, unified);
        }
        else
        {
            var legacy = user.GetComponent<ItemManager>();
            if (legacy == null)
            {
                Debug.LogWarning("[UnifiedSetItem] ItemManager/UnifiedItemManager를 찾을 수 없습니다.");
                return;
            }
            ApplyToLegacy(user, item, legacy);
        }

        // 로컬 알림 (RPC 대신)
        if (item.CompareTag("Weapon"))
            Debug.Log("[UnifiedSetItem] 아이템 장착! (local)");

        // 필드 아이템 제거 (로컬)
        Destroy(item);
    }

    private void ApplyToUnified(GameObject user, GameObject item, UnifiedItemManager im)
    {
        if (item.CompareTag("Weapon"))
        {
            Debug.Log("[UnifiedSetItem] 무기 감지 (local)");
            im.weapon = itemAsset as IWeapon;
            im.GetWeapon();

            if (im.weapon != null)
            {
                GameObject weaponObj = im.weapon.SummonWeapon(user.transform.position, Quaternion.identity);
                IPlayerWeapon ipw = weaponObj != null ? weaponObj.GetComponent<IPlayerWeapon>() : null;
                if (ipw != null) ipw.SetUser(user);
                im.weaponAvailable = im.weapon.AvailableTime();
            }
        }
        else if (item.CompareTag("Active"))
        {
            im.active = itemAsset as IActive;
            im.GetActive();
            if (im.active != null) im.activeAvailable = im.active.AvailableTime;
        }
        else if (item.CompareTag("Passive"))
        {
            im.passive = itemAsset as IPassive;
            im.GetPassive();
        }
        else if (item.CompareTag("Field"))
        {
            // 필드 아이템 처리 (후속 작업)
        }
    }

    private void ApplyToLegacy(GameObject user, GameObject item, ItemManager im)
    {
        if (item.CompareTag("Weapon"))
        {
            Debug.Log("[UnifiedSetItem] 무기 감지 (local → legacy ItemManager)");
            im.weapon = itemAsset as IWeapon;
            im.GetWeapon();

            if (im.weapon != null)
            {
                GameObject weaponObj = im.weapon.SummonWeapon(user.transform.position, Quaternion.identity);
                IPlayerWeapon ipw = weaponObj != null ? weaponObj.GetComponent<IPlayerWeapon>() : null;
                if (ipw != null) ipw.SetUser(user);
                im.weaponAvailable = im.weapon.AvailableTime();
            }
        }
        else if (item.CompareTag("Active"))
        {
            im.active = itemAsset as IActive;
            im.GetActive();
            if (im.active != null) im.activeAvailable = im.active.AvailableTime;
        }
        else if (item.CompareTag("Passive"))
        {
            im.passive = itemAsset as IPassive;
            im.GetPassive();
        }
    }

    // -----------------------------
    // 온라인 경로 (기존 SetItem과 동일 동작)
    // -----------------------------
    [Command(requiresAuthority = false)]
    private void CmdSave(GameObject user, GameObject item)
    {
        // 서버에서는 UnifiedItemManager 또는 ItemManager 둘 다 지원
        var unified = user.GetComponent<UnifiedItemManager>();
        var legacy = unified == null ? user.GetComponent<ItemManager>() : null;

        if (unified == null && legacy == null) return;

        if (item.CompareTag("Weapon"))
        {
            Debug.Log("무기 감지.");
            IWeapon weaponAsset = itemAsset as IWeapon;
            if (unified != null) { unified.weapon = weaponAsset; unified.GetWeapon(); }
            else                 { legacy.weapon  = weaponAsset; legacy.GetWeapon();  }

            if (weaponAsset != null)
            {
                GameObject weaponObj = weaponAsset.SummonWeapon(user.transform.position, Quaternion.identity);
                NetworkServer.Spawn(weaponObj, user.GetComponent<NetworkIdentity>().connectionToClient);
                IPlayerWeapon ipw = weaponObj.GetComponent<IPlayerWeapon>();
                if (ipw != null) ipw.SetUser(user);

                if (unified != null) unified.weaponAvailable = weaponAsset.AvailableTime();
                else                 legacy.weaponAvailable  = weaponAsset.AvailableTime();
            }

            RpcOnWeaponEquipped(user);
        }
        else if (item.CompareTag("Active"))
        {
            IActive activeAsset = itemAsset as IActive;
            if (unified != null) { unified.active = activeAsset; unified.GetActive(); if (activeAsset != null) unified.activeAvailable = activeAsset.AvailableTime; }
            else                 { legacy.active  = activeAsset; legacy.GetActive();  if (activeAsset != null) legacy.activeAvailable  = activeAsset.AvailableTime; }
        }
        else if (item.CompareTag("Passive"))
        {
            IPassive passiveAsset = itemAsset as IPassive;
            if (unified != null) { unified.passive = passiveAsset; unified.GetPassive(); }
            else                 { legacy.passive  = passiveAsset; legacy.GetPassive();  }
        }
        else if (item.CompareTag("Field"))
        {
            // 필드 아이템 처리
        }

        // 필드 아이템 오브젝트 제거
        NetworkServer.Destroy(item);
    }

    [ClientRpc]
    private void RpcOnWeaponEquipped(GameObject user)
    {
        Debug.Log("아이템 장착!");
    }
}
