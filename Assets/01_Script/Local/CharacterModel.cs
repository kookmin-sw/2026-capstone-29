using UnityEngine;
using System;
using System.Collections;

public class CharacterModel : MonoBehaviour
{
    private int comboCount = 0;
    private bool isCharging = false;
    public float currentHealth = 100f;
    private bool isDead = false;

    [Header("Lives Setting")]
    public int maxLives = 1;
    private int remainingLives;

    public int ComboCount => comboCount;
    public bool IsCharging => isCharging;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    public event Action<int> OnComboChanged;
    public event Action OnDie;
    public event Action<bool> OnChargeStateChanged;
    public event Action OnStrongAttack;
    public event Action<float> OnHealthChanged;
    public event Action<int> OnLivesChanged;
    public event Action OnGameOver;
    public event Action OnRespawn;

    private void Start()
    {
        remainingLives = maxLives;
    }

    public void NextCombo()
    {
        comboCount = (comboCount % 4) + 1;
        OnComboChanged?.Invoke(comboCount);
    }

    public void ResetCombo()
    {
        comboCount = 0;
        OnComboChanged?.Invoke(0);
    }

    public void SetCharging(bool state)
    {
        isCharging = state;
        OnChargeStateChanged?.Invoke(state);
    }

    public void StrongAttack()
    {
        OnStrongAttack?.Invoke();
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        float oldHealth = currentHealth;
        currentHealth -= damageAmount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
        }

        Debug.Log($"Health Changed: {oldHealth} -> {currentHealth}");
        OnHealthChanged?.Invoke(currentHealth);

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            HandleDeath();
        }
    }

    public void SelfHarm(float damageAmount)
    {
        Debug.Log($"Self Harm: {damageAmount} damage");
        TakeDamage(damageAmount);
    }


    private void HandleDeath()
    {
        OnDie?.Invoke();

        remainingLives--;
        OnLivesChanged?.Invoke(remainingLives);

        if (remainingLives > 0)
        {
            OnRespawn?.Invoke();
            StartCoroutine(RespawnCoroutine());
        }
        else
        {
            HandleGameOver();
        }
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(2f);
        currentHealth = 100f;
        isDead = false;
        OnHealthChanged?.Invoke(currentHealth);
    }

    private void HandleGameOver()
    {
        OnGameOver?.Invoke();
        Debug.Log("Game Over!");
        
    }

    private void OnEnable()
    {
        
    }
}