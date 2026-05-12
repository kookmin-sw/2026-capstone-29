using Mirror;
using System.Collections;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

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

    //패시브는 각각의 코루틴으로 라이프사이클 관리를 하여야 한다.
    private class Passive
    {
        public IPassive passive;
        public Coroutine routine;
        public float timer;
        public float available; // duration
        public int uiId;
        public System.Action consumedHandler;
    }

    private int nextPassiveUiId = 1;
    private List<Passive> passiveEntries = new();

    // bool 상태는 온라인에선 클라이언트에 동기화, 오프라인에선 로컬 필드처럼 사용
    [SyncVar] private bool hasWeapon = false;
    [SyncVar] private bool hasActive = false;
    [SyncVar] private bool activeUsed = false;

    private GameObject currentWeaponObject; // 현재 보유 무기 아이템.
    [SyncVar] private int passiveCount = 0; // 클라이언트 동기화용

    [SyncVar] public float weaponAvailable;
    [SyncVar] public float activeAvailable;

    [SerializeField] private float weaponTimer;
    [SerializeField] private float activeTimer;

    private Coroutine activeRoutine;

    private void Update()
    {
        // 온라인: 서버만 타이머. 오프라인: 무조건 본인이 타이머 돌림.
        bool canTick = AuthorityGuard.IsOffline || isServer;
        if (!canTick) return;

        // 무기 타이머
        if (hasWeapon)
        {
            weaponTimer += Time.deltaTime;
            if (weaponAvailable <= weaponTimer)
            {
                Debug.Log("아이템 해제.");
                hasWeapon = false;
                weapon = null;
                currentWeaponObject = null;  // 추가
                weaponTimer = 0;
                weaponAvailable = 0;

                NotifyWeaponRemoved();
            }
        }

        // 액티브: 사용 중이면 타이머 진행 → 만료 시 코루틴 정지 + 원상 복구
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

        // 패시브 리스트 역순 순회
        for (int i = passiveEntries.Count - 1; i >= 0; i--)
        {
            var entry = passiveEntries[i];

            // 아직 코루틴이 시작되지 않은 아이템에 대해 발동을 적용
            if (entry.routine == null && entry.passive != null)
            {
                Debug.Log($"[UnifiedItemManager] Passive 발동 - available={entry.available}");
                entry.timer = 0f;
                entry.routine = StartCoroutine(
                    PassiveRoutineWrapper(entry, entry.passive.Activate(gameObject)));
                NotifyPassiveActivated(entry);
            }

            // 타이머 진행
            if (entry.routine != null)
            {
                entry.timer += Time.deltaTime;
                UpdatePassiveUILocal(entry);

                if (entry.available <= entry.timer)
                {
                    NotifyPassiveRemoved(entry);
                    ExpirePassiveEntry(entry);
                    passiveEntries.RemoveAt(i);
                    SyncPassiveCount();
                }
            }
        }
    }

        // ──────────────────────────────
        // 패시브 내부 헬퍼
        // ──────────────────────────────
    private void ExpirePassiveEntry(Passive entry)
    {
        if (entry.routine != null)
        {
            StopCoroutine(entry.routine);
            entry.routine = null;
            entry.passive?.OnDeactivate(gameObject);
        }
        // DamageAmplifier 콜백 해제
        var amp = GetComponent<DamageAmplifier>();
        if (amp != null && entry.consumedHandler != null)
        {
            amp.OnAllStacksConsumed -= entry.consumedHandler;
            entry.consumedHandler = null;
        }
    }

    private void RemoveDuplicatePassive(IPassive newPassive)
    {
        for (int i = passiveEntries.Count - 1; i >= 0; i--)
        {
            // SO 기준으로, 같은 에셋이면 같은 참조
            if (passiveEntries[i].passive == newPassive)
            {
                HidePassiveUILocal(passiveEntries[i]); // 중복 UI 제거
                ExpirePassiveEntry(passiveEntries[i]);
                passiveEntries.RemoveAt(i);
            }
        }
    }

    private void SyncPassiveCount()
    {
        passiveCount = passiveEntries.Count;
    }

    private bool ShouldControlLocalUI()
    {
        return AuthorityGuard.IsOffline || isLocalPlayer;
    }

    private void ShowPassiveUILocal(Passive entry)
    {
        if (!ShouldControlLocalUI()) return;
        if (entry == null || entry.passive == null) return;

        var uiManager = FindObjectOfType<InGameUIManger>();
        if (uiManager == null) return;

        uiManager.ShowPassiveItem(entry.uiId, entry.passive.UISprite, entry.passive.UIType, entry.available);
    }

    private void UpdatePassiveUILocal(Passive entry)
    {
        if (!ShouldControlLocalUI()) return;
        if (entry == null || entry.passive == null) return;
        if (entry.passive.UIType != PassiveUIType.TimedSpeed) return;
        if (entry.available <= 0f) return;

        var uiManager = FindObjectOfType<InGameUIManger>();
        if (uiManager == null) return;

        float normalized = 1f - entry.timer / entry.available;
        uiManager.UpdatePassiveItemTimer(entry.uiId, normalized);
    }

    private void HidePassiveUILocal(Passive entry)
    {
        if (!ShouldControlLocalUI()) return;
        if (entry == null) return;

        var uiManager = FindObjectOfType<InGameUIManger>();
        if (uiManager == null) return;

        uiManager.HidePassiveItem(entry.uiId);
    }

    // -----------------------------
    // 조회/세터 (SetItem / UnifiedSetItem에서 호출)
    // -----------------------------
    public void ApplyWeaponTimer(float available) => weaponAvailable = available;
    public void ApplyActiveTimer(float available) => activeAvailable = available;

    public void ApplyPassiveTimer(float available)
    {
        if (passiveEntries.Count > 0)
            passiveEntries[^1].available = available;
    }

    public bool HasWeapon() => hasWeapon;
    public bool HasActive() => hasActive;
    public bool IsActiveRunning() => activeUsed;
    public bool HasPassive() => passiveEntries.Count > 0;
    public bool IsPassiveRunning() => passiveEntries.Any(e => e.routine != null);

    public void GetActive() => hasActive = true;
    public void GetPassive(IPassive newPassive, float available)
    {
        // 같은 타입 중복 제거
        RemoveDuplicatePassive(newPassive);

        var entry = new Passive
        {
            passive = newPassive,
            routine = null,
            timer = 0f,
            available = available,
            uiId = nextPassiveUiId++
        };
        passiveEntries.Add(entry);
        SyncPassiveCount();

        // DamageAmplifier 조기 만료 콜백
        var amp = GetComponent<DamageAmplifier>();
        if (amp != null && newPassive.UIType == PassiveUIType.Battery)
        {
            entry.consumedHandler = () => OnPassiveEarlyExpired(entry);
            amp.OnAllStacksConsumed += entry.consumedHandler;
        }
    }

    public void GetWeapon(GameObject weaponObject)
    {
        // 이미 무기가 있으면 먼저 강제 만료시킨다.
        // 무기 본인의 ForceExpire가 NotifyUnequip → Destroy를 처리하므로
        // 여기서는 참조만 비우면 된다.
        if (hasWeapon && currentWeaponObject != null)
        {
            Debug.Log("[UnifiedItemManager] 기존 무기 강제 만료 후 새 무기 장착.");
            var oldWeapon = currentWeaponObject.GetComponent<IPlayerWeapon>();
            oldWeapon?.ForceExpire();
        }

        hasWeapon = true;
        weaponTimer = 0f;
        currentWeaponObject = weaponObject;
    }

    private void OnPassiveEarlyExpired(Passive entry)
    {
        if (!passiveEntries.Contains(entry)) return;
        
        NotifyPassiveRemoved(entry);
        ExpirePassiveEntry(entry);
        passiveEntries.Remove(entry);
        SyncPassiveCount();
    }

    public void ForceExpireAllPassives()
    {
        for (int i = passiveEntries.Count - 1; i >= 0; i--)
        {
            HidePassiveUILocal(passiveEntries[i]);
            ExpirePassiveEntry(passiveEntries[i]);
        }
        passiveEntries.Clear();
        SyncPassiveCount();
    }

    public void ForceExpirePassive<T>() where T : IPassive
    {
        for (int i = passiveEntries.Count - 1; i >= 0; i--)
        {
            if (passiveEntries[i].passive is T)
            {
                HidePassiveUILocal(passiveEntries[i]);
                ExpirePassiveEntry(passiveEntries[i]);
                passiveEntries.RemoveAt(i);
            }
        }
        SyncPassiveCount();
    }

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

    private IEnumerator PassiveRoutineWrapper(Passive entry, IEnumerator inner)
    {
        yield return inner;
        entry.routine = null;
    }

    private IEnumerator FieldRoutineWrapper(IEnumerator inner)
    {
        yield return inner;
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

    private void NotifyPassiveActivated(Passive entry)
    {
        if (AuthorityGuard.IsOffline)
        {
            ShowPassiveUILocal(entry);
            OnPassiveActivatedLocal();
            return;
        }

        if (!NetworkServer.active) return;

        string itemName = (entry.passive as ScriptableObject)?.name;
        if (string.IsNullOrEmpty(itemName)) return;

        TargetOnPassiveActivated(connectionToClient, entry.uiId, itemName, entry.available, entry.passive.UIType);
    }

    private void NotifyPassiveRemoved(Passive entry)
    {
        if (AuthorityGuard.IsOffline)
        {
            HidePassiveUILocal(entry);
            OnPassiveRemovedLocal();
            return;
        }

        if (!NetworkServer.active) return;

        TargetOnPassiveRemoved(connectionToClient, entry.uiId);
    }

    [ClientRpc] void RpcOnWeaponRemoved() => OnWeaponRemovedLocal();
    [ClientRpc] void RpcOnActiveUsed() => OnActiveUsedLocal();
    [ClientRpc] void RpcOnActiveRemoved() => OnActiveRemovedLocal();
    [ClientRpc] void RpcOnPassiveActivated() => OnPassiveActivatedLocal();
    [ClientRpc] void RpcOnPassiveRemoved() => OnPassiveRemovedLocal();

    private void OnWeaponRemovedLocal() => Debug.Log("무기 아이템 해제됨.");
    
    private void OnActiveUsedLocal()
    {
        Debug.Log("액티브 아이템 사용 시작.");

        // ★ 추가
        if (isLocalPlayer)
        {
            var uiManager = FindObjectOfType<InGameUIManger>();
            uiManager?.HideActiveItem();
    }

    }
    private void OnActiveRemovedLocal() => Debug.Log("액티브 아이템 해제됨."); 
    private void OnPassiveActivatedLocal() => Debug.Log("패시브 아이템 발동.");
    private void OnPassiveRemovedLocal() => Debug.Log("패시브 아이템 해제됨.");

    [TargetRpc]
    private void TargetOnPassiveActivated(NetworkConnection target, int uiId, string itemName, float duration, PassiveUIType uiType)
    {
        var passiveAsset = Resources.Load<ScriptableObject>($"Items/{itemName}") as IPassive;
        Sprite sprite = passiveAsset?.UISprite;

        var uiManager = FindObjectOfType<InGameUIManger>();
        uiManager?.ShowPassiveItem(uiId, sprite, uiType, duration);

        OnPassiveActivatedLocal();
    }

    [TargetRpc]
    private void TargetOnPassiveRemoved(NetworkConnection target, int uiId)
    {
        var uiManager = FindObjectOfType<InGameUIManger>();
        uiManager?.HidePassiveItem(uiId);

        OnPassiveRemovedLocal();
    }
}