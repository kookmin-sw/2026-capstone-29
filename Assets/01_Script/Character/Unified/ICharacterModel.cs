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
    bool IsStunned { get; }
    bool HasGun { get; }

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
    void RequestSetHasGun(bool state);
    void RequestGunShoot();
    void RequestSpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, int effectIndex);

    /// <summary>
    /// 이 캐릭터에 스턴을 부여한다.
    /// 이미 스턴 중이면 남은 시간을 더 긴 쪽으로 갱신하고 VFX를 재스폰한다.
    /// vfxPrefab은 모든 클라이언트가 알아볼 수 있도록 사전 등록되어 있어야 한다
    /// (TazorBullet 등이 Awake에서 자동 등록).
    /// </summary>
    void RequestApplyStun(float duration, GameObject vfxPrefab, Vector3 vfxPositionOffset, Vector3 vfxRotationOffset);

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
    event Action OnVictory;
    event Action<bool> OnHasGunChanged;
    event Action OnGunShoot;

    /// <summary>
    /// 스턴 상태 변경 이벤트. true이면 스턴 시작, false이면 해제.
    /// View는 이 이벤트로 애니메이터 Bool/입력 차단을 토글한다.
    /// </summary>
    event Action<bool> OnStunChanged;

    /// <summary>
    /// 스턴 VFX 스폰 요청 이벤트. View가 받아 자식으로 부착한다.
    /// 새 스턴이 시작될 때마다 발화 — 중복 피격으로 갱신될 때도 다시 발화되어
    /// 기존 VFX 파괴 후 새로 스폰되는 효과를 낸다.
    /// </summary>
    event Action<GameObject, Vector3, Vector3> OnStunVfxSpawnRequested;
}