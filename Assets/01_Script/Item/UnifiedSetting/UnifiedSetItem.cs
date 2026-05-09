using Mirror;
using UnityEngine;
// HardenOfflineObject: 오프라인에서 Instantiate된 오브젝트가 Mirror NetworkIdentity에 의해
// 비활성화되거나 SetParent가 막히는 것을 우회하기 위한 헬퍼.

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
                HardenOfflineObject(weaponObj);
                IPlayerWeapon ipw = weaponObj != null ? weaponObj.GetComponent<IPlayerWeapon>() : null;
                if (ipw != null) ipw.SetUser(user);
                im.weaponAvailable = im.weapon.AvailableTime();

                bool shouldShowLocalWeaponUI = AuthorityGuard.IsOffline
                    || user.GetComponent<UnifiedCharacterModel>()?.isLocalPlayer == true;

                // ★ 아이템 UI 표시 — 로컬 플레이어만 자기 화면에 표시
                if (weaponObj != null && shouldShowLocalWeaponUI)
                {
                    var uiManager = FindObjectOfType<InGameUIManger>();
                    if (uiManager != null)
                    {
                        Sprite itemSprite = im.weapon.UISprite;
                        var bomb = weaponObj.GetComponent<UnifiedWeaponBomb>();
                        if (bomb != null)
                            uiManager.ShowWeaponItemCount(itemSprite, bomb.MaxThrowCount);
                        else
                            uiManager.ShowWeaponItem(itemSprite, im.weapon.AvailableTime());
                    }
                }
            }
        }
        else if (item.CompareTag("Active"))
        {
            IActive activeAsset = itemAsset as IActive;
            im.active = activeAsset;
            if (activeAsset != null) im.activeAvailable = activeAsset.AvailableTime;
            im.GetActive();
        }
        else if (item.CompareTag("Passive"))
        {

            IPassive passiveAsset = itemAsset as IPassive;
            //레거시 코드. 수정 후 삭제 요망.
            /*
            im.passive = passiveAsset;
            if (passiveAsset != null) im.passiveAvailable = passiveAsset.AvailableTime;
            im.GetPassive();  // 플래그는 마지막에 세팅 (Update에서 자동 발동 트리거)
            */
            im.GetPassive(passiveAsset, passiveAsset.AvailableTime);
        }
        else if (item.CompareTag("Field"))
        {
            Debug.Log("필드 아이템 인식");
            IField field = itemAsset as IField;

            //오브젝트 소환만 하는 것으로 변경. 그 이후 생명주기 관리는 오브젝트에서 전담.
            field.SummonObj();
        }
    }

    //이전 스크립트르 적용. 더 이상 건드리지 않기에 수정 안하는 편이 좋음.
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
                HardenOfflineObject(weaponObj);
                IPlayerWeapon ipw = weaponObj != null ? weaponObj.GetComponent<IPlayerWeapon>() : null;
                if (ipw != null) ipw.SetUser(user);
                im.weaponAvailable = im.weapon.AvailableTime();
            }
        }
        else if (item.CompareTag("Active"))
        {
            IActive activeAsset = itemAsset as IActive;
            im.active = activeAsset;
            if (activeAsset != null) im.activeAvailable = activeAsset.AvailableTime;
            im.GetActive();
        }
        else if (item.CompareTag("Passive"))
        {
            IPassive passiveAsset = itemAsset as IPassive;
            im.passive = passiveAsset;
            if (passiveAsset != null) im.passiveAvailable = passiveAsset.AvailableTime;
            im.GetPassive();
        }
        else if (item.CompareTag("Field"))
        {
            Debug.Log("필드 아이템 인식");
            IField field = itemAsset as IField;
            field.SummonObj();

            //필드 아이템 수정중. 수정 성공시 제거.
            /*
            IField fieldAsset = itemAsset as IField;
            if (fieldAsset == null)
            {
                Debug.LogError("[UnifiedSetItem] Field 태그인데 itemAsset이 IField가 아님.");
            }
            else
            {
                im.field = fieldAsset;
                im.fieldAvailable = fieldAsset.AvailableTime;
                im.GetField();
            }
            */
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
            else { legacy.weapon = weaponAsset; legacy.GetWeapon(); }

            if (weaponAsset != null)
            {
                GameObject weaponObj = weaponAsset.SummonWeapon(user.transform.position, Quaternion.identity);
                NetworkServer.Spawn(weaponObj, user.GetComponent<NetworkIdentity>().connectionToClient);
                IPlayerWeapon ipw = weaponObj.GetComponent<IPlayerWeapon>();
                if (ipw != null) ipw.SetUser(user);

                if (unified != null) unified.weaponAvailable = weaponAsset.AvailableTime();
                else legacy.weaponAvailable = weaponAsset.AvailableTime();
            }

            RpcOnWeaponEquipped(user);
        }
        else if (item.CompareTag("Active"))
        {
            IActive activeAsset = itemAsset as IActive;
            if (unified != null)
            {
                unified.active = activeAsset;
                if (activeAsset != null) unified.activeAvailable = activeAsset.AvailableTime;
                unified.GetActive();
            }
            else
            {
                legacy.active = activeAsset;
                if (activeAsset != null) legacy.activeAvailable = activeAsset.AvailableTime;
                legacy.GetActive();
            }

            NetworkIdentity userIdentity = user.GetComponent<NetworkIdentity>();

            Debug.Log($"[UnifiedSetItem] isServer={isServer}, isClient={isClient}");
            Debug.Log($"[UnifiedSetItem] connectionToClient={userIdentity.connectionToClient}");

            if (userIdentity.connectionToClient is LocalConnectionToClient)
            {
                // 호스트 경로 — 직접 UI 호출
                var activeAssetLoaded = Resources.Load<ScriptableObject>($"Items/{itemAsset.name}") as IActive;
                var uiManager = FindObjectOfType<InGameUIManger>();
                uiManager?.ShowActiveItem(activeAssetLoaded?.UISprite);
            }
            else
            {
                // 일반 클라이언트 경로
                RpcOnActiveEquipped(userIdentity.connectionToClient, itemAsset.name);
            }
        }
        else if (item.CompareTag("Passive"))
        {
            IPassive passiveAsset = itemAsset as IPassive;
            
            if (unified != null)
            {
                /*
                // ★ 이전 패시브 강제 해제 추가
                if (unified.HasPassive() || unified.IsPassiveRunning())
                    unified.ForceExpirePassive();

                unified.passive = passiveAsset;
                if (passiveAsset != null) unified.passiveAvailable = passiveAsset.AvailableTime;
                unified.GetPassive();
                */

                unified.GetPassive(passiveAsset, passiveAsset.AvailableTime);

            }
            else
            {
                legacy.passive = passiveAsset;
                if (passiveAsset != null) legacy.passiveAvailable = passiveAsset.AvailableTime;
                legacy.GetPassive();
            }
        }
        else if (item.CompareTag("Field"))
        {
            IField field = itemAsset as IField;

            //오브젝트 소환만 하는 것으로 변경. 그 이후 생명주기 관리는 오브젝트에서 전담.
            field.SummonObj();
        }

        // 필드 아이템 오브젝트 제거
        NetworkServer.Destroy(item);
    }
    [ClientRpc]
    private void RpcOnWeaponEquipped(GameObject user)
    {
        Debug.Log("아이템 장착!");
    }

    // 액티브 아이템 획득 시
    [TargetRpc]
    private void RpcOnActiveEquipped(NetworkConnection target, string itemName)
    {
        // 클라이언트에서 직접 에셋을 로드
        var activeAsset = Resources.Load<ScriptableObject>($"Items/{itemName}") as IActive;
        Sprite sprite = activeAsset?.UISprite;

        var uiManager = FindObjectOfType<InGameUIManger>();
        uiManager?.ShowActiveItem(sprite);
    }

    // -----------------------------
    // 오프라인 하드닝 헬퍼
    // -----------------------------
    private static void HardenOfflineObject(GameObject obj)
    {
        if (obj == null) return;
        if (!AuthorityGuard.IsOffline) return; // 온라인에선 Mirror가 관리

        if (obj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;

        if (!obj.activeSelf) obj.SetActive(true);
    }
}
