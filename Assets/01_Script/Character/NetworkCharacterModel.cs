using UnityEngine;
using Mirror;
using System;
using Mirror.Examples.Common;
using StarterAssets;

public class NetworkCharacterModel : NetworkBehaviour
{   
    // 콤보 카운트 훅
    [SyncVar(hook = nameof(OnComboChangedHook))]
    private int comboCount = 0;

    // 체력 변경 훅
    [SyncVar(hook = nameof(OnHealthChangedHook))]
    public float currentHealth = 100f;

    // 사망 훅
    [SyncVar(hook = nameof(OnDieHook))] 
    private bool isDead = false;

    // 차징 중 훅
    [SyncVar(hook = nameof(OnChargeStateChangedHook))]
    private bool isCharging = false;

    // 차지 완료 훅
    [SyncVar(hook = nameof(OnChargeReadyChangedHook))]
    private bool isChargeReady = false;

    //활 장착 훅
    [SyncVar(hook = nameof(OnHasBowChangedHook))]
    private bool hasBow = false;

    //활 장전 훅
    [SyncVar(hook = nameof(OnBowDrawChangedHook))]
    private bool isBowDraw = false;
    //활 발사 훅
    [SyncVar(hook = nameof(OnBowReleaseHook))]
    private int bowReleaseCount = 0;

    // 목숨 세팅
    [Header("Lives Setting")]
    public int maxLives = 1;

    // 배열로 변경
    [Header("Hit Effect")]
    public GameObject[] hitEffectPrefabs;  // 0: 오른손, 1: 왼손, 2: 발, 3: --
    public float effectDuration = 2f;

    // 남아있는 목숨 -> 서버에서 클라이언트에 전파
    [SyncVar(hook = nameof(OnLivesChangedHook))]
    public int remaingLives;

    public int ComboCount => comboCount;
    public bool IsCharging => isCharging;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public bool HasBow => hasBow;
    public bool IsChargeReady => isChargeReady;
    public bool IsBowDraw => isBowDraw;

    public event Action<int> OnComboChanged;
    public event Action OnDie;
    public event Action OnStrongAttack;
    public event Action<float> OnHealthChanged;
    public event Action<int> OnLivesChanged;
    public event Action OnGameOver; // 모든 목숨 소진시
    public event Action<bool> OnChargeStateChanged;
    public event Action<bool> OnChargeReadyChanged;
    public event Action<bool> OnHasBowChanged;
    public event Action<bool> OnBowDrawChanged;
    public event Action OnBowRelease;
    public event Action OnRespawn;

    public override void OnStartServer()
    {
        base.OnStartServer();
        remaingLives = maxLives;
    }

    [Command]
    public void CmdNextCombo() { comboCount = (comboCount % 4) + 1; }

    [Command]
    public void CmdResetCombo() { comboCount = 0; }

    [Command]
    public void CmdSetCharging(bool state) { isCharging = state; }

    [Command]
    public void CmdSetChargeReady(bool state) { isChargeReady = state; }

    [Command]
    public void CmdStrongAttack() { RpcPlayStrongAttack(); }


    [Command]
    public void CmdSetBowDraw(bool state) { isBowDraw = state; }

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
        if(IsDead) return;

        // 기존 데미지 함수 재활용
        CmdTakeDamage(damage);

        // 리스폰 위치 이동
        if(NetworkGameManger.instance != null)
        {
            NetworkGameManger.instance.RespawnPlayer(this);
        }
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


    [ClientRpc]
    void RpcPlayStrongAttack() { OnStrongAttack?.Invoke(); }

    void OnComboChangedHook(int oldV, int newV) => OnComboChanged?.Invoke(newV);
    void OnChargeStateChangedHook(bool oldV, bool newV) => OnChargeStateChanged?.Invoke(newV);
    void OnChargeReadyChangedHook(bool oldV, bool newV) => OnChargeReadyChanged?.Invoke(newV); 
    void OnHasBowChangedHook(bool oldV, bool newV) => OnHasBowChanged?.Invoke(newV);
    void OnBowDrawChangedHook(bool oldV, bool newV) => OnBowDrawChanged?.Invoke(newV);
    void OnBowReleaseHook(int oldV, int newV) => OnBowRelease?.Invoke();
    void OnHealthChangedHook(float oldV, float newV) 
    {
        Debug.Log($"Health Changed: {oldV} -> {newV}"); 
        OnHealthChanged?.Invoke(newV);
    }
    
    // 목숨 변경시
    void OnLivesChangedHook(int oldV, int newV) => OnLivesChanged?.Invoke(newV);

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

                // 목숨 남은 경우 부활
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
            OnRespawn?.Invoke(); // 클라이언트에서 실행됨
        }
    }

    // 부활 코루틴
    [Server]
    private System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(2f); // 2초 후 부활
        currentHealth = 100f;
        isDead = false;
        // 위치 이동 추가
        if(NetworkGameManger.instance != null)
        {
            NetworkGameManger.instance.RespawnPlayer(this);
        }
    }

    // 이펙트 연결 
    [Command]
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
 
        // GameManager에 게임 오버 알림
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.OnPlayerGameOver(this);
    }

    // 게임 매니저 연결 - 모든 클라이언트가
    public override void OnStartClient()
    {
        base.OnStartClient();

        // 신 내부의 GameManager를 찾아 자신을 등록
        if(NetworkGameManger.instance != null)
            NetworkGameManger.instance.RegisterPlayer(this);
    }

    // UI 매니저 연결 - 내 플레이어에서만
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // 내 플레이어가 소환됐을 때 UI매니저 등록
        StarterAssetsInputs input = GetComponent<StarterAssetsInputs>();
        InGameUIManger uiManager = FindObjectOfType<InGameUIManger>();
        if(input != null && uiManager != null)
            uiManager.RegisterInput(input);
    }
}