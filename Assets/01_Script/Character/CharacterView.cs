using UnityEngine;

public class CharacterView : MonoBehaviour
{
    private Animator anim;
    private CharacterModel model;

    [Header("이펙트")]
    public GameObject chargingEffect;
    public GameObject chargeReadyEffect;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        model = GetComponent<CharacterModel>();
    }

    private void OnEnable()
    {
        // 모델의 이벤트 구독 (데이터가 변하면 UI/애니메이션 갱신)
        model.OnHealthChanged += HandleHealthChange;
        model.OnDie += HandleDie;
        model.OnComboChanged += HandleCombo;
    }

    private void OnDisable()
    {
        // 구독 해제 (메모리 누수 방지)
        model.OnHealthChanged -= HandleHealthChange;
        model.OnDie -= HandleDie;
        model.OnComboChanged -= HandleCombo;
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
}
