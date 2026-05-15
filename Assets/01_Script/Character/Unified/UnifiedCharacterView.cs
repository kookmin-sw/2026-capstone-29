using UnityEngine;
using StarterAssets;

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
    public CharacterHitBox rightFootHitbox;
    public CharacterHitBox leftFootHitbox;

    [Header("스턴 설정")]
    [Tooltip("Animator의 스턴 Bool 파라미터 이름")]
    [SerializeField] private string stunBoolParam = "Stunned";

    [Tooltip("스턴 중 비활성화할 StarterAssetsInputs. 비워두면 자동 탐색.")]
    [SerializeField] private StarterAssetsInputs stunStarterInputs;

    [Header("Sound Settings")]
    public AudioSource audioSource;
    [Tooltip("공격 모션 시 재생되는 사운드 (랜덤 픽)")]
    public AudioClip[] attackSounds;
    [Tooltip("공격 모션 시 재생되는 캐릭터 보이스 (랜덤 픽)")]
    public AudioClip[] attackVoiceSounds;
    [Tooltip("피격 시 재생되는 타격 사운드 (랜덤 픽)")]
    public AudioClip[] hitSounds;
    [Tooltip("피격 시 재생되는 캐릭터 보이스 (랜덤 픽)")]
    public AudioClip[] hitVoiceSounds;
    public AudioClip chargeSounds;
    public AudioClip readySounds;

    [Header("Sound Toggles")]
    [Tooltip("공격 보이스 재생 여부")]
    public bool playAttackVoice = true;
    [Tooltip("피격 보이스 재생 여부")]
    public bool playHitVoice = true;

    [Header("Sound Volumes")]
    [Range(0f, 1f)] public float attackVolume = 1f;
    [Range(0f, 1f)] public float attackVoiceVolume = 1f;
    [Range(0f, 1f)] public float hitVolume = 1f;
    [Range(0f, 1f)] public float hitVoiceVolume = 1f;
    [Range(0f, 1f)] public float chargeVolume = 1f;
    [Range(0f, 1f)] public float readyVolume = 1f;

    // 히트박스 리셋용 스테이트 감시
    private int _prevStateHash;

    // 데미지 판정용 직전 HP (회복/리스폰 시 GetHit 트리거 오발 방지)
    private float _prevHealth = float.MaxValue;

    // 근접무기 장착 시 공격 사운드를 덮어씌우기 위한 슬롯.
    // null이면 캐릭터 기본 attackSounds(맨손 펀치)가 재생됨.
    private UnifiedWeaponMelee _meleeOverride;

    //근접무기 장착 여부 확인. - 던지기 할 때 사용
    public bool HasMeleeWeaponEquipped => _meleeOverride != null;

    //근접무기 인스턴스. 던지기에 사용
    public UnifiedWeaponMelee EquippedMeleeWeapon => _meleeOverride;


    // 현재 활성 스턴 VFX 인스턴스 (재스폰 시 파괴용)
    private GameObject _activeStunVfx;

    /// <summary>
    /// 근접무기가 장착될 때 자기 자신을 등록한다.
    /// 등록되면 HandleCombo에서 캐릭터 기본 펀치 대신 무기 swingSounds가 재생됨.
    /// </summary>
    public void SetMeleeWeapon(UnifiedWeaponMelee weapon) => _meleeOverride = weapon;

    /// <summary>
    /// 근접무기가 해제(만료/던지기/파괴)될 때 호출.
    /// 등록된 무기가 자기 자신일 때만 슬롯을 비운다 — 다른 무기로 교체된 경우 오삭제 방지.
    /// </summary>
    public void ClearMeleeWeapon(UnifiedWeaponMelee weapon)
    {
        if (_meleeOverride == weapon) _meleeOverride = null;
    }

    private void Awake()
    {
        anim = GetComponent<Animator>();
        model = GetComponent<ICharacterModel>();
        if (model == null)
            Debug.LogError($"[{nameof(UnifiedCharacterView)}] ICharacterModel이 같은 오브젝트에 없음.");

        if (stunStarterInputs == null) stunStarterInputs = GetComponent<StarterAssetsInputs>();
        if (stunStarterInputs == null) stunStarterInputs = GetComponentInChildren<StarterAssetsInputs>();
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
        model.OnVictory += HandleVictory;
        model.OnStunChanged += HandleStunChanged;
        model.OnStunVfxSpawnRequested += HandleStunVfxSpawn;
        model.OnHasGunChanged += HandleHasGun;
        model.OnGunShoot += HandleGunShoot;
        model.OnHasBombChanged += HandleHasBomb;
        model.OnMeleeThrow += HandleMeleeThrow; 
        model.OnUseActive += HandleUseActive;
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
        model.OnVictory -= HandleVictory;
        model.OnStunChanged -= HandleStunChanged;
        model.OnStunVfxSpawnRequested -= HandleStunVfxSpawn;
        model.OnHasGunChanged -= HandleHasGun;
        model.OnGunShoot -= HandleGunShoot;
        model.OnHasBombChanged -= HandleHasBomb;
        model.OnMeleeThrow -= HandleMeleeThrow;
        model.OnUseActive -= HandleUseActive;
    }

    private void OnDestroy()
    {
        // 안전망: 스턴 중에 오브젝트가 파괴되어도 입력은 반드시 복구.
        if (stunStarterInputs != null) stunStarterInputs.enabled = true;
        if (_activeStunVfx != null) Destroy(_activeStunVfx);
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
                PlayRandom(hitSounds, hitVolume);
                if (playHitVoice) PlayRandom(hitVoiceSounds, hitVoiceVolume);
            }
        }
    }

    /// <summary>
    /// 사운드 클립 배열에서 랜덤으로 하나를 골라 재생.
    /// </summary>
    private void PlayRandom(AudioClip[] clips, float volume = 1f)
    {
        if (audioSource == null) return;
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) audioSource.PlayOneShot(clip, volume);
    }

    private void HandleDie() => anim.SetBool("Die", true);
    private void HandleRespawn()
    {
        anim.ResetTrigger("GetHit");
        anim.ResetTrigger("SelfHarm");
        anim.SetBool("Die", false);
        anim.Play("Movement");

        // 리스폰 시 스턴 상태도 안전하게 해제
        ClearStunVfx();
        if (stunStarterInputs != null) stunStarterInputs.enabled = true;
        if (!string.IsNullOrEmpty(stunBoolParam)) anim.SetBool(stunBoolParam, false);
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

            // 공격 모션 시작과 동시에 사운드 재생
            // 근접무기 장착 중이면 무기 swingSounds, 아니면 캐릭터 기본 펀치
            if (_meleeOverride != null)
                _meleeOverride.PlaySwingSound();
            else
                PlayRandom(attackSounds, attackVolume);

            if (playAttackVoice) PlayRandom(attackVoiceSounds, attackVoiceVolume);
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

    private void HandleHasGun(bool hasGun)
    {
        anim.SetBool("HasGun", hasGun);
        if (!hasGun) anim.ResetTrigger("GunShot");
    }

    
    private void HandleGunShoot()
    {
        Debug.Log($"[UnifiedCharacterView] HandleGunShoot 진입. HasGun={anim.GetBool("HasGun")}, " +
                  $"current state hash={anim.GetCurrentAnimatorStateInfo(0).fullPathHash}");
        anim.SetTrigger("GunShot");
    }

    private void HandleHasBomb(bool hasBomb)
    {
        anim.SetBool("HasBomb", hasBomb);
    }

    private void HandleStunChanged(bool stunned)
    {
        if (!string.IsNullOrEmpty(stunBoolParam))
            anim.SetBool(stunBoolParam, stunned);

        if (stunStarterInputs != null)
            stunStarterInputs.enabled = !stunned;

        if (!stunned) ClearStunVfx();
    }

    private void HandleStunVfxSpawn(GameObject prefab, Vector3 posOffset, Vector3 rotOffset)
    {
        ClearStunVfx();
        if (prefab == null) return;

        _activeStunVfx = Instantiate(prefab, transform);
        _activeStunVfx.transform.localPosition = posOffset;
        _activeStunVfx.transform.localRotation = Quaternion.Euler(rotOffset);
    }

    private void HandleMeleeThrow()
    {
        anim.ResetTrigger("Throw");
        anim.SetTrigger("Throw");
    }

    private void ClearStunVfx()
    {
        if (_activeStunVfx != null)
        {
            Destroy(_activeStunVfx);
            _activeStunVfx = null;
        }
    }

    private void HandleUseActive()
    {
        anim.ResetTrigger("UseActive");
        anim.SetTrigger("UseActive");
    }

    // -------- 차지 이펙트 (Combat이 직접 호출) --------
    public void UpdateChargeEffect(bool isCharging, bool isReady)
    {
        anim.SetBool("IsCharging", isCharging);

        if (!isCharging)
        {
            if (chargingEffect) chargingEffect.SetActive(false);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
            return;
        }
        if (audioSource != null && chargeSounds != null) audioSource.PlayOneShot(chargeSounds, chargeVolume);

        if (isReady)
        {
            if (audioSource != null && readySounds != null) audioSource.PlayOneShot(readySounds, readyVolume);
            if (chargingEffect) chargingEffect.SetActive(false);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(true);
        }
        else
        {
            if (chargingEffect) chargingEffect.SetActive(true);
            if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
        }
    }
    public void ClearChargeEffectsOnly()
    {
        if (chargingEffect) chargingEffect.SetActive(false);
        if (chargeReadyEffect) chargeReadyEffect.SetActive(false);
    }

    public void PlayStrongAttackEffect() => anim.SetTrigger("DoStrongAttack");

    public void UpdateMovementAnimation(float currentSpeed) => anim.SetFloat("Speed", currentSpeed);

    // -------- 애니메이션 이벤트용 히트박스 토글 --------
    public void EnableRightHandHitbox() { if (rightHandHitbox != null) rightHandHitbox.EnableHitbox(); }
    public void DisableRightHandHitbox() { if (rightHandHitbox != null) rightHandHitbox.DisableHitbox(); }
    public void EnableLeftHandHitbox() { if (leftHandHitbox != null) leftHandHitbox.EnableHitbox(); }
    public void DisableLeftHandHitbox() { if (leftHandHitbox != null) leftHandHitbox.DisableHitbox(); }
    public void EnableRightFootHitbox() { if (leftFootHitbox != null) rightFootHitbox.EnableHitbox(); }
    public void DisableRightFootHitbox() { if (leftFootHitbox != null) rightFootHitbox.DisableHitbox(); }
    public void EnableLeftFootHitbox() { if (leftFootHitbox != null) leftFootHitbox.EnableHitbox(); }
    public void DisableLeftFootHitbox() { if (leftFootHitbox != null) leftFootHitbox.DisableHitbox(); }
}