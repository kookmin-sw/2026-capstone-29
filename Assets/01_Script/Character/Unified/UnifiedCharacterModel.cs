using System;
using System.Collections;
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

    [SyncVar(hook = nameof(OnLivesChangedHook))]
    public int remaingLives; // 오타 유지(기존 호환)

    // ---- 설정 ----
    [Header("Lives Setting")]
    public int maxLives = 1;

    [Header("초기 체력")]
    public float startHealth = 100f;

    [Header("Hit Effect")]
    public GameObject[] hitEffectPrefabs;  // 0: 오른손, 1: 왼손, 2: 발, 3: --
    public float effectDuration = 2f;

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
    // Mirror Cmd / Rpc / Server (네트워크 모드 전용)
    // ============================================================
    [Command] private void CmdNextCombo() { comboCount = (comboCount % 4) + 1; }
    [Command] private void CmdResetCombo() { comboCount = 0; }
    [Command] private void CmdSetCharging(bool s) { isCharging = s; }
    [Command] private void CmdSetChargeReady(bool s) { isChargeReady = s; }
    [Command] private void CmdStrongAttack() { RpcPlayStrongAttack(); }
    [Command] private void CmdSetBowDraw(bool s) { isBowDraw = s; }
    [Command] private void CmdBowRelease() { isBowDraw = false; bowReleaseCount++; }

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

    [ClientRpc]
    private void RpcPlayStrongAttack() { OnStrongAttack?.Invoke(); }

    // 이펙트 스폰 (기존 API 유지)
    [Command]
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
    private void OnLivesChangedHook(int oldV, int newV) => OnLivesChanged?.Invoke(newV);

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
