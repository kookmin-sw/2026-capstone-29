using System;
using UnityEngine;

/// <summary>
/// 네트워크/로컬 모드를 모두 커버하는 캐릭터 모델 인터페이스.
/// 컨트롤러·뷰·콤뱃은 이 인터페이스만 바라보면 됨.
/// 구현체가 내부에서 Cmd 호출 또는 직접 변이로 분기한다.
/// </summary>
public interface ICharacterModel
{
    // ---- 조회 ----
    int ComboCount { get; }
    float CurrentHealth { get; }
    int RemainingLives { get; }
    bool IsDead { get; }
    bool IsCharging { get; }
    bool IsChargeReady { get; }
    bool HasBow { get; }
    bool IsBowDraw { get; }

    // ---- 상태 변경 요청 (구현체가 네트워크/로컬 분기) ----
    void RequestNextCombo();
    void RequestResetCombo();
    void RequestSetCharging(bool state);
    void RequestSetChargeReady(bool state);
    void RequestStrongAttack();
    void RequestSetBowDraw(bool state);
    void RequestBowRelease();
    void RequestSelfHarm(float damage);
    void RequestTakeDamage(float damage);
    void RequestFallDamage(float damage);
    void RequestSetHasBow(bool state);
    void RequestSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex);

    // ---- 이벤트 ----
    event Action<int> OnComboChanged;
    event Action<float> OnHealthChanged;
    event Action<int> OnLivesChanged;
    event Action OnDie;
    event Action OnRespawn;
    event Action OnStrongAttack;
    event Action OnGameOver;
    event Action<bool> OnChargeStateChanged;
    event Action<bool> OnChargeReadyChanged;
    event Action<bool> OnHasBowChanged;
    event Action<bool> OnBowDrawChanged;
    event Action OnBowRelease;
}
