using UnityEngine;
using Mirror;
using StarterAssets;

public class PlayerCombat : NetworkBehaviour
{
    private StarterAssetsInputs _input;
    private NetworkCharacterModel _model;
    private CharacterView _view;

    [Header("공격 설정")]
    [SerializeField] private float comboResetTime = 1.0f;
    [SerializeField] private float chargeTimeNeeded = 1.5f;
    private float _lastAttackTime;
    private float _chargeStartTime;

    private void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _model = GetComponent<NetworkCharacterModel>();
        _view = GetComponent<CharacterView>();
    }

    private void Update()
    {
        if (!isLocalPlayer || _model.IsDead) return;

        HandlePunch();
        HandleCharge();
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

    private void HandleCharge()
    {
        if (_input.charge) // 누르고 있을 때
        {
            if (!_model.IsCharging)
            {
                _model.CmdSetCharging(true);
                _chargeStartTime = Time.time;
            }

            float duration = Time.time - _chargeStartTime;
            bool isReady = (duration >= chargeTimeNeeded);
            
            // 이펙트는 내 화면에서 먼저 즉시 보여줌
            _view.UpdateChargeEffect(true, isReady);
        }
        else if (_model.IsCharging) // 방금 뗐을 때 (입력은 false인데 모델은 아직 charging인 상태)
        {
            float duration = Time.time - _chargeStartTime;

            if (duration >= chargeTimeNeeded)
            {
                _model.CmdStrongAttack(); // 서버를 통해 강공격 트리거
            }

            _model.CmdSetCharging(false);
            _view.UpdateChargeEffect(false, false);
        }
    }

    private void CheckComboTimer()
    {
        if (_model.ComboCount > 0 && Time.time > _lastAttackTime + comboResetTime)
        {
            _model.CmdResetCombo();
        }
    }
}