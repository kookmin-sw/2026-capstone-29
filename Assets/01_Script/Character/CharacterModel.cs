using UnityEngine;
using System;

public class CharacterModel : MonoBehaviour
{
    [Header("스탯")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }

    [Header("전투 상")]
    [SerializeField] private int comboCount = 0;
    [SerializeField] private bool isCharging = false;

    public int ComboCount => comboCount;
    public bool IsCharging => isCharging;
    public bool IsDead => CurrentHealth <= 0;

    // 이벤트
    public event Action<float> OnHealthChanged;
    public event Action OnDie;
    public event Action<int> OnComboChanged;
    public event Action<bool> OnChargeStateChanged;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHealth -= amount;
        OnHealthChanged?.Invoke(CurrentHealth);

        if (IsDead) OnDie?.Invoke();
    }

    public void SetCharging(bool state)
    {
        isCharging = state;
        OnChargeStateChanged?.Invoke(isCharging);
    }

    public void NextCombo()
    {
        comboCount++;
        if (comboCount > 3) comboCount = 1;
        OnComboChanged?.Invoke(comboCount);
    }

    public void ResetCombo()
    {
        comboCount = 0;
        OnComboChanged?.Invoke(comboCount);
    }
}