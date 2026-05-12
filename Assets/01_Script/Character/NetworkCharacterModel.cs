using UnityEngine;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using Mirror.Examples.Common;
using StarterAssets;

public class NetworkCharacterModel : NetworkBehaviour, ICharacterModel
{
    // ============================================================
    // SyncVars
    // ============================================================
    [SyncVar(hook = nameof(OnComboChangedHook))]
    private int comboCount = 0;

    [SyncVar(hook = nameof(OnHealthChangedHook))]
    public float currentHealth = 100f;

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

    [SyncVar(hook = nameof(OnStunChangedHook))]
    private bool isStunned = false;

    [SyncVar(hook = nameof(OnLivesChangedHook))]
    public int remaingLives;

    // ---- 설정 ----
    [Header("Lives Setting")]
    public int maxLives = 1;

    [Header("Hit Effect")]
    public GameObject[] hitEffectPrefabs;  // 0: 오른손, 1: 왼손, 2: 발, 3: --
    public float effectDuration = 2f;

    // ---- 스턴 내부 상태 (서버만 사용) ----
    private float _stunRemainingTime;
    private Coroutine _stunCoroutine;

    /// <summary>
    /// 스턴이 절대 이 시간을 넘기지 못하도록 강제하는 워치독 상한.
    /// 코루틴이 어떤 이유로든 자연 종료되지 못하더라도 이 시간이 지나면 강제 해제된다.
    /// </summary>
    private const float StunAbsoluteCap = 15f;

    // ============================================================
    // ICharacterModel 조회 프로퍼티
    // ============================================================
    public int ComboCount => comboCount;
    public bool IsCharging => isCharging;
    public float CurrentHealth => currentHealth;
    public int RemainingLives => remaingLives;
    public bool IsDead => isDead;
    public bool HasBow => hasBow;
    public bool HasGun => hasGun;
    public bool IsChargeReady => isChargeReady;
    public bool IsBowDraw => isBowDraw;
    public bool IsStunned => isStunned;

    // ============================================================
    // 이벤트
    // ============================================================
    public event Action<int> OnComboChanged;
    public event Action OnDie;
    public event Action OnStrongAttack;
    public event Action<float> OnHealthChanged;
    public event Action<int> OnLivesChanged;
    public event Action OnGameOver;
    public event Action<bool> OnChargeStateChanged;
    public event Action<bool> OnChargeReadyChanged;
    public event Action<bool> OnHasBowChanged;
    public event Action<bool> OnBowDrawChanged;
    public event Action OnBowRelease;
    public event Action OnRespawn;
    public event Action OnVictory;
    public event Action<bool> OnStunChanged;
    public event Action<bool> OnHasGunChanged;
    public event Action OnGunShoot;
    public event Action<GameObject, Vector3, Vector3> OnStunVfxSpawnRequested;

    // ============================================================
    // 라이프사이클
    // ============================================================
    public override void OnStartServer()
    {
        base.OnStartServer();
        remaingLives = maxLives;
    }

    // 게임 매니저 연결 - 모든 클라이언트가
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.RegisterPlayer(this);
    }

    // UI 매니저 연결 - 내 플레이어에서만
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        StarterAssetsInputs input = GetComponent<StarterAssetsInputs>();
        InGameUIManger uiManager = FindObjectOfType<InGameUIManger>();
        if (input != null && uiManager != null)
            uiManager.RegisterInput(input);
    }

    // ============================================================
    // Cmd / Server
    // ============================================================
    [Command] public void CmdNextCombo() { comboCount = (comboCount % 4) + 1; }
    [Command] public void CmdResetCombo() { comboCount = 0; }
    [Command] public void CmdSetCharging(bool state) { isCharging = state; }
    [Command] public void CmdSetChargeReady(bool state) { isChargeReady = state; }
    [Command] public void CmdStrongAttack() { RpcPlayStrongAttack(); }
    [Command] public void CmdSetBowDraw(bool state) { isBowDraw = state; }

    [Command(requiresAuthority = false)]
    public void CmdTakeDamage(float damageAmount)
    {
        if (IsDead) return;

        currentHealth -= damageAmount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
        }
    }

    // 추락 데미지 판정
    [Command]
    public void CmdFallDamage(float damage)
    {
        if (IsDead) return;

        CmdTakeDamage(damage);

        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.RespawnPlayer(this);
    }

    [Server]
    public void ServerSetHasBow(bool state)
    {
        hasBow = state;
        if (!state)
        {
            isBowDraw = false;
        }
    }

    [Server]
    public void ServerSetHasGun(bool state)
    {
        hasGun = state;
    }

    [Command]
    public void CmdSelfHarm(float damageAmount)
    {
        Debug.Log($"Self Harm: {damageAmount} damage");
        CmdTakeDamage(damageAmount);
    }

    [Command]
    public void CmdBowRelease()
    {
        Debug.Log("발사 ");
        isBowDraw = false;
        bowReleaseCount++;
    }

    [Command]
    public void CmdGunShoot() { gunShootCount++; }

    [ClientRpc]
    void RpcPlayStrongAttack() { OnStrongAttack?.Invoke(); }

    // ============================================================
    // 스턴 로직 (서버 권한)
    // ============================================================
    [Server]
    private void ApplyStunServer(float duration, GameObject vfxPrefab, Vector3 vfxPosOffset, Vector3 vfxRotOffset)
    {
        // 중복 피격: 더 긴 쪽으로 갱신
        _stunRemainingTime = Mathf.Max(_stunRemainingTime, duration);

        isStunned = true; // SyncVar hook이 모든 클라이언트에서 OnStunChanged 발화

        // VFX 동기화: 모든 클라이언트에 RPC. 프리팹은 이름으로 식별 (StunVfxRegistry 사용).
        string prefabName = vfxPrefab != null ? vfxPrefab.name : null;
        RpcSpawnStunVfx(prefabName, vfxPosOffset, vfxRotOffset);

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
                Debug.LogWarning($"[{nameof(NetworkCharacterModel)}] 스턴 워치독 발동: {StunAbsoluteCap}초 상한 도달 → 강제 해제");
                break;
            }
            yield return null;
        }

        _stunRemainingTime = 0f;
        _stunCoroutine = null;
        isStunned = false; // SyncVar hook으로 클라이언트 전파
    }

    [ClientRpc]
    private void RpcSpawnStunVfx(string prefabName, Vector3 posOffset, Vector3 rotOffset)
    {
        var prefab = StunVfxRegistry.Resolve(prefabName);
        OnStunVfxSpawnRequested?.Invoke(prefab, posOffset, rotOffset);
    }

    // ============================================================
    // SyncVar hooks
    // ============================================================
    void OnComboChangedHook(int oldV, int newV) => OnComboChanged?.Invoke(newV);
    void OnChargeStateChangedHook(bool oldV, bool newV) => OnChargeStateChanged?.Invoke(newV);
    void OnChargeReadyChangedHook(bool oldV, bool newV) => OnChargeReadyChanged?.Invoke(newV);
    void OnHasBowChangedHook(bool oldV, bool newV) => OnHasBowChanged?.Invoke(newV);
    void OnBowDrawChangedHook(bool oldV, bool newV) => OnBowDrawChanged?.Invoke(newV);
    void OnBowReleaseHook(int oldV, int newV) => OnBowRelease?.Invoke();
    void OnHasGunChangedHook(bool oldV, bool newV) => OnHasGunChanged?.Invoke(newV);
    void OnGunShootHook(int oldV, int newV) => OnGunShoot?.Invoke();
    void OnStunChangedHook(bool oldV, bool newV) => OnStunChanged?.Invoke(newV);
    void OnLivesChangedHook(int oldV, int newV) => OnLivesChanged?.Invoke(newV);

    void OnHealthChangedHook(float oldV, float newV)
    {
        Debug.Log($"Health Changed: {oldV} -> {newV}");
        OnHealthChanged?.Invoke(newV);
    }

    // 사망 시
    void OnDieHook(bool oldV, bool newV)
    {
        if (newV == true && oldV == false)
        {
            OnDie?.Invoke();

            // 목숨 차감은 서버에서만
            if (isServer)
            {
                remaingLives--;

                if (remaingLives > 0)
                {
                    StartCoroutine(RespawnCoroutine());
                }
                else
                {
                    RpcNotifyGameOver();
                }
            }
        }
        if (newV == false && oldV == true)
        {
            OnRespawn?.Invoke();
        }
    }

    // 부활 코루틴
    [Server]
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(2f);
        currentHealth = 100f;
        isDead = false;
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.RespawnPlayer(this);
    }

    [Command(requiresAuthority = false)]
    public void CmdSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex)
    {
        RpcSpawnHitEffect(hitPoint, hitNormal, effectIndex);
    }

    [ClientRpc]
    void RpcSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex)
    {
        if (hitEffectPrefabs == null || effectIndex >= hitEffectPrefabs.Length) return;
        GameObject prefab = hitEffectPrefabs[effectIndex];
        if (prefab == null) return;

        GameObject effect = Instantiate(prefab, hitPoint, Quaternion.LookRotation(hitNormal));
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

        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.OnPlayerGameOver(this);
    }

    // ============================================================
    // ICharacterModel 구현 (기존 Cmd* 메서드로 위임)
    // Unified 체계와의 호환을 위해 추가됨. 기존 동작은 변하지 않음.
    // ============================================================
    public void RequestNextCombo() => CmdNextCombo();
    public void RequestResetCombo() => CmdResetCombo();
    public void RequestSetCharging(bool state) => CmdSetCharging(state);
    public void RequestSetChargeReady(bool state) => CmdSetChargeReady(state);
    public void RequestStrongAttack() => CmdStrongAttack();
    public void RequestSetBowDraw(bool state) => CmdSetBowDraw(state);
    public void RequestBowRelease() => CmdBowRelease();
    public void RequestSelfHarm(float damage) => CmdSelfHarm(damage);
    public void RequestTakeDamage(float damage) => CmdTakeDamage(damage);
    public void RequestFallDamage(float damage) => CmdFallDamage(damage);

    public void RequestSetHasBow(bool state)
    {
        if (isServer) ServerSetHasBow(state);
        // 클라에서 호출된 경우 기존 API가 Server 전용이므로 무시 (기존 동작 유지)
    }

    public void RequestSetHasGun(bool state)
    {
        if (isServer) ServerSetHasGun(state);
    }

    public void RequestGunShoot() => CmdGunShoot();

    public void RequestSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex)
        => CmdSpawnHitEffect(hitPoint, hitNormal, effectIndex);

    public void RequestApplyStun(float duration, GameObject vfxPrefab, Vector3 vfxPositionOffset, Vector3 vfxRotationOffset)
    {
        if (duration <= 0f) return;

        if (isServer)
        {
            ApplyStunServer(duration, vfxPrefab, vfxPositionOffset, vfxRotationOffset);
        }
        else
        {
            // 무기(TazorBullet)는 OnTriggerEnter에서 isServer일 때만 호출하지만
            // 안전하게 한 번 더 검사. 클라이언트에서 들어오면 무시.
            Debug.LogWarning("[NetworkCharacterModel] RequestApplyStun: 클라이언트에서 호출됨. 서버에서만 호출해야 함.");
        }
    }
}