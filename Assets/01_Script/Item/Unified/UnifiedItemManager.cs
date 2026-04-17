using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어가 들고 있는 아이템(무기/액티브/패시브)의 상태·유효시간을 관리하는 통합 컴포넌트.
/// - 온라인: 기존 <see cref="ItemManager"/>과 동일. 타이머는 서버에서만 돌고 RPC로 클라이언트 알림.
/// - 오프라인: 서버 없이 혼자 돌아가야 하므로 타이머를 직접 실행하고, RPC 대신 로컬 알림 호출.
///
/// <see cref="SyncVar"/>는 오프라인에선 단순 필드로 동작하기 때문에 그대로 두었다.
/// </summary>
public class UnifiedItemManager : NetworkBehaviour
{
    // 인터페이스는 SyncVar 불가 → 런타임에서만 참조
    [HideInInspector] public IWeapon weapon;
    [HideInInspector] public IActive active;
    [HideInInspector] public IPassive passive;

    // bool 상태는 온라인에선 클라이언트에 동기화, 오프라인에선 로컬 필드처럼 사용
    [SyncVar] private bool hasWeapon = false;
    [SyncVar] private bool hasActive = false;
    [SyncVar] private bool hasPassive = false;

    [SyncVar] public float weaponAvailable;
    [SyncVar] public float activeAvailable;

    [SerializeField] private float weaponTimer;
    [SerializeField] private float activeTimer;

    private void Update()
    {
        // 온라인: 서버만 타이머. 오프라인: 무조건 본인이 타이머 돌림.
        bool canTick = AuthorityGuard.IsOffline || isServer;
        if (!canTick) return;

        if (hasWeapon)
        {
            weaponTimer += Time.deltaTime;
            if (weaponAvailable <= weaponTimer)
            {
                Debug.Log("아이템 해제.");
                hasWeapon = false;
                weapon = null;
                weaponTimer = 0;
                weaponAvailable = 0;

                NotifyWeaponRemoved();
            }
        }

        if (hasActive)
        {
            activeTimer += Time.deltaTime;
            if (activeAvailable <= activeTimer)
            {
                hasActive = false;
                active = null;
                activeTimer = 0;
                activeAvailable = 0;

                NotifyActiveRemoved();
            }
        }

        if (hasPassive)
        {
            // 패시브는 얻는 즉시 효과 발동 후 제거
            if (passive != null) passive.Apply();
            hasPassive = false;
            passive = null;

            NotifyPassiveUsed();
        }
    }

    // -----------------------------
    // 조회/세터 (SetItem / UnifiedSetItem에서 호출)
    // -----------------------------
    public void ApplyWeaponTimer(float available) => weaponAvailable = available;
    public void ApplyActiveTimer(float available) => activeAvailable = available;

    public bool HasWeapon()  => hasWeapon;
    public bool HasActive()  => hasActive;
    public bool HasPassive() => hasPassive;

    public void GetWeapon()  => hasWeapon  = true;
    public void GetActive()  => hasActive  = true;
    public void GetPassive() => hasPassive = true;

    // -----------------------------
    // 알림 (오프라인은 직접 호출, 온라인은 서버 → 전체 클라이언트)
    // -----------------------------
    private void NotifyWeaponRemoved()
    {
        if (AuthorityGuard.IsOffline) OnWeaponRemovedLocal();
        else if (NetworkServer.active) RpcOnWeaponRemoved();
    }

    private void NotifyActiveRemoved()
    {
        if (AuthorityGuard.IsOffline) OnActiveRemovedLocal();
        else if (NetworkServer.active) RpcOnActiveRemoved();
    }

    private void NotifyPassiveUsed()
    {
        if (AuthorityGuard.IsOffline) OnPassiveUsedLocal();
        else if (NetworkServer.active) RpcOnPassiveUsed();
    }

    [ClientRpc] void RpcOnWeaponRemoved() => OnWeaponRemovedLocal();
    [ClientRpc] void RpcOnActiveRemoved() => OnActiveRemovedLocal();
    [ClientRpc] void RpcOnPassiveUsed()   => OnPassiveUsedLocal();

    private void OnWeaponRemovedLocal()
    {
        Debug.Log("무기 아이템 해제됨.");
        // UI 갱신 등의 작업 추가 가능
    }

    private void OnActiveRemovedLocal()
    {
        Debug.Log("액티브 아이템 해제됨.");
        // UI 갱신 등의 작업 추가 가능
    }

    private void OnPassiveUsedLocal()
    {
        Debug.Log("패시브 아이템 사용됨.");
        // UI/이펙트 등의 작업 추가 가능
    }
}
