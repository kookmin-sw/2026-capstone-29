using UnityEngine;
using Mirror;
using System;

public class NetworkCharacterModel : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnComboChangedHook))]
    private int comboCount = 0;

    [SyncVar(hook = nameof(OnChargeStateChangedHook))]
    private bool isCharging = false;

    [SyncVar(hook = nameof(OnHealthChangedHook))]
    public float currentHealth = 100f;

    [SyncVar(hook = nameof(OnDieHook))] 
    private bool isDead = false;

    public int ComboCount => comboCount;
    public bool IsCharging => isCharging;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    public event Action<int> OnComboChanged;
    public event Action OnDie;
    public event Action<bool> OnChargeStateChanged;
    public event Action OnStrongAttack;
    public event Action<float> OnHealthChanged;

    [Command]
    public void CmdNextCombo() { comboCount = (comboCount % 3) + 1; }

    [Command]
    public void CmdResetCombo() { comboCount = 0; }

    [Command]
    public void CmdSetCharging(bool state) { isCharging = state; }

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

    [ClientRpc]
    void RpcPlayStrongAttack() { OnStrongAttack?.Invoke(); }

    void OnComboChangedHook(int oldV, int newV) => OnComboChanged?.Invoke(newV);
    void OnChargeStateChangedHook(bool oldV, bool newV) => OnChargeStateChanged?.Invoke(newV);
    void OnHealthChangedHook(float oldV, float newV) => OnHealthChanged?.Invoke(newV);
    void OnDieHook(bool oldV, bool newV)
    {
        if (newV == true && oldV == false) 
        {
            OnDie?.Invoke();
        }
    }

}