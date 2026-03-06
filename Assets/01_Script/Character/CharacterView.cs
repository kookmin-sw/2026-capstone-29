using UnityEngine;

public class CharacterView : MonoBehaviour
{
    private Animator anim;
    private NetworkCharacterModel model;

    [Header("이펙트")]
    public GameObject chargingEffect;
    public GameObject chargeReadyEffect;

    [Header("전투 히트박스")]
    public CharacterHitBox rightHandHitbox;
    public CharacterHitBox leftHandHitbox;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        model = GetComponent<NetworkCharacterModel>();
    }

    private void OnEnable()
    {
        if (model != null)
        {
            model.OnComboChanged += HandleCombo;
            model.OnStrongAttack += PlayStrongAttackEffect;
            model.OnHealthChanged += HandleHealthChange;
            model.OnDie += HandleDie;
        }
    }

    private void OnDisable()
    {
        if (model != null)
        {
            model.OnComboChanged -= HandleCombo;
            model.OnStrongAttack -= PlayStrongAttackEffect;
            model.OnHealthChanged -= HandleHealthChange;
            model.OnDie -= HandleDie;
        }
    }

    // --- 이벤트 핸들러 ---

    void HandleHealthChange(float hp)
    {
        // 체력이 깎였는데 아직 살았으면 피격 모션
        if (hp > 0)
        {
            anim.SetTrigger("GetHit");
        }
    }

    void HandleDie()
    {
        anim.SetBool("Die", true);
    }

    void HandleCombo(int step)
    {
        if (step > 0)
        {
            anim.SetInteger("ComboStep", step);
            anim.SetTrigger("AttackTrigger");
        }
        else
        {
            anim.SetInteger("ComboStep", 0);
        }
    }

    public void UpdateChargeEffect(bool isCharging, bool isReady)
    {
        if (!isCharging)
        {
            // 차지 안 할 땐 둘 다 끄기
            if (chargingEffect) chargingEffect.SetActive(false);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
            return;
        }

        if (isReady)
        {
            // 완료되면: 1단계 끄고 2단계 켜기
            if (chargingEffect) chargingEffect.SetActive(false);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(true);
        }
        else
        {
            // 모으는 중: 1단계 켜고 2단계 끄기
            if (chargingEffect) chargingEffect.SetActive(true);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
        }
    }

    // 컨트롤러가 직접 호출할 수도 있는 메서드 (강공격 등)
    public void PlayStrongAttackEffect()
    {
        anim.SetTrigger("DoStrongAttack");
    }

    public void UpdatePhysicsAnimation(float yVelocity, bool isGrounded)
    {
        anim.SetFloat("VerticalSpeed", yVelocity);
        anim.SetBool("IsGrounded", isGrounded);
    }

    public void UpdateMovementAnimation(float currentSpeed)
    {
        // 0.1보다 크면 달리는 모션 (Blend Tree 사용 시 유용)
        anim.SetFloat("Speed", currentSpeed);
    }

    public void EnableRightPunchHitbox()
    {
        if (rightHandHitbox != null)
            rightHandHitbox.EnableHitbox();
        if (leftHandHitbox != null)
            leftHandHitbox.EnableHitbox();
    }

    // 애니메이션 이벤트가 호출할 함수 2 (주먹 회수할 때 끄기)
    public void DisableRightPunchHitbox()
    {
        if (rightHandHitbox != null)
            rightHandHitbox.DisableHitbox();
        if (leftHandHitbox != null)
            leftHandHitbox.DisableHitbox();
    }
    
    public void EnableLeftPunchHitbox()
    {
        if (leftHandHitbox != null)
            leftHandHitbox.EnableHitbox();
    }

    // 애니메이션 이벤트가 호출할 함수 2 (주먹 회수할 때 끄기)
    public void DisableLeftPunchHitbox()
    {
        if (leftHandHitbox != null) 
            leftHandHitbox.DisableHitbox();
    }
}
