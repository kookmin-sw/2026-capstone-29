using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

public class LocalPlayerCombat : MonoBehaviour
{
    private StarterAssetsInputs _input;
    private CharacterModel _model;
    private CharacterLocalView _view;
    private PlayerInput _playerInput;
    private InputAction _chargeAction;

    [Header("공격 설정")]
    [SerializeField] private float comboResetTime = 1.0f;
    private float _lastAttackTime;
    // 현재 공격 모션 중인지 체크용
    private bool _isAttacking = false;

    private void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _model = GetComponent<CharacterModel>();
        _view = GetComponent<CharacterLocalView>();

        _playerInput = GetComponent<PlayerInput>();
        _chargeAction = _playerInput.actions["Charge"];

    }

    private void OnEnable()
    {
        _chargeAction.started += OnChargeStarted;
        _chargeAction.performed += OnChargeReady;
        _chargeAction.canceled += OnChargeCanceled;
    }

    private void OnDisable()
    {
        _chargeAction.started -= OnChargeStarted;
        _chargeAction.performed -= OnChargeReady;
        _chargeAction.canceled -= OnChargeCanceled;
    }

    private void Update()
    {
        if (_model.IsDead) return;

        HandlePunch();
        HandleSelfHarm();
        CheckComboTimer();
    }


    private void HandlePunch()
    {
        if (_input.punch && !_model.IsCharging)
        {
            _lastAttackTime = Time.time;
            _model.NextCombo();   // 서버 Cmd 대신 직접 호출
            _input.punch = false;
        }
    }


    private void HandleSelfHarm()
    {
        if (_input.selfHarm)
        {
            _model.SelfHarm(10f);
            _input.selfHarm = false;
        }
    }


    private void OnChargeStarted(InputAction.CallbackContext context)
    {
        if (_model.IsDead) return;

        _model.SetCharging(true);
        _view.UpdateChargeEffect(true, false);
    }

    private void OnChargeReady(InputAction.CallbackContext context)
    {
        if (_model.IsDead) return;

        _view.UpdateChargeEffect(true, true);
    }

    private void OnChargeCanceled(InputAction.CallbackContext context)
    {
        if (_model.IsDead) return;

        if (context.duration >= 1.5f)
        {
            Debug.Log("Platycombat");
            _model.StrongAttack();   // 서버 Cmd 대신 직접 호출
        }

        _model.SetCharging(false);
        _view.UpdateChargeEffect(false, false);
    }

    private void CheckComboTimer()
    {
        if (_model.ComboCount > 0 && Time.time > _lastAttackTime + comboResetTime)
        {
            _model.ResetCombo();   // 서버 Cmd 대신 직접 호출
        }
    }
    
}