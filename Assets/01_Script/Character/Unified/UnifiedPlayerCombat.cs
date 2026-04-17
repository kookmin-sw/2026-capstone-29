using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerCombat + LocalPlayerCombat 를 합친 전투 입력 처리기.
/// ICharacterModel만 호출하므로 네트워크/로컬 모드 모두 동일 코드로 동작한다.
/// 드리프트 통합 방침(사용자 확정): "양쪽 다 적용"
///  → HasBow 가드를 로컬 모드에서도 적용.
/// </summary>
public class UnifiedPlayerCombat : MonoBehaviour
{
    private StarterAssetsInputs _input;
    private ICharacterModel _model;
    private UnifiedCharacterView _view;
    private PlayerInput _playerInput;
    private InputAction _chargeAction;
    private Animator _animator;

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

        if (_playerInput != null && _playerInput.actions != null)
            _chargeAction = _playerInput.actions["Charge"];

        if (_model == null)
            Debug.LogError($"[{nameof(UnifiedPlayerCombat)}] ICharacterModel이 없음.");
    }

    private void OnEnable()
    {
        if (_chargeAction != null)
        {
            _chargeAction.started += OnChargeStarted;
            _chargeAction.performed += OnChargeReady;
            _chargeAction.canceled += OnChargeCanceled;
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
        // 활 장착 중이면 펀치 금지 (양쪽 모드 공통 적용)
        if (_animator.GetBool("HasBow")) return;

        if (_input.punch && !_model.IsCharging && !IsStrongAttacking)
        {
            _lastAttackTime = Time.time;
            _model.RequestNextCombo();
            _input.punch = false;
        }
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

    private void OnChargeStarted(InputAction.CallbackContext context)
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (_model.IsDead || IsAttacking || IsStrongAttacking) return;

        _chargeStarted = true;
        _model.RequestSetCharging(true);
        if (_view != null) _view.UpdateChargeEffect(true, false);
    }

    private void OnChargeReady(InputAction.CallbackContext context)
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (_model.IsDead || IsAttacking || IsStrongAttacking) return;

        if (_view != null) _view.UpdateChargeEffect(true, true);
        _input.punch = false;
    }

    private void OnChargeCanceled(InputAction.CallbackContext context)
    {
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (_model.IsDead || IsAttacking || IsStrongAttacking) return;

        if (context.duration >= strongAttackHoldTime)
        {
            _model.RequestStrongAttack();
        }

        _chargeStarted = false;
        _model.RequestSetCharging(false);
        if (_view != null) _view.UpdateChargeEffect(false, false);
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
