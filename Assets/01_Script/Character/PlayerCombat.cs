using UnityEngine;
using Mirror;
using StarterAssets;
using UnityEngine.InputSystem;

public class PlayerCombat : NetworkBehaviour
{
    private StarterAssetsInputs _input;
    private ICharacterModel _model;
    private CharacterView _view;
    private PlayerInput _playerInput;
    private InputAction _chargeAction;
    private Animator _animator;

    [Header("공격 설정")]
    [SerializeField] private float comboResetTime = 1.0f;
    private float _lastAttackTime;
    // 현재 공격 모션 중인지 체크용
    private bool _isAttacking = false;
    private bool _chargeStarted = false; 
    private bool _isBowDrawing = false;



    private void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _model = GetComponent<ICharacterModel>();
        _view = GetComponent<CharacterView>();

        _playerInput = GetComponent<PlayerInput>();
        _animator = GetComponent<Animator>();
        _chargeAction = _playerInput.actions["Charge"];
    }

    private void OnEnable()
    {
        _chargeAction.started += OnChargeStarted;     // 1. 누르기 시작할 때
        _chargeAction.performed += OnChargeReady;     // 2. 1.5초 홀드 성공했을 때
        _chargeAction.canceled += OnChargeCanceled;   // 3. 버튼에서 손을 뗐을 때
    }

    // ★ 이벤트 해제 (스크립트가 꺼질 때)
    private void OnDisable()
    {
        _chargeAction.started -= OnChargeStarted;
        _chargeAction.performed -= OnChargeReady;
        _chargeAction.canceled -= OnChargeCanceled;
    }

    private void Update()
    {
        if (!isLocalPlayer || _model.IsDead) return;

        HandlePunch();
        HandleSelfHarm();
        CheckComboTimer();
    }

    private void HandlePunch()
    {
        //주먹 나가기 전에 활 부착 여부 확인(추후 무기 추가시 현재 구조 변경 예정.)
        if (_animator.GetBool("HasBow")) return;

        // 차징 중에는 일반 펀치 불가
        if (_input.punch && !_model.IsCharging && !IsStrongAttacking)
        {
            _lastAttackTime = Time.time;
            _model.RequestNextCombo(); // 서버에 콤보 증가 요청 (오프라인이면 로컬 변이)
            _input.punch = false;  // 입력 소비
        }
    }

    private void HandleSelfHarm()
    {
        if (_input.selfHarm)
        {
            _model.RequestSelfHarm(20f);
            _input.selfHarm = false;
        }
    }

    private bool IsAttacking =>
        _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack");

    private bool IsStrongAttacking =>
        _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsTag("StrongAttack");


    private void OnChargeStarted(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || _model.IsDead || IsAttacking || IsStrongAttacking) return;

        // 누르기 시작: 기 모으기 이펙트(1단계) 켜기
        _chargeStarted = true;
        _model.RequestSetCharging(true);
        _view.UpdateChargeEffect(true, false);
    }

    private void OnChargeReady(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || _model.IsDead || IsAttacking || IsStrongAttacking) return;

        // 에디터에서 설정한 Hold Time(1.5초) 도달!: 이펙트를 '준비 완료(2단계)'로 변경
        _view.UpdateChargeEffect(true, true);
        _input.punch = false;
    }

    private void OnChargeCanceled(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || _model.IsDead || IsAttacking || IsStrongAttacking) return;

        // context.duration을 쓰면 내가 몇 초 동안 누르고 있었는지 정확히 알려줍니다!
        // 에디터에서 Hold Time을 1.5로 했으니, duration이 1.5 이상이면 강공격 발동
        if (context.duration >= 1.5f)
        {
            _model.RequestStrongAttack();
        }

        // 차징 상태 해제 및 이펙트 끄기
        _chargeStarted = false;
        _model.RequestSetCharging(false);
        _view.UpdateChargeEffect(false, false);
    }


    private void CheckComboTimer()
    {
        if (_model.ComboCount > 0 && Time.time > _lastAttackTime + comboResetTime)
        {
            _model.RequestResetCombo();
        }
    }
}