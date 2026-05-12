using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// NetworkCharacterModel + CharacterModel 을 하나로 합친 모델.
/// - 네트워크 모드: SyncVar + Cmd/Rpc 로 동작 (기존 NetworkCharacterModel과 동일 동작)
/// - 오프라인 모드: 직접 필드 변이 + 이벤트 발행 (기존 CharacterModel과 동일 동작)
/// 외부는 항상 RequestXxx() 또는 공개 프로퍼티만 사용한다.
///
/// 주의:
/// - GameObject에 NetworkIdentity가 반드시 붙어 있어야 함 (NetworkBehaviour 요구).
/// - CharacterHitBox 등 레거시 스크립트가 NetworkCharacterModel/CharacterModel을 직접 찾는 경우,
///   마이그레이션 시 해당 호출 경로를 ICharacterModel로 바꿔야 한다(README 참고).
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class UnifiedCharacterModel : NetworkBehaviour, ICharacterModel
{
    // ============================================================
    // 동기화 필드 (네트워크 모드에선 SyncVar, 오프라인에선 그냥 필드로 사용)
    // ============================================================
    [SyncVar(hook = nameof(OnComboChangedHook))]
    private int comboCount = 0;

    [SyncVar(hook = nameof(OnHealthChangedHook))]
    private float currentHealth = 100f;

    [SyncVar(hook = nameof(OnDieHook))]
    private bool isDead = false;

    [SyncVar(hook = nameof(OnChargeStateChangedHook))]
    private bool isCharging = false;

    [SyncVar(hook = nameof(OnChargeReadyChangedHook))]
    private bool isChargeReady = false;

    [SyncVar(hook = nameof(OnHasBowChangedHook))]
    private bool hasBow = false;

    [SyncVar(hook = nameof(OnBowDrawChangedHook))]
    private bool isBowDraw = false;

    [SyncVar(hook = nameof(OnBowReleaseHook))]
    private int bowReleaseCount = 0;

    [SyncVar(hook = nameof(OnHasGunChangedHook))]
    private bool hasGun = false;

    [SyncVar(hook = nameof(OnGunShootHook))]
    private int gunShootCount = 0;

    [SyncVar(hook = nameof(OnLivesChangedHook))]
    public int remaingLives; // 오타 유지(기존 호환)

    [SyncVar(hook = nameof(OnStunChangedHook))]
    private bool isStunned = false;

    // ---- 설정 ----
    [Header("Lives Setting")]
    public int maxLives = 1;

    [Header("초기 체력")]
    public float startHealth = 100f;

    [Header("Hit Effect")]
    public GameObject[] hitEffectPrefabs;  // 0: 오른손, 1: 왼손, 2: 발, 3: --
    public float effectDuration = 2f;

    // ---- 스턴 내부 상태 (서버/오프라인 권한자만 사용) ----
    private float _stunRemainingTime;
    private Coroutine _stunCoroutine;

    /// <summary>
    /// 스턴이 절대 이 시간을 넘기지 못하도록 강제하는 워치독 상한.
    /// 코루틴이 어떤 이유로든 자연 종료되지 못하더라도 이 시간이 지나면 강제 해제된다.
    /// 갱신(중복 피격)이 거듭되어도 한 번 스턴이 시작된 후 이 시간이 지나면 무조건 풀림.
    /// 정상적인 스턴 지속시간보다 충분히 길게 둘 것 (예: 일반 스턴 2초 / 워치독 15초).
    /// </summary>
    private const float StunAbsoluteCap = 15f;

    // ============================================================
    // ICharacterModel 조회 프로퍼티
    // ============================================================
    public int ComboCount => comboCount;
    public float CurrentHealth => currentHealth;
    public int RemainingLives => remaingLives;
    public bool IsDead => isDead;
    public bool IsCharging => isCharging;
    public bool IsChargeReady => isChargeReady;
    public bool HasBow => hasBow;
    public bool IsBowDraw => isBowDraw;
    public bool IsStunned => isStunned;
    public bool HasGun => hasGun;

    // ============================================================
    // 이벤트
    // ============================================================
    public event Action<int> OnComboChanged;
    public event Action<float> OnHealthChanged;
    public event Action<int> OnLivesChanged;
    public event Action OnDie;
    public event Action OnRespawn;
    public event Action OnStrongAttack;
    public event Action OnGameOver;
    public event Action<bool> OnChargeStateChanged;
    public event Action<bool> OnChargeReadyChanged;
    public event Action<bool> OnHasBowChanged;
    public event Action<bool> OnBowDrawChanged;
    public event Action OnBowRelease;
    public event Action OnVictory;
    public event Action<bool> OnHasGunChanged;
    public event Action OnGunShoot;
    public event Action<bool> OnStunChanged;
    public event Action<GameObject, Vector3, Vector3> OnStunVfxSpawnRequested;

    // ============================================================
    // 라이프사이클
    // ============================================================
    private void Start()
    {
        // 오프라인 모드: 서버 콜백이 오지 않으므로 여기서 초기화.
        if (AuthorityGuard.IsOffline)
        {
            remaingLives = maxLives;
            currentHealth = startHealth;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        remaingLives = maxLives;
        currentHealth = startHealth;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // NetworkGameManger가 ICharacterModel을 받도록 리팩터링됨.
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.RegisterPlayer(this);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // UI 매니저 등록 (기존 NetworkCharacterModel 동작 보존)
        var input = GetComponent<StarterAssets.StarterAssetsInputs>();
        var uiManager = FindObjectOfType<InGameUIManger>();
        if (input != null && uiManager != null)
            uiManager.RegisterInput(input);
    }

    // ============================================================
    // RequestXxx — 외부에서 항상 이걸 호출. 내부에서 분기.
    // ============================================================
    public void RequestNextCombo()
    {
        if (AuthorityGuard.IsOffline)
        {
            int old = comboCount;
            comboCount = (comboCount % 4) + 1;
            OnComboChangedHook(old, comboCount);
        }
        else CmdNextCombo();
    }

    public void RequestResetCombo()
    {
        if (AuthorityGuard.IsOffline)
        {
            int old = comboCount;
            comboCount = 0;
            OnComboChangedHook(old, 0);
        }
        else CmdResetCombo();
    }

    public void RequestSetCharging(bool state)
    {
        if (AuthorityGuard.IsOffline)
        {
            bool old = isCharging;
            isCharging = state;
            OnChargeStateChangedHook(old, state);
        }
        else CmdSetCharging(state);
    }

    public void RequestSetChargeReady(bool state)
    {
        if (AuthorityGuard.IsOffline)
        {
            bool old = isChargeReady;
            isChargeReady = state;
            OnChargeReadyChangedHook(old, state);
        }
        else CmdSetChargeReady(state);
    }

    public void RequestStrongAttack()
    {
        if (AuthorityGuard.IsOffline) OnStrongAttack?.Invoke();
        else CmdStrongAttack();
    }

    public void TriggerVictory()
    {
        OnVictory?.Invoke();
    }

    public void RequestSetBowDraw(bool state)
    {
        if (AuthorityGuard.IsOffline)
        {
            bool old = isBowDraw;
            isBowDraw = state;
            OnBowDrawChangedHook(old, state);
        }
        else CmdSetBowDraw(state);
    }

    public void RequestBowRelease()
    {
        if (AuthorityGuard.IsOffline)
        {
            bool oldDraw = isBowDraw;
            isBowDraw = false;
            bowReleaseCount++;
            OnBowDrawChangedHook(oldDraw, false);
            OnBowRelease?.Invoke();
        }
        else CmdBowRelease();
    }

    public void RequestSelfHarm(float damage)
    {
        if (AuthorityGuard.IsOffline) ApplyDamageLocal(damage);
        else CmdSelfHarm(damage);
    }

    public void RequestTakeDamage(float damage)
    {
        if (AuthorityGuard.IsOffline) ApplyDamageLocal(damage);
        else CmdTakeDamage(damage);
    }

    public void RequestFallDamage(float damage)
    {
        if (AuthorityGuard.IsOffline)
        {
            ApplyDamageLocal(damage);
            // 오프라인 리스폰 위치 이동은 LocalSpawnPoint 같은 씬 오브젝트에 맡기거나 생략
        }
        else CmdFallDamage(damage);
    }

    public void RequestSetHasBow(bool state)
    {
        if (AuthorityGuard.IsOffline)
        {
            bool old = hasBow;
            hasBow = state;
            if (!state)
            {
                bool oldDraw = isBowDraw;
                isBowDraw = false;
                OnBowDrawChangedHook(oldDraw, false);
            }
            OnHasBowChangedHook(old, state);
        }
        else
        {
            if (isServer) ServerSetHasBow(state);
            // 클라에서 강제 요청이 필요하면 별도 Cmd를 만들어야 하지만 기존 API가 Server 전용이었음
        }
    }

    // ---- 총 (활과 평행한 구조) ----
    public void RequestSetHasGun(bool state)
    {
        if (AuthorityGuard.IsOffline)
        {
            bool old = hasGun;
            hasGun = state;
            OnHasGunChangedHook(old, state);
        }
        else
        {
            if (isServer) ServerSetHasGun(state);
        }
    }

    /// <summary>
    /// 총 발사 트리거. 활의 RequestBowRelease와 동일한 1회성 이벤트 패턴.
    /// View는 OnGunShoot 이벤트로 애니메이터 트리거를 한 번 발화한다.
    /// </summary>
    public void RequestGunShoot()
    {
        if (AuthorityGuard.IsOffline)
        {
            gunShootCount++;
            OnGunShoot?.Invoke();
        }
        else CmdGunShoot();
    }

    // ---- 스턴 ----
    public void RequestApplyStun(float duration, GameObject vfxPrefab, Vector3 vfxPositionOffset, Vector3 vfxRotationOffset)
    {
        if (duration <= 0f) return;

        if (AuthorityGuard.IsOffline)
        {
            ApplyStunLocal(duration, vfxPrefab, vfxPositionOffset, vfxRotationOffset);
        }
        else
        {
            // 권한자: 서버. 무기(TazorBullet)는 OnTriggerEnter에서 isServer일 때만 호출하지만
            // 안전하게 한 번 더 검사하고 분기.
            if (isServer)
            {
                ApplyStunServer(duration, vfxPrefab, vfxPositionOffset, vfxRotationOffset);
            }
            else
            {
                Debug.LogWarning("[UnifiedCharacterModel] RequestApplyStun: 클라이언트에서 호출됨. 서버에서만 호출해야 함.");
            }
        }
    }

    // ============================================================
    // 오프라인 데미지 로직
    // ============================================================
    private void ApplyDamageLocal(float damage)
    {
        if (isDead) return;

        float old = currentHealth;
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
        }

        OnHealthChangedHook(old, currentHealth);

        if (currentHealth <= 0 && !isDead)
        {

            bool oldDie = isDead;
            isDead = true;
            OnDieHook(oldDie, true);
        }
    }

    // ============================================================
    // 스턴 로직 — 서버/오프라인 권한자 측에서만 실행
    // ============================================================
    [Server]
    private void ApplyStunServer(float duration, GameObject vfxPrefab, Vector3 vfxPosOffset, Vector3 vfxRotOffset)
    {
        // 중복 피격: 더 긴 쪽으로 갱신
        _stunRemainingTime = Mathf.Max(_stunRemainingTime, duration);

        bool wasStunned = isStunned;
        isStunned = true; // SyncVar hook이 모든 클라이언트에서 OnStunChanged 발화

        // VFX 동기화: 모든 클라이언트에 RPC. 프리팹은 이름으로 식별 (StunVfxRegistry 사용).
        string prefabName = vfxPrefab != null ? vfxPrefab.name : null;
        RpcSpawnStunVfx(prefabName, vfxPosOffset, vfxRotOffset);

        // 호스트(서버 + 로컬 클라이언트)에서는 RPC가 두 번 호출되는 것을 피하기 위해
        // 호스트 환경에서는 RpcSpawnStunVfx가 이미 로컬에서 한 번 발화하므로 추가 호출 불필요.
        // → Mirror에서 [ClientRpc]는 호스트의 클라이언트에서도 자동 실행됨.

        if (_stunCoroutine == null)
            _stunCoroutine = StartCoroutine(ServerStunTimer());
    }

    [Server]
    private IEnumerator ServerStunTimer()
    {
        // 워치독: 코루틴이 살아있는 절대 시간. 갱신과 무관하게 이 시간이 지나면 강제 해제.
        float elapsed = 0f;

        while (_stunRemainingTime > 0f)
        {
            float dt = Time.deltaTime;
            _stunRemainingTime -= dt;
            elapsed += dt;
            if (elapsed >= StunAbsoluteCap)
            {
                Debug.LogWarning($"[{nameof(UnifiedCharacterModel)}] 스턴 워치독 발동: {StunAbsoluteCap}초 상한 도달 → 강제 해제");
                break;
            }
            yield return null;
        }

        _stunRemainingTime = 0f;
        _stunCoroutine = null;
        isStunned = false; // SyncVar hook으로 클라이언트 전파
    }

    private void ApplyStunLocal(float duration, GameObject vfxPrefab, Vector3 vfxPosOffset, Vector3 vfxRotOffset)
    {
        _stunRemainingTime = Mathf.Max(_stunRemainingTime, duration);

        bool wasStunned = isStunned;
        isStunned = true;
        if (!wasStunned) OnStunChangedHook(false, true);

        // 오프라인: VFX 이벤트를 직접 발화 (RPC 없음)
        OnStunVfxSpawnRequested?.Invoke(vfxPrefab, vfxPosOffset, vfxRotOffset);

        if (_stunCoroutine == null)
            _stunCoroutine = StartCoroutine(LocalStunTimer());
    }

    private IEnumerator LocalStunTimer()
    {
        float elapsed = 0f;

        while (_stunRemainingTime > 0f)
        {
            float dt = Time.deltaTime;
            _stunRemainingTime -= dt;
            elapsed += dt;
            if (elapsed >= StunAbsoluteCap)
            {
                Debug.LogWarning($"[{nameof(UnifiedCharacterModel)}] 스턴 워치독 발동: {StunAbsoluteCap}초 상한 도달 → 강제 해제");
                break;
            }
            yield return null;
        }

        _stunRemainingTime = 0f;
        _stunCoroutine = null;
        bool oldStunned = isStunned;
        isStunned = false;
        if (oldStunned) OnStunChangedHook(true, false);
    }

    [ClientRpc]
    private void RpcSpawnStunVfx(string prefabName, Vector3 posOffset, Vector3 rotOffset)
    {
        var prefab = StunVfxRegistry.Resolve(prefabName);
        OnStunVfxSpawnRequested?.Invoke(prefab, posOffset, rotOffset);
    }

    // ============================================================
    // Mirror Cmd / Rpc / Server (네트워크 모드 전용)
    // ============================================================
    [Command] private void CmdNextCombo() { comboCount = (comboCount % 4) + 1; }
    [Command] private void CmdResetCombo() { comboCount = 0; }
    [Command] private void CmdSetCharging(bool s) { isCharging = s; }
    [Command] private void CmdSetChargeReady(bool s) { isChargeReady = s; }
    [Command] private void CmdStrongAttack() { RpcPlayStrongAttack(); }
    [Command] private void CmdSetBowDraw(bool s) { isBowDraw = s; }
    [Command] private void CmdBowRelease() { isBowDraw = false; bowReleaseCount++; }
    [Command] private void CmdGunShoot() { gunShootCount++; }

    [Command(requiresAuthority = false)]
    private void CmdTakeDamage(float amount)
    {
        if (IsDead) return;
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
        }
    }

    [Command]
    private void CmdSelfHarm(float amount)
    {
        Debug.Log($"Self Harm: {amount} damage");
        CmdTakeDamage(amount);
    }

    [Command]
    private void CmdFallDamage(float amount)
    {
        if (IsDead) return;
        CmdTakeDamage(amount);

        // 리스폰 위치 이동 (서버 권위)
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.RespawnPlayer(this);
    }

    [Server]
    public void ServerSetHasBow(bool state)
    {
        hasBow = state;
        if (!state) isBowDraw = false;
    }

    [Server]
    public void ServerSetHasGun(bool state)
    {
        hasGun = state;
    }

    [ClientRpc]
    private void RpcPlayStrongAttack() { OnStrongAttack?.Invoke(); }

    [Command(requiresAuthority = false)]
    public void CmdSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex)
    {
        RpcSpawnHitEffect(hitPoint, hitNormal, effectIndex);
    }

    [ClientRpc]
    private void RpcSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex)
    {
        if (hitEffectPrefabs == null || effectIndex >= hitEffectPrefabs.Length) return;
        var prefab = hitEffectPrefabs[effectIndex];
        if (prefab == null) return;

        var effect = Instantiate(prefab, hitPoint, Quaternion.LookRotation(hitNormal));
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear();
            ps.Play();
        }
        Destroy(effect, effectDuration);
    }

    [ClientRpc]
    private void RpcNotifyGameOver()
    {
        OnGameOver?.Invoke();

        // GameManager에 게임 오버 알림 (ICharacterModel 수용)
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.OnPlayerGameOver(this);
    }

    // ============================================================
    // SyncVar 훅 — 오프라인에서도 수동으로 호출됨
    // ============================================================
    private void OnComboChangedHook(int oldV, int newV) => OnComboChanged?.Invoke(newV);
    private void OnChargeStateChangedHook(bool oldV, bool newV) => OnChargeStateChanged?.Invoke(newV);
    private void OnChargeReadyChangedHook(bool oldV, bool newV) => OnChargeReadyChanged?.Invoke(newV);
    private void OnHasBowChangedHook(bool oldV, bool newV) => OnHasBowChanged?.Invoke(newV);
    private void OnBowDrawChangedHook(bool oldV, bool newV) => OnBowDrawChanged?.Invoke(newV);
    private void OnBowReleaseHook(int oldV, int newV) => OnBowRelease?.Invoke();
    private void OnHasGunChangedHook(bool oldV, bool newV) => OnHasGunChanged?.Invoke(newV);
    private void OnGunShootHook(int oldV, int newV) => OnGunShoot?.Invoke();
    private void OnLivesChangedHook(int oldV, int newV) => OnLivesChanged?.Invoke(newV);
    private void OnStunChangedHook(bool oldV, bool newV) => OnStunChanged?.Invoke(newV);

    private void OnHealthChangedHook(float oldV, float newV)
    {
        Debug.Log($"Health Changed: {oldV} -> {newV}");
        OnHealthChanged?.Invoke(newV);
    }

    private void OnDieHook(bool oldV, bool newV)
    {
        if (newV == true && oldV == false)
        {
            OnDie?.Invoke();

            if (AuthorityGuard.IsOffline)
            {
                // 오프라인: 로컬에서 목숨 차감 및 리스폰 처리
                remaingLives--;
                OnLivesChanged?.Invoke(remaingLives);
                if (remaingLives > 0) StartCoroutine(OfflineRespawnCoroutine());
                else OnGameOver?.Invoke();
            }
            else if (isServer)
            {
                // 네트워크: 서버에서 목숨 관리
                remaingLives--;
                if (remaingLives > 0) StartCoroutine(ServerRespawnCoroutine());
                else RpcNotifyGameOver();
            }
        }
        if (newV == false && oldV == true)
        {
            OnRespawn?.Invoke();
        }
    }

    private IEnumerator OfflineRespawnCoroutine()
    {
        yield return new WaitForSeconds(2f);
        bool oldDie = isDead;
        currentHealth = startHealth;
        isDead = false;
        OnHealthChangedHook(0, currentHealth);
        OnDieHook(oldDie, false);
    }

    [Server]
    private IEnumerator ServerRespawnCoroutine()
    {
        yield return new WaitForSeconds(2f);
        currentHealth = startHealth;
        isDead = false;

        // 위치 리스폰 (ICharacterModel 수용)
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.RespawnPlayer(this);
    }

    // ============================================================
    // RequestSpawnHitEffect — ICharacterModel 요구. 오프라인/네트워크 분기.
    // ============================================================
    public void RequestSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex)
    {
        if (AuthorityGuard.IsOffline)
        {
            SpawnHitEffectLocal(hitPoint, hitNormal, effectIndex);
        }
        else
        {
            CmdSpawnHitEffect(hitPoint, hitNormal, effectIndex);
        }
    }

    private void SpawnHitEffectLocal(Vector3 hitPoint, Vector3 hitNormal, int effectIndex)
    {
        if (hitEffectPrefabs == null || effectIndex >= hitEffectPrefabs.Length) return;
        var prefab = hitEffectPrefabs[effectIndex];
        if (prefab == null) return;

        var effect = Instantiate(prefab, hitPoint, Quaternion.LookRotation(hitNormal));
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear();
            ps.Play();
        }
        Destroy(effect, effectDuration);
    }
}

/// <summary>
/// 스턴 VFX 프리팹 이름 → 프리팹 객체 매핑.
/// VFX 프리팹을 NetworkServer.Spawn하지 않고 각 클라이언트가 로컬 Instantiate하기 위해 필요.
/// TazorBullet 등 VFX를 보유한 컴포넌트가 Awake에서 자신의 프리팹을 등록한다.
/// </summary>
public static class StunVfxRegistry
{
    private static readonly Dictionary<string, GameObject> _map = new Dictionary<string, GameObject>();

    public static void Register(GameObject prefab)
    {
        if (prefab == null) return;
        _map[prefab.name] = prefab;
    }

    public static GameObject Resolve(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        _map.TryGetValue(name, out var prefab);
        return prefab;
    }
}