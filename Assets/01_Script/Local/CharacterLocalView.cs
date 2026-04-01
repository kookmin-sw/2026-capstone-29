using UnityEngine;

public class CharacterLocalView : MonoBehaviour
{
    private Animator anim;
    private CharacterModel model;

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

    // 히트박스 초기화
    private int _prevStateHash;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        model = GetComponent<CharacterModel>();
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
    private void Update()
    {
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        // 애니매이션의 이동을 따름
        bool useRootMotion = stateInfo.IsName("Movement") ||
                            stateInfo.IsName("Jumping") ||
                            stateInfo.IsName("Falling") ||
                            stateInfo.IsName("Quickshift") ||
                            stateInfo.IsName("Combo Attack 4") ||
                            stateInfo.IsName("Crouch");

        anim.applyRootMotion = useRootMotion;
        // 이전 프레임과 스테이트가 바뀌었으면 히트박스 리셋
        if (stateInfo.fullPathHash != _prevStateHash)
        {
            _prevStateHash = stateInfo.fullPathHash;
            ResetAllHitboxes();
        }
    }


    private void ResetAllHitboxes()
    {
        // 히트박스 콜라이더는 끄되, 피격 목록만 초기화 (다음 공격 준비)
        if (rightHandHitbox != null) rightHandHitbox.ResetHitbox();
        if (leftHandHitbox  != null) leftHandHitbox.ResetHitbox();
        if (leftFootHitbox  != null) leftFootHitbox.ResetHitbox();
        // 검/방패 히트박스도 있다면 여기에 추가
    }


    void HandleHealthChange(float hp)
    {
        // 체력이 깎였는데 아직 살았으면 피격 모션
        if (hp > 0)
        {
            anim.SetTrigger("GetHit");
            if (audioSource != null)
            {
                audioSource.PlayOneShot(punchSounds);
                audioSource.PlayOneShot(hitSounds);
            }
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

    public void UpdateChargeEffect(bool isCharging, bool isReady)
    {
        if (!isCharging)
        {

            // 차지 안 할 땐 둘 다 끄기
            if (chargingEffect) chargingEffect.SetActive(false);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
            return;
        }
        if (audioSource != null)
            audioSource.PlayOneShot(chargeSounds);

        if (isReady)
        {
            // 완료되면: 1단계 끄고 2단계 켜기
            if (audioSource != null)
                audioSource.PlayOneShot(readySounds);
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


    public void UpdateMovementAnimation(float currentSpeed)
    {
        // 0.1보다 크면 달리는 모션 (Blend Tree 사용 시 유용)
        anim.SetFloat("Speed", currentSpeed);
    }

    public void EnableRightHandHitbox()
    {
        if (rightHandHitbox != null)
            rightHandHitbox.EnableHitbox();

    }

    public void DisableRightHandHitbox()
    {
        if (rightHandHitbox != null)
            rightHandHitbox.DisableHitbox();
            
    }

    public void EnableLeftHandHitbox()
    {
        if (leftHandHitbox != null)
            leftHandHitbox.EnableHitbox();
    }

    public void DisableLeftHandHitbox()
    {
        if (leftHandHitbox != null)
            leftHandHitbox.DisableHitbox();
    }
    public void EnableLeftFootHitbox()
    {
        if (leftFootHitbox != null)
            leftFootHitbox.EnableHitbox();
    }

    public void DisableLeftFootHitbox()
    {
        if (leftFootHitbox != null)
            leftFootHitbox.DisableHitbox();
    }
}
