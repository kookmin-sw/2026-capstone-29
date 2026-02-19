using UnityEngine;
using Mirror;
using System;

public class NetworkCharacterModel : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnComboChangedHook))] 
    private int comboCount = 0;
    
    [SyncVar(hook = nameof(OnChargeStateChangedHook))] 
    private bool isCharging = false;

    public int ComboCount => comboCount;
    public bool IsCharging => isCharging;
    public bool IsDead => false; // 임시

    public event Action<int> OnComboChanged;
    public event Action<bool> OnChargeStateChanged;
    public event Action OnStrongAttack;

    [Command]
    public void CmdNextCombo() { comboCount = (comboCount % 3) + 1; }

    [Command]
    public void CmdResetCombo() { comboCount = 0; }

    [Command]
    public void CmdSetCharging(bool state) { isCharging = state; }

    [Command]
    public void CmdStrongAttack() { RpcPlayStrongAttack(); }

    [ClientRpc]
    void RpcPlayStrongAttack() { OnStrongAttack?.Invoke(); }

    void OnComboChangedHook(int oldV, int newV) => OnComboChanged?.Invoke(newV);
    void OnChargeStateChangedHook(bool oldV, bool newV) => OnChargeStateChanged?.Invoke(newV);
}