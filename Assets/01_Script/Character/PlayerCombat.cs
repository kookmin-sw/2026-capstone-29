using UnityEngine;
using Mirror;
using StarterAssets;
using UnityEngine.InputSystem;

public class PlayerCombat : NetworkBehaviour
{
    private StarterAssetsInputs _input;
    private NetworkCharacterModel _model;
    private CharacterView _view;
    private PlayerInput _playerInput;
    private InputAction _chargeAction;

    [Header("공격 설정")]
    [SerializeField] private float comboResetTime = 1.0f;
    private float _lastAttackTime;



    private void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _model = GetComponent<NetworkCharacterModel>();
        _view = GetComponent<CharacterView>();
        
        _playerInput = GetComponent<PlayerInput>();
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
        // 차징 중에는 일반 펀치 불가
        if (_input.punch && !_model.IsCharging)
        {
            _lastAttackTime = Time.time;
            _model.CmdNextCombo(); // 서버에 콤보 증가 요청
            _input.punch = false;  // 입력 소비
        }
    }
    
    private void HandleSelfHarm()
    {
        if (_input.selfHarm)
        {
            _model.CmdSelfHarm(1f);
            _input.selfHarm = false;
        }
    }

    private void OnChargeStarted(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || _model.IsDead) return;

        // 누르기 시작: 기 모으기 이펙트(1단계) 켜기
        _model.CmdSetCharging(true);
        _view.UpdateChargeEffect(true, false);
    }

    private void OnChargeReady(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || _model.IsDead) return;

        // 에디터에서 설정한 Hold Time(1.5초) 도달!: 이펙트를 '준비 완료(2단계)'로 변경
        _view.UpdateChargeEffect(true, true); 
    }

    private void OnChargeCanceled(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer || _model.IsDead) return;

        // context.duration을 쓰면 내가 몇 초 동안 누르고 있었는지 정확히 알려줍니다!
        // 에디터에서 Hold Time을 1.5로 했으니, duration이 1.5 이상이면 강공격 발동
        if (context.duration >= 1.5f) 
        {
            Debug.Log("Platycombat");
            _model.CmdStrongAttack(); 
        }

        // 차징 상태 해제 및 이펙트 끄기
        _model.CmdSetCharging(false);
        _view.UpdateChargeEffect(false, false); 
    }


    private void CheckComboTimer()
    {
        if (_model.ComboCount > 0 && Time.time > _lastAttackTime + comboResetTime)
        {
            _model.CmdResetCombo();
        }
    }
}