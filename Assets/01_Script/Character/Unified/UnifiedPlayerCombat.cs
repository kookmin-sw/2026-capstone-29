using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerCombat + LocalPlayerCombat 를 합친 전투 입력 처리기.
/// ICharacterModel만 호출하므로 네트워크/로컬 모드 모두 동일 코드로 동작한다.
/// 드리프트 통합 방침(사용자 확정): "양쪽 다 적용"
///  → HasBow / HasGun 가드를 로컬 모드에서도 적용.
/// </summary>
public class UnifiedPlayerCombat : MonoBehaviour
{
    private StarterAssetsInputs _input;
    private ICharacterModel _model;
    private UnifiedCharacterView _view;
    private PlayerInput _playerInput;
    private InputAction _chargeAction;
    private Animator _animator;
    private UnifiedThirdPersonController _controller; 

    [Header("공격 설정")]
    [SerializeField] private float comboResetTime = 1.0f;
    [SerializeField] private float strongAttackHoldTime = 1.5f;
    [SerializeField] private float selfHarmDamage = 20f;

    private float _lastAttackTime;
    private bool _chargeStarted = false;

    private void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _model = GetComponent<ICharacterModel>();
        _view = GetComponent<UnifiedCharacterView>();
        _playerInput = GetComponent<PlayerInput>();
        _animator = GetComponent<Animator>();
        _controller = GetComponent<UnifiedThirdPersonController>();
        if (_playerInput != null && _playerInput.actions != null)
        {
            _chargeAction = _playerInput.actions["Charge"];
        }
    }

    private void OnEnable()
    {
        if (_chargeAction != null)
        {
            _chargeAction.started += OnChargeStarted;
            _chargeAction.performed += OnChargeReady;
            _chargeAction.canceled += OnChargeCanceled;
        }
        else
        {
            Debug.LogError("=== _chargeAction이 NULL!");
        }
    }

    private void OnDisable()
    {
        if (_chargeAction != null)
        {
            _chargeAction.started -= OnChargeStarted;
            _chargeAction.performed -= OnChargeReady;
            _chargeAction.canceled -= OnChargeCanceled;
        }
    }

    private void Update()
    {
        // 권위 검사: 네트워크면 isLocalPlayer, 오프라인이면 항상 true
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (_model == null || _model.IsDead) return;

        HandlePunch();
        HandleSelfHarm();
        CheckComboTimer();
    }

    private void HandlePunch()
    {
        // 원거리 무기(활/총/폭탄) 장착 중이면 펀치(콤보) 금지.
        // 좌클릭은 무기 본인(UnifiedWeaponBow / UnifiedWeaponTazorGun / UnifiedWewaponBomb)이 직접 잡아서
        // 발사/차징 로직으로 보낸다. 콤보 시스템과의 이중 발화를 막기 위해 여기서 게이팅.
        if (_animator.GetBool("HasBow")) return;
        if (_animator.GetBool("HasGun")) return;
        if (_animator.GetBool("HasBomb")) return;

        if (!_input.punch) return;

        // 공격 가능 조건: 차지/강공/점프/피격/대쉬 중이 아닐 때
        bool canPunch = !_model.IsCharging
                     && !IsStrongAttacking
                     && !IsJumping
                     && !IsGettingHit
                     && !IsDashing;

        if (canPunch)
        {
            _lastAttackTime = Time.time;
            _model.RequestNextCombo();
        }

        // 가드에 걸렸든 아니든 입력은 소비 — 다음 프레임에 자동 큐잉되는 것 방지
        _input.punch = false;
    }

    private void HandleSelfHarm()
    {
        if (_input.selfHarm)
        {
            _model.RequestSelfHarm(selfHarmDamage);
            _input.selfHarm = false;
        }
    }

    private bool IsAttacking =>
        _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack");

    private bool IsStrongAttacking =>
        _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsTag("StrongAttack");

    // 점프 중: 컨트롤러가 매 프레임 갱신하는 Grounded 파라미터 사용
    private bool IsJumping =>
        _animator != null && !_animator.GetBool("Grounded");

    // 피격 중: Animator 상태에 "Hit" 태그 부여 필요
    private bool IsGettingHit =>
        _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsTag("Hit");

    // 대쉬 중: 컨트롤러 코루틴 플래그를 직접 참조
    private bool IsDashing =>
        _controller != null && _controller.IsDashing;

    private void OnChargeStarted(InputAction.CallbackContext context)
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (_model.IsDead || IsAttacking || IsStrongAttacking) return;

        // 근접무기 장착 중이면: Charge 대신 throwAction 플래그를 세움
        // 무기 본인(UnifiedWeaponMelee.Update)이 다음 프레임에 읽고 소비.
        if (_view != null && _view.HasMeleeWeaponEquipped)
        {
            _input.throwAction = true;
            return;
        }

        _chargeStarted = true;
        _model.RequestSetCharging(true);
        if (_view != null) _view.UpdateChargeEffect(true, false);
    }

    private void OnChargeReady(InputAction.CallbackContext context)
{
    if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
    if (_model.IsDead || IsAttacking || IsStrongAttacking) return;
    if (!_chargeStarted) return;   // ← 추가: Charge가 시작되지 않았으면 무시 (무기 장착 중이었던 경우)

    if (_view != null) _view.UpdateChargeEffect(true, true);
    _input.punch = false;
}

    private void OnChargeCanceled(InputAction.CallbackContext context)
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (_model.IsDead) return;
        if (!_chargeStarted) return;   // ← 추가: Charge가 시작되지 않았으면 무시 (무기 장착 중이었던 경우)
        bool willStrongAttack = _chargeStarted               // ← 여기 추가
                            && !IsAttacking
                            && !IsStrongAttacking
                            && context.duration >= strongAttackHoldTime;



        if (willStrongAttack)
        {
            _model.RequestStrongAttack();
            // IsCharging은 StrongAttack 상태 진입 시 StateMachineBehaviour가 false로 처리.
            // 여기선 모델/이펙트만 정리하고 Animator 파라미터는 건드리지 않는다.
            _chargeStarted = false;
            _model.RequestSetCharging(false);
            if (_view != null) _view.ClearChargeEffectsOnly();
        }
        else
        {
            _chargeStarted = false;
            _model.RequestSetCharging(false);
            if (_view != null) _view.UpdateChargeEffect(false, false);
        }

        _input.punch = false;
    }

    private void CheckComboTimer()
    {
        if (_model.ComboCount > 0 && Time.time > _lastAttackTime + comboResetTime)
        {
            _model.RequestResetCombo();
        }
    }
}