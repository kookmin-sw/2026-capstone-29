using UnityEngine;

/// <summary>
/// CharacterView + CharacterLocalView를 하나로 합친 뷰.
/// ICharacterModel만 구독하므로 네트워크/로컬 모드 모두에서 동작한다.
/// 드리프트 통합 방침(사용자 확정): "양쪽 다 적용"
///  → 활(Bow) 이벤트/이펙트는 로컬 모드에서도 훅업.
/// </summary>
[RequireComponent(typeof(Animator))]
public class UnifiedCharacterView : MonoBehaviour
{
    private Animator anim;
    private ICharacterModel model;

    [Header("이펙트")]
    public GameObject chargingEffect;
    public GameObject chargeReadyEffect;

    [Header("전투 히트박스")]
    public CharacterHitBox rightHandHitbox;
    public CharacterHitBox leftHandHitbox;
    public CharacterHitBox leftFootHitbox;

    [Header("Sound Settings")]
    public AudioSource audioSource;
    public AudioClip punchSounds;
    public AudioClip hitSounds;
    public AudioClip chargeSounds;
    public AudioClip readySounds;

    // 히트박스 리셋용 스테이트 감시
    private int _prevStateHash;

    // 데미지 판정용 직전 HP (회복/리스폰 시 GetHit 트리거 오발 방지)
    private float _prevHealth = float.MaxValue;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        model = GetComponent<ICharacterModel>();
        if (model == null)
            Debug.LogError($"[{nameof(UnifiedCharacterView)}] ICharacterModel이 같은 오브젝트에 없음.");
    }

    private void OnEnable()
    {
        if (model == null) return;
        _prevHealth = model.CurrentHealth;   // 직전 HP 초기값 동기화
        model.OnComboChanged += HandleCombo;
        model.OnStrongAttack += PlayStrongAttackEffect;
        model.OnHasBowChanged += HandleHasBow;
        model.OnBowDrawChanged += HandleBowDraw;
        model.OnBowRelease += HandleBowRelease;
        model.OnHealthChanged += HandleHealthChange;
        model.OnDie += HandleDie;
        model.OnRespawn += HandleRespawn;
        model.OnVictory  += HandleVictory;
    }

    private void OnDisable()
    {
        if (model == null) return;
        model.OnComboChanged -= HandleCombo;
        model.OnStrongAttack -= PlayStrongAttackEffect;
        model.OnHasBowChanged -= HandleHasBow;
        model.OnBowDrawChanged -= HandleBowDraw;
        model.OnBowRelease -= HandleBowRelease;
        model.OnHealthChanged -= HandleHealthChange;
        model.OnDie -= HandleDie;
        model.OnRespawn -= HandleRespawn;
        model.OnVictory  -= HandleVictory;
    }

    private void Update()
    {
        var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool useRootMotion = stateInfo.IsName("Combo Attack 4")
                        || stateInfo.IsTag("Stun")
                        || stateInfo.IsTag("Hit")
                        || stateInfo.IsTag("Die");
        anim.applyRootMotion = useRootMotion;

        if (stateInfo.fullPathHash != _prevStateHash)
        {
            _prevStateHash = stateInfo.fullPathHash;
            ResetAllHitboxes();
        }
    }

    private void ResetAllHitboxes()
    {
        if (rightHandHitbox != null) rightHandHitbox.ResetHitbox();
        if (leftHandHitbox != null) leftHandHitbox.ResetHitbox();
        if (leftFootHitbox != null) leftFootHitbox.ResetHitbox();
    }

    // -------- 이벤트 핸들러 --------

    private void HandleHealthChange(float hp)
    {
        // HP가 감소했고, 살아있을 때만 데미지로 판정.
        // → 리스폰/회복 시 GetHit 오발 방지, 사망 중 추가 GetHit 방지.
        bool tookDamage = hp < _prevHealth && hp > 0;
        _prevHealth = hp;

        if (tookDamage && !model.IsDead)
        {
            anim.SetTrigger("GetHit");
            if (audioSource != null)
            {
                audioSource.PlayOneShot(punchSounds);
                audioSource.PlayOneShot(hitSounds);
            }
        }
    }

    private void HandleDie() => anim.SetBool("Die", true);
    private void HandleRespawn()
    {
        anim.ResetTrigger("GetHit");
        anim.ResetTrigger("SelfHarm");
        anim.SetBool("Die", false);
        anim.Play("Movement");
    }

    private void HandleVictory()
    {

        anim.SetFloat("Speed", 0f); // 이동 정지
        anim.SetTrigger("Victory");
    }

    private void HandleCombo(int step)
    {
        if (step > 0)
        {
            anim.ResetTrigger("AttackTrigger");
            anim.SetInteger("ComboStep", step);
            anim.SetTrigger("AttackTrigger");
        }
        else
        {
            anim.ResetTrigger("AttackTrigger");
            anim.SetInteger("ComboStep", 0);
        }
    }

    private void HandleHasBow(bool hasBow)
    {
        anim.SetBool("HasBow", hasBow);
        if (!hasBow)
        {
            anim.SetBool("IsDraw", false);
            anim.SetTrigger("BowHasGone");
        }
    }

    private void HandleBowDraw(bool isDraw) => anim.SetBool("IsDraw", isDraw);

    private void HandleBowRelease()
    {
        anim.SetBool("IsDraw", false);
        anim.SetTrigger("BowRelease");
    }

    // -------- 차지 이펙트 (Combat이 직접 호출) --------
    public void UpdateChargeEffect(bool isCharging, bool isReady)
    {
        if (!isCharging)
        {
            if (chargingEffect) chargingEffect.SetActive(false);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
            return;
        }
        if (audioSource != null) audioSource.PlayOneShot(chargeSounds);

        if (isReady)
        {
            if (audioSource != null) audioSource.PlayOneShot(readySounds);
            if (chargingEffect) chargingEffect.SetActive(false);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(true);
        }
        else
        {
            if (chargingEffect) chargingEffect.SetActive(true);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
        }
    }

    public void PlayStrongAttackEffect() => anim.SetTrigger("DoStrongAttack");

    public void UpdateMovementAnimation(float currentSpeed) => anim.SetFloat("Speed", currentSpeed);

    // -------- 애니메이션 이벤트용 히트박스 토글 --------
    public void EnableRightHandHitbox() { if (rightHandHitbox != null) rightHandHitbox.EnableHitbox(); }
    public void DisableRightHandHitbox() { if (rightHandHitbox != null) rightHandHitbox.DisableHitbox(); }
    public void EnableLeftHandHitbox() { if (leftHandHitbox != null) leftHandHitbox.EnableHitbox(); }
    public void DisableLeftHandHitbox() { if (leftHandHitbox != null) leftHandHitbox.DisableHitbox(); }
    public void EnableLeftFootHitbox() { if (leftFootHitbox != null) leftFootHitbox.EnableHitbox(); }
    public void DisableLeftFootHitbox() { if (leftFootHitbox != null) leftFootHitbox.DisableHitbox(); }
}
