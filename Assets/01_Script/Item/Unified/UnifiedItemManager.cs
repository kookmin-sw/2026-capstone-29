using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어가 들고 있는 아이템(무기/액티브/패시브/필드)의 상태·유효시간을 관리하는 통합 컴포넌트.
/// - 온라인: 기존 <see cref="ItemManager"/>과 동일. 타이머는 서버에서만 돌고 RPC로 클라이언트 알림.
/// - 오프라인: 서버 없이 혼자 돌아가야 하므로 타이머를 직접 실행하고, RPC 대신 로컬 알림 호출.
///
/// <see cref="SyncVar"/>는 오프라인에선 단순 필드로 동작하기 때문에 그대로 두었다.
/// 코루틴(<see cref="StartCoroutine"/>)은 MonoBehaviour 기능이라 온/오프라인 모두 동일하게 사용 가능.
/// </summary>
public class UnifiedItemManager : NetworkBehaviour
{
    // 인터페이스는 SyncVar 불가 → 런타임에서만 참조
    [HideInInspector] public IWeapon weapon;
    [HideInInspector] public IActive active;
    [HideInInspector] public IPassive passive;
    [HideInInspector] public IField field;

    // bool 상태는 온라인에선 클라이언트에 동기화, 오프라인에선 로컬 필드처럼 사용
    [SyncVar] private bool hasWeapon = false;
    [SyncVar] private bool hasActive = false;
    [SyncVar] private bool activeUsed = false;
    [SyncVar] private bool hasPassive = false;
    [SyncVar] private bool passiveUsed = false;
    [SyncVar] private bool hasField = false;
    [SyncVar] private bool fieldUsed = false;

    [SyncVar] public float weaponAvailable;
    [SyncVar] public float activeAvailable;
    [SyncVar] public float passiveAvailable;
    [SyncVar] public float fieldAvailable;

    [SerializeField] private float weaponTimer;
    [SerializeField] private float activeTimer;
    [SerializeField] private float passiveTimer;
    [SerializeField] private float fieldTimer;

    private Coroutine activeRoutine;
    private Coroutine passiveRoutine;
    private Coroutine fieldRoutine;

    private void Update()
    {
        // 온라인: 서버만 타이머. 오프라인: 무조건 본인이 타이머 돌림.
        bool canTick = AuthorityGuard.IsOffline || isServer;
        if (!canTick) return;

        // -----------------------------
        // 무기 타이머
        // -----------------------------
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

        // -----------------------------
        // 액티브: 사용 중이면 타이머 진행 → 만료 시 코루틴 정지 + 원복
        // -----------------------------
        if (activeUsed)
        {
            activeTimer += Time.deltaTime;
            if (activeAvailable <= activeTimer)
            {
                if (activeRoutine != null)
                {
                    StopCoroutine(activeRoutine);
                    activeRoutine = null;
                    active?.OnDeactivate(gameObject);
                }

                activeUsed = false;
                hasActive = false;
                active = null;
                activeTimer = 0;
                activeAvailable = 0;
                NotifyActiveRemoved();
            }
        }

        // -----------------------------
        // 패시브: 장착 즉시 자동 발동
        // -----------------------------
        if (hasPassive && !passiveUsed && passive != null)
        {
            Debug.Log($"[UnifiedItemManager] Passive 발동 - available={passiveAvailable}");
            passiveUsed = true;
            passiveTimer = 0f;
            passiveRoutine = StartCoroutine(PassiveRoutineWrapper(passive.Activate(gameObject)));
            NotifyPassiveActivated();
        }

        if (passiveUsed)
        {
            passiveTimer += Time.deltaTime;
            if (passiveAvailable <= passiveTimer)
            {
                if (passiveRoutine != null)
                {
                    StopCoroutine(passiveRoutine);
                    passiveRoutine = null;
                    passive?.OnDeactivate(gameObject);
                }

                passiveUsed = false;
                hasPassive = false;
                passive = null;
                passiveTimer = 0;
                passiveAvailable = 0;
                NotifyPassiveRemoved();
            }
        }

        // -----------------------------
        // 필드: 장착 즉시 자동 발동
        // -----------------------------
        if (hasField && !fieldUsed && field != null)
        {
            fieldUsed = true;
            fieldTimer = 0f;
            fieldRoutine = StartCoroutine(FieldRoutineWrapper(field.Activate()));
            NotifyFieldActivated();
        }

        if (fieldUsed)
        {
            fieldTimer += Time.deltaTime;
            if (fieldAvailable <= fieldTimer)
            {
                if (fieldRoutine != null)
                {
                    StopCoroutine(fieldRoutine);
                    fieldRoutine = null;
                    field?.OnDeactivate();
                }

                fieldUsed = false;
                hasField = false;
                field = null;
                fieldTimer = 0;
                fieldAvailable = 0;
                NotifyFieldRemoved();
            }
        }
    }

    // -----------------------------
    // 조회/세터 (SetItem / UnifiedSetItem에서 호출)
    // -----------------------------
    public void ApplyWeaponTimer(float available) => weaponAvailable = available;
    public void ApplyActiveTimer(float available) => activeAvailable = available;
    public void ApplyPassiveTimer(float available) => passiveAvailable = available;
    public void ApplyFieldTimer(float available) => fieldAvailable = available;

    public bool HasWeapon() => hasWeapon;
    public bool HasActive() => hasActive;
    public bool IsActiveRunning() => activeUsed;
    public bool HasPassive() => hasPassive;
    public bool IsPassiveRunning() => passiveUsed;
    public bool HasField() => hasField;
    public bool IsFieldRunning() => fieldUsed;

    public void GetWeapon() => hasWeapon = true;
    public void GetActive() => hasActive = true;
    public void GetPassive() => hasPassive = true;
    public void GetField() => hasField = true;

    // -----------------------------
    // 액티브 사용 요청 (입력 → 실행)
    // -----------------------------
    public void RequestUseActive()
    {
        // 보유하지 않았거나 이미 사용 중이면 무시
        if (!hasActive || activeUsed) return;

        if (AuthorityGuard.IsOffline)
        {
            UseActiveLocal();
            return;
        }

        CmdUseActive();
    }

    [Command]
    private void CmdUseActive() => UseActiveServer();

    [Server]
    private void UseActiveServer()
    {
        if (!hasActive || activeUsed || active == null) return;

        activeUsed = true;
        activeTimer = 0f;
        activeRoutine = StartCoroutine(ActiveRoutineWrapper(active.Activate(gameObject)));

        RpcOnActiveUsed();
    }

    private void UseActiveLocal()
    {
        if (!hasActive || activeUsed || active == null) return;

        activeUsed = true;
        activeTimer = 0f;
        activeRoutine = StartCoroutine(ActiveRoutineWrapper(active.Activate(gameObject)));

        OnActiveUsedLocal();
    }

    // -----------------------------
    // 코루틴 래퍼 (자연 종료 시 핸들 정리)
    // -----------------------------
    private IEnumerator ActiveRoutineWrapper(IEnumerator inner)
    {
        yield return inner;
        activeRoutine = null;
    }

    private IEnumerator PassiveRoutineWrapper(IEnumerator inner)
    {
        yield return inner;
        passiveRoutine = null;
    }

    private IEnumerator FieldRoutineWrapper(IEnumerator inner)
    {
        yield return inner;
        fieldRoutine = null;
    }

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

    private void NotifyPassiveActivated()
    {
        if (AuthorityGuard.IsOffline) OnPassiveActivatedLocal();
        else if (NetworkServer.active) RpcOnPassiveActivated();
    }

    private void NotifyPassiveRemoved()
    {
        if (AuthorityGuard.IsOffline) OnPassiveRemovedLocal();
        else if (NetworkServer.active) RpcOnPassiveRemoved();
    }

    private void NotifyFieldActivated()
    {
        if (AuthorityGuard.IsOffline) OnFieldActivatedLocal();
        else if (NetworkServer.active) RpcOnFieldActivated();
    }

    private void NotifyFieldRemoved()
    {
        if (AuthorityGuard.IsOffline) OnFieldRemovedLocal();
        else if (NetworkServer.active) RpcOnFieldRemoved();
    }

    [ClientRpc] void RpcOnWeaponRemoved() => OnWeaponRemovedLocal();
    [ClientRpc] void RpcOnActiveUsed() => OnActiveUsedLocal();
    [ClientRpc] void RpcOnActiveRemoved() => OnActiveRemovedLocal();
    [ClientRpc] void RpcOnPassiveActivated() => OnPassiveActivatedLocal();
    [ClientRpc] void RpcOnPassiveRemoved() => OnPassiveRemovedLocal();
    [ClientRpc] void RpcOnFieldActivated() => OnFieldActivatedLocal();
    [ClientRpc] void RpcOnFieldRemoved() => OnFieldRemovedLocal();

    private void OnWeaponRemovedLocal() => Debug.Log("무기 아이템 해제됨.");
    private void OnActiveUsedLocal() => Debug.Log("액티브 아이템 사용 시작.");
    private void OnActiveRemovedLocal() => Debug.Log("액티브 아이템 해제됨.");
    private void OnPassiveActivatedLocal() => Debug.Log("패시브 아이템 발동.");
    private void OnPassiveRemovedLocal() => Debug.Log("패시브 아이템 해제됨.");
    private void OnFieldActivatedLocal() => Debug.Log("필드 아이템 발동 (장판 스폰).");
    private void OnFieldRemovedLocal() => Debug.Log("필드 아이템 해제됨.");
}