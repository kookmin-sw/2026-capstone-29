using UnityEngine;
using Mirror;
using System;
using Mirror.Examples.Common;

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

    // 목숨 세팅
    [Header("Lives Setting")]
    public int maxLives = 1;

    // 남아있는 목숨 -> 서버에서 클라이언트에 전파
    [SyncVar(hook = nameof(OnLivesChangedHook))]
    private int remaingLives;

    public int ComboCount => comboCount;
    public bool IsCharging => isCharging;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public bool IsChargeReady => isChargeReady;

    public event Action<int> OnComboChanged;
    public event Action OnDie;
    public event Action OnStrongAttack;
    public event Action<float> OnHealthChanged;
    public event Action<int> OnLivesChanged;
    public event Action OnGameOver; // 모든 목숨 소진시
    public event Action<bool> OnChargeStateChanged;
    public event Action<bool> OnChargeReadyChanged;

    public override void OnStartServer()
    {
        base.OnStartServer();
        remaingLives = maxLives;
    }

    [Command]
    public void CmdNextCombo() { comboCount = (comboCount % 3) + 1; }

    [Command]
    public void CmdResetCombo() { comboCount = 0; }

    [Command]
    public void CmdSetCharging(bool state) { isCharging = state; }

    [Command]
    public void CmdSetChargeReady(bool state) { isChargeReady = state; }

    [Command]
    public void CmdStrongAttack() { RpcPlayStrongAttack(); }

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

    [Command]
    public void CmdSelfHarm(float damageAmount)
    {
        Debug.Log($"Self Harm: {damageAmount} damage");
        CmdTakeDamage(damageAmount);
    }

    [ClientRpc]
    void RpcPlayStrongAttack() { OnStrongAttack?.Invoke(); }

    void OnComboChangedHook(int oldV, int newV) => OnComboChanged?.Invoke(newV);
    void OnChargeStateChangedHook(bool oldV, bool newV) => OnChargeStateChanged?.Invoke(newV);
    void OnChargeReadyChangedHook(bool oldV, bool newV) => OnChargeReadyChanged?.Invoke(newV);
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
            if(isServer)
            {
                remaingLives--;

                // 목숨 남은 경우 부활
                if(remaingLives > 0)
                {
                    StartCoroutine(RespawnCoroutine());
                }
                else
                {
                    RpcNotifyGameOver();
                }
            }
        }
    }

    // 부활 코루틴
    [Server]
    private System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(2f); // 2초 후 부활
        currentHealth = 100f;
        isDead = false;
    }

    [ClientRpc]
    private void RpcNotifyGameOver()
    {
        OnGameOver?.Invoke();
 
        // GameManager에 게임 오버 알림
        if (NetworkGameManger.instance != null)
            NetworkGameManger.instance.OnPlayerGameOver(this);
    }

    // 게임 매니저 연결
    public override void OnStartClient()
    {
        base.OnStartClient();

        // 신 내부의 GameManager를 찾아 자신을 등록
        if(NetworkGameManger.instance != null)
            NetworkGameManger.instance.RegisterPlayer(this);
    }
}