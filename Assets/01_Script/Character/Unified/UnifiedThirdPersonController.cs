using UnityEngine;
using Mirror;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// ThirdPersonController + LocalThirdPersonController 를 합친 통합 컨트롤러.
    /// - Mirror 실행 중이면: isLocalPlayer 기준으로 동작.
    /// - Mirror 미실행(오프라인) 시: 항상 본인 캐릭터로 간주.
    /// - 회전 수식은 Inspector 토글(RotationMode)로 선택 (Network 방식 / Local 방식).
    /// 드리프트 통합 방침(사용자 확정): "양쪽 다 적용" + "회전 수식은 Inspector 옵션".
    /// </summary>
    public enum CharacterRotationMode
    {
        /// <summary>기존 ThirdPersonController: _mainCamera.eulerAngles.y 직접 사용 (일반 3인칭).</summary>
        CameraYaw = 0,
        /// <summary>기존 LocalThirdPersonController: cameraToPlayer 벡터 기반 상대 yaw (아레나/탑다운형).</summary>
        CameraToPlayer = 1
    }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkIdentity))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class UnifiedThirdPersonController : NetworkBehaviour
    {
        [Header("Rotation Mode (Inspector에서 선택)")]
        [Tooltip("CameraYaw: 일반 3인칭. CameraToPlayer: 아레나/탑다운형.")]
        public CharacterRotationMode rotationMode = CharacterRotationMode.CameraYaw;

        [Header("Player")]
        public float MoveSpeed = 2.0f;
        public float CrouchSpeed = 3.0f;
        public float JumpMoveSpeed = 4.0f;
        [Tooltip("공격 중 이동 속도")] public float AttackMoveSpeed = 1.0f;

        [Header("Distance-Based Chase Boost (추격 거리 보정)")]
        [Tooltip("거리 기반 추격 속도 보정 활성화")]
        public bool EnableDistanceSpeedBoost = true;

        [Tooltip("이 거리 안에서는 보정 OFF (격투 거리 보호)")]
        public float ChaseBoostMinDistance = 5.0f;

        [Tooltip("이 거리에서 최대 보정 배율 도달")]
        public float ChaseBoostMaxDistance = 20.0f;

        [Tooltip("최대 속도 배율 (1.4 = 40% 더 빠름)")]
        [Range(1.0f, 3.0f)]
        public float ChaseBoostMaxMultiplier = 1.4f;

        [Tooltip("보정 곡선 지수. 1=선형, <1 빠르게 증가(ease-out), >1 천천히 증가(ease-in)")]
        [Range(0.1f, 3.0f)]
        public float ChaseBoostCurveExponent = 0.7f;

        [Tooltip("상대를 향해 이동할 때만 보정 적용 (체크 시 도망자는 보정 X)")]
        public bool ChaseBoostOnlyWhenApproaching = true;

        [Tooltip("접근 각도에 따라 보정량 조절. 정면일수록 강하게.")]
        public bool ChaseBoostScaleByAngle = true;

        [Tooltip("공격 모션 중에는 보정 OFF (콤보 거리감 보호)")]
        public bool ChaseBoostDisableDuringAttack = true;

        [Header("Shift Dash Settings")]
        public float ShiftCooldown = 1.0f;
        [Tooltip("대시로 이동할 고정 거리 (m)")] public float ShiftDashDistance = 5.0f;
        [Tooltip("대시 지속 시간 (초)")] public float ShiftDashDuration = 0.3f;

        [Range(0.0f, 0.3f)] public float RotationSmoothTime = 0.12f;
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;

        //중력 Scale 변환 시, 기존의 중력 스케일 및 점프 높이를 저장하는 변수
        private float _baseJumpHeight;       // 인스펙터에서 설정한 원본 값 (Awake 시 캐시)
        private float _baseGravity;
        private bool _baseValuesCached = false;

        //Dictionary로 관리하여 원본 중력의 오염을 방지
        private readonly System.Collections.Generic.Dictionary<object, (float jumpMul, float gravityMul)> _gravityModifiers = new System.Collections.Generic.Dictionary<object, (float, float)>();

        [Space(10)]
        public float JumpTimeout = 0.50f;
        public float FallTimeout = 0.15f;

        [Header("Multi Jump")]
        [Tooltip("멀티 점프(더블 점프 등) 활성화. 끄면 기존처럼 지상 점프만 가능.")]
        public bool EnableMultiJump = false;
        [Tooltip("최대 점프 횟수. 첫(지상) 점프를 포함한 총 횟수. 2 = 더블 점프, 3 = 트리플 점프.")]
        [Min(1)]
        public int MaxJumpCount = 2;
        [Tooltip("연속 점프(공중 점프 포함) 사이 최소 간격(초). 0이면 입력 들어오는 즉시 다음 점프.")]
        [Min(0f)]
        public float MultiJumpInterval = 0.15f;
        [Tooltip("절벽에서 걸어 떨어졌을 때 첫 점프를 '지상 점프'로 인정해 멀티 점프 횟수를 깎을지 여부. 체크 시 walk-off → 한 번 점프하면 공중 점프 1회만 남음. 해제 시 walk-off 후에도 풀(MaxJumpCount) 점프 가능.")]
        public bool ConsumeJumpOnWalkOff = false;

        // 남은 점프 횟수. 착지 시 MaxJumpCount 로 리셋되고 점프할 때마다 -1.
        private int _jumpCountRemaining;
        // 직전 프레임의 Grounded 상태 (walk-off 감지용)
        private bool _wasGroundedLastFrame = true;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 70.0f;
        public float BottomClamp = -30.0f;
        public float CameraAngleOverride = 0.0f;
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        [SyncVar] private float _speedMultiplier = 1f; // 아이템용

        // 거리 기반 추격 보정용
        private UnifiedThirdPersonController _opponent;
        private float _opponentSearchTimer = 0f;
        private const float _opponentSearchInterval = 0.5f;
        private float _currentChaseBoost = 1f;
        /// <summary>현재 적용 중인 추격 거리 보정 배율 (1.0 = 보정 없음). 디버그/UI 용.</summary>
        public float CurrentChaseBoost => _currentChaseBoost;

        // timeout
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;
        private float _shiftCooldownDelta = 0.0f;

        // anim IDs
        private int _animIDSpeed;
        private int _animIDVerticalSpeed;
        private int _animIDGrounded;
        private int _animIDCrouch;
        private int _animIDShift;

        private bool _isDashing = false;
        public bool IsDashing => _isDashing;
        private Vector3 _dashDirection;

        // 에임 모드: 활 등 원거리 무기 장착 시 UnifiedWeaponBow가 켬/끔.
        // 이동 입력이 없어도 캐릭터 yaw를 카메라 yaw로 정렬.
        private bool _isAiming = false;
        // 에임 모드 진입 직후 첫 프레임: SmoothDamp 지연 없이 즉시 카메라 방향으로 스냅.
        private bool _aimModeJustActivated = false;
        // 에임 애니메이션이 측면 스탠스일 때 무기 쪽에서 세팅하는 보정각(도).
        // target yaw = camera_yaw - _aimYawOffset.
        private float _aimYawOffset = 0f;

        /// <summary>
        /// 에임 모드 토글. 활/원거리 무기 장착·해제 시 외부에서 호출.
        /// </summary>
        /// <param name="isAiming">에임 모드 on/off</param>
        /// <param name="yawOffset">
        /// 측면 스탠스 애니메이션 보정각(도). 애니메이션이 캐릭터 오른쪽(+X)으로 발사하면 양수, 왼쪽이면 음수.
        /// target yaw = camera_yaw - yawOffset 로 적용.
        /// </param>
        public void SetAimMode(bool isAiming, float yawOffset = 0f)
        {
            if (isAiming && !_isAiming) _aimModeJustActivated = true;
            _isAiming = isAiming;
            _aimYawOffset = yawOffset;
        }

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;
        private bool _hasAnimator;
        private bool _initialized;

        // 게임 오버 플래그
        private bool _isGameOver = false;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        // ============================================================
        // 게임 오버 이벤트
        // - 네트워크 모드: NetworkGameManger.OnGameOverEvent static 이벤트
        // - 오프라인 모드: 외부에서 HandleGameOver() 수동 호출 (예: 로컬 매니저)
        // ============================================================
        private void OnEnable()
        {
            if (!AuthorityGuard.IsOffline)
                NetworkGameManger.OnGameOverEvent += HandleGameOver;
        }

        private void OnDisable()
        {
            if (!AuthorityGuard.IsOffline)
                NetworkGameManger.OnGameOverEvent -= HandleGameOver;
        }

        public void HandleGameOver() => _isGameOver = true;

        // ============================================================
        // 초기화
        // - 오프라인: Start()에서 바로 초기화
        // - 네트워크: OnStartLocalPlayer()에서 초기화, 원격 플레이어는 입력/컨트롤러 비활성
        // ============================================================
        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        private void Start()
        {
            if (AuthorityGuard.IsOffline)
            {
                InitializeLocalControl();
                return;
            }

            if (!isLocalPlayer)
            {
#if ENABLE_INPUT_SYSTEM
                if (TryGetComponent(out PlayerInput pInput)) pInput.enabled = false;
#endif
                if (TryGetComponent(out StarterAssetsInputs sInput)) sInput.enabled = false;
                //if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
                return;
            }
            // 네트워크 + 로컬 플레이어일 땐 OnStartLocalPlayer에서 초기화
        }

        public override void OnStartLocalPlayer()
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetComponent(out PlayerInput pInput)) pInput.enabled = true;
#endif
            if (TryGetComponent(out StarterAssetsInputs sInput)) sInput.enabled = true;
            if (TryGetComponent(out CharacterController cc)) cc.enabled = true;

            InitializeLocalControl();
        }

        private void InitializeLocalControl()
        {
            if (_initialized) return;
            _initialized = true;

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
            if (_mainCamera == null)
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

            if (CinemachineCameraTarget != null)
                _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

            AssignAnimationIDs();
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            _jumpCountRemaining = EnableMultiJump ? Mathf.Max(1, MaxJumpCount) : 1;
            _wasGroundedLastFrame = true;
        }

        // ============================================================
        // Update / LateUpdate
        // ============================================================
        private void Update()
        {
            if (!AuthorityGuard.IsLocallyControlled(this.netIdentity)) return;
            if (_isGameOver) return;
            if (!_initialized) return;

            _hasAnimator = TryGetComponent(out _animator);

            // 순서 중요: 먼저 Grounded 갱신 → 그 결과로 JumpAndGravity 가 분기.
            // 이렇게 해야 착지 프레임에 stale Grounded 값으로 else 분기를 타며
            // _jumpTimeoutDelta 가 한 번 더 리셋되는 문제(즉시 재점프 막힘)가 사라진다.
            GroundedCheck();
            JumpAndGravity();
            if (!_isDashing) Move();
        }

        private void LateUpdate()
        {
            if (!AuthorityGuard.IsLocallyControlled(this.netIdentity)) return;
            if (_isGameOver) return;
            if (!_initialized) return;

            CameraRotation();
        }

        // ============================================================
        // 보조
        // ============================================================
        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDVerticalSpeed = Animator.StringToHash("VerticalSpeed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDCrouch = Animator.StringToHash("Crouch");
            _animIDShift = Animator.StringToHash("Shift");
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x,
                transform.position.y - GroundedOffset, transform.position.z);
            bool layerGrounded = Physics.CheckSphere(spherePosition, GroundedRadius,
                GroundLayers, QueryTriggerInteraction.Ignore);

            // 다른 플레이어 위에 올라갔을 때도 grounded 로 인정 → VerticalSpeed 무한 누적 방지
            Grounded = layerGrounded || (_controller != null && _controller.isGrounded);

            if (_hasAnimator) _animator.SetBool(_animIDGrounded, Grounded);
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                _cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            if (_shiftCooldownDelta > 0.0f) _shiftCooldownDelta -= Time.deltaTime;
            if (_shiftCooldownDelta > 0.0f) _input.shift = false;

            float targetSpeed = !Grounded ? JumpMoveSpeed :
                                _animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ? AttackMoveSpeed :
                                _animator.GetCurrentAnimatorStateInfo(0).IsTag("Strong Attack") ? AttackMoveSpeed :
                                _input.crouch ? CrouchSpeed :
                                MoveSpeed;

            targetSpeed *= _speedMultiplier;

            // 거리 기반 추격 보정 (상대를 향해 이동 중일 때 거리에 비례해 빨라짐)
            _currentChaseBoost = CalculateChaseBoostMultiplier();
            targetSpeed *= _currentChaseBoost;

            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                // --- 회전 수식 분기 ---
                float referenceYaw;
                if (rotationMode == CharacterRotationMode.CameraYaw)
                {
                    // Network 방식: 메인 카메라 yaw 직접 사용
                    referenceYaw = _mainCamera.transform.eulerAngles.y;
                }
                else
                {
                    // Local 방식: 카메라→플레이어 벡터 기반
                    Vector3 cameraToPlayer = (transform.position - _mainCamera.transform.position).normalized;
                    referenceYaw = Mathf.Atan2(cameraToPlayer.x, cameraToPlayer.z) * Mathf.Rad2Deg;
                }

                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + referenceYaw;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }
            else if (_isAiming)
            {
                // 에임 모드: 이동 입력이 없어도 카메라 정면으로 몸을 정렬.
                // 활 조준 중 카메라만 돌려도 캐릭터가 따라오게 해서 발사 방향과 몸 방향을 맞춘다.
                float aimReferenceYaw;
                if (rotationMode == CharacterRotationMode.CameraYaw)
                {
                    aimReferenceYaw = _mainCamera.transform.eulerAngles.y;
                }
                else
                {
                    Vector3 cameraToPlayer = (transform.position - _mainCamera.transform.position).normalized;
                    aimReferenceYaw = Mathf.Atan2(cameraToPlayer.x, cameraToPlayer.z) * Mathf.Rad2Deg;
                }

                // 측면 스탠스 애니메이션 보정: 몸을 카메라 yaw에서 보정각만큼 빼서 정렬.
                _targetRotation = aimReferenceYaw - _aimYawOffset;

                if (_aimModeJustActivated)
                {
                    // 에임 진입 첫 프레임: SmoothDamp 지연 없이 즉시 스냅.
                    _rotationVelocity = 0f;
                    transform.rotation = Quaternion.Euler(0.0f, _targetRotation, 0.0f);
                    _aimModeJustActivated = false;
                }
                else
                {
                    float aimRotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
                    transform.rotation = Quaternion.Euler(0.0f, aimRotation, 0.0f);
                }
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetBool(_animIDCrouch, _input.crouch);

                if (_input.shift)
                {
                    bool isAttacking = _animator.GetCurrentAnimatorStateInfo(0).IsTag("Strong Attack")
                                    || _animator.GetCurrentAnimatorStateInfo(0).IsName("Combo Attack 4");
                    if (_input.move != Vector2.zero && !isAttacking)
                    {
                        // 로컬은 즉시 반응(입력 지연 방지), 네트워크 모드면 추가로 Cmd 호출해
                        // 다른 클라이언트들의 Animator에도 Shift 트리거를 동기화한다.
                        _animator.SetTrigger(_animIDShift);
                        if (!AuthorityGuard.IsOffline)
                        {
                            CmdPlayDashAnim();
                        }
                        _shiftCooldownDelta = ShiftCooldown;
                        _dashDirection = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;
                        StartCoroutine(DashCoroutine());
                    }
                    _input.shift = false;
                }
            }
        }

        private IEnumerator DashCoroutine()
        {
            _isDashing = true;
            float elapsed = 0f;
            float distancePerFrame;

            while (elapsed < ShiftDashDuration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float t = 1f - (elapsed / ShiftDashDuration);
                distancePerFrame = (ShiftDashDistance / ShiftDashDuration) * t * 2f * dt;
                _controller.Move(_dashDirection * distancePerFrame);
                yield return null;
            }

            _isDashing = false;
        }

        private void JumpAndGravity()
        {
            // 점프 쿨타임은 지상/공중 무관하게 항상 틱 다운 (공중 체공시간 동안에도 흘러야
            // 착지 직후 즉시 재점프가 자연스러움).
            if (_jumpTimeoutDelta > 0.0f) _jumpTimeoutDelta -= Time.deltaTime;

            int maxJumps = EnableMultiJump ? Mathf.Max(1, MaxJumpCount) : 1;

            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                if (_hasAnimator) _animator.SetBool(_animIDGrounded, true);

                // 착지(또는 지상 유지) 시 점프 카운터 리필.
                _jumpCountRemaining = maxJumps;

                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;

                if (_input.jump && _jumpTimeoutDelta <= 0.0f && _jumpCountRemaining > 0)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    _jumpCountRemaining--;
                    _jumpTimeoutDelta = JumpTimeout;
                    _input.jump = false; // 한 번 누름 = 한 번 점프 (홀드 시 자동 연사 방지)
                    if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);
                }
            }
            else
            {
                // walk-off 감지: 직전 프레임엔 Grounded 였는데 지금은 공중 = 점프 안 하고 떨어짐.
                // 옵션에 따라 첫 점프(지상 점프) 한 칸을 소비한 것으로 처리.
                if (_wasGroundedLastFrame && ConsumeJumpOnWalkOff && _jumpCountRemaining == maxJumps)
                {
                    _jumpCountRemaining = Mathf.Max(0, _jumpCountRemaining - 1);
                }

                if (_fallTimeoutDelta >= 0.0f) _fallTimeoutDelta -= Time.deltaTime;
                else if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);

                // 공중 점프(멀티 점프). EnableMultiJump 가 꺼져 있으면 maxJumps=1 이라
                // walk-off 후엔 _jumpCountRemaining<=1 → 옵션에 따라 막히거나 1회 가능.
                if (_input.jump && _jumpCountRemaining > 0 && _jumpTimeoutDelta <= 0.0f && EnableMultiJump)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    _jumpCountRemaining--;
                    // 공중 연속 점프 사이의 최소 간격은 MultiJumpInterval 로 별도 관리
                    _jumpTimeoutDelta = MultiJumpInterval;
                    _input.jump = false; // 한 번 누름 = 한 번 점프
                    if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);
                    // 공중 점프 시 fall 타임아웃 살짝 리셋해 Falling 트랜지션이 한 박자 늦게 들어가게 함
                    _fallTimeoutDelta = FallTimeout;
                }

                // 주의: 기존엔 _input.jump = false 를 매 프레임 호출했으나 제거.
                // 그 결과 공중에서 미리 누른 점프 입력이 다음 점프(착지/멀티)까지 살아남아
                // 자연스러운 입력 버퍼링이 동작한다.
            }

            if (_verticalVelocity < _terminalVelocity) _verticalVelocity += Gravity * Time.deltaTime;
            if (_hasAnimator) _animator.SetFloat(_animIDVerticalSpeed, _verticalVelocity);

            _wasGroundedLastFrame = Grounded;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);
            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (!AuthorityGuard.IsLocallyControlled(this.netIdentity)) return;
            if (_controller == null) return;
            if (FootstepAudioClips == null || FootstepAudioClips.Length == 0) return;

            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                var index = Random.Range(0, FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (!AuthorityGuard.IsLocallyControlled(this.netIdentity)) return;
            if (_controller == null) return;
            if (LandingAudioClip == null) return;

            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }

        // ============================================================
        // 거리 기반 추격 보정
        // ============================================================
        /// <summary>
        /// 1대1 매치에서 상대 캐릭터를 검색해 캐싱한다.
        /// 네트워크 모드에선 원격 플레이어가 늦게 스폰될 수 있으므로 주기적으로 재시도.
        /// </summary>
        private void TryFindOpponent()
        {
            // 이미 유효한 상대를 들고 있으면 검색 생략
            if (_opponent != null) return;

            _opponentSearchTimer -= Time.deltaTime;
            if (_opponentSearchTimer > 0f) return;
            _opponentSearchTimer = _opponentSearchInterval;

            var all = FindObjectsOfType<UnifiedThirdPersonController>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != this)
                {
                    _opponent = all[i];
                    return;
                }
            }
        }

        /// <summary>
        /// 상대와의 거리에 따라 이동 속도 배율을 계산한다.
        /// - 임계 거리 안에선 1.0 (보정 없음)
        /// - 임계 거리 ~ 최대 거리 사이에서 곡선 보간으로 증가
        /// - 최대 거리 이상에선 ChaseBoostMaxMultiplier 로 캡
        /// - ChaseBoostOnlyWhenApproaching 옵션 시 상대 방향 이동일 때만 적용
        /// </summary>
        private float CalculateChaseBoostMultiplier()
        {
            if (!EnableDistanceSpeedBoost) return 1f;

            // 공격 모션 중엔 거리감/콤보 보호 위해 보정 OFF
            if (ChaseBoostDisableDuringAttack && _hasAnimator)
            {
                var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsTag("Attack") || stateInfo.IsTag("Strong Attack")) return 1f;
            }

            TryFindOpponent();
            if (_opponent == null) return 1f;

            // XZ 평면 거리 (수직 거리는 무시)
            Vector3 toOpponent = _opponent.transform.position - transform.position;
            toOpponent.y = 0f;
            float distance = toOpponent.magnitude;

            if (distance <= ChaseBoostMinDistance) return 1f;

            // 임계 거리 ~ 최대 거리 정규화
            float range = Mathf.Max(0.0001f, ChaseBoostMaxDistance - ChaseBoostMinDistance);
            float t = Mathf.Clamp01((distance - ChaseBoostMinDistance) / range);
            // 곡선 적용 (기본 ease-out: exponent < 1 → 임계 직후 빠르게 보정 들어옴)
            float curved = Mathf.Pow(t, ChaseBoostCurveExponent);
            float bonus = curved * (ChaseBoostMaxMultiplier - 1f);

            // 상대를 향해 이동 중일 때만 적용 (도망자는 보정 X)
            if (ChaseBoostOnlyWhenApproaching)
            {
                if (_input == null || _input.move == Vector2.zero) return 1f;
                if (toOpponent.sqrMagnitude < 0.0001f) return 1f;

                Vector3 toOpponentDir = toOpponent / distance;
                Vector3 moveDir = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude < 0.0001f) return 1f;
                moveDir.Normalize();

                float dot = Vector3.Dot(moveDir, toOpponentDir);
                if (dot <= 0f) return 1f; // 멀어지거나 직각 이동 → 보정 없음

                if (ChaseBoostScaleByAngle)
                {
                    // 정면(dot=1)일수록 강하게, 측면(dot=0)에 가까울수록 약하게
                    bonus *= dot;
                }
            }

            return 1f + bonus;
        }

        // 아이템에서 호출하는 속도 배율
        public float GetSpeedMultiplier() => _speedMultiplier;
        public void SetSpeedMultiplier(float value) => _speedMultiplier = value;

        //애니메이션 속도 조절용 스크립트 추가.
        public void RequestSetAnimatorSpeed(float speed)
        {
            if (AuthorityGuard.IsOffline)
            {
                ApplyAnimatorSpeed(speed);
            }
            else
            {
                // 네트워크 모드에선 서버에서만 RPC를 쏠 수 있음
                RpcSetAnimatorSpeed(speed);
            }
        }

        [ClientRpc]
        private void RpcSetAnimatorSpeed(float speed)
        {
            ApplyAnimatorSpeed(speed);
        }

        private void ApplyAnimatorSpeed(float speed)
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator != null) _animator.speed = speed;
        }

        private void CacheBaseGravityIfNeeded()
        {
            if (!_baseValuesCached)
            {
                _baseJumpHeight = JumpHeight;
                _baseGravity = Gravity;
                _baseValuesCached = true;
            }
        }

        public void AddGravityModifier(object source, float jumpMultiplier, float gravityMultiplier)
        {
            if (source == null) return;
            CacheBaseGravityIfNeeded();
            _gravityModifiers[source] = (jumpMultiplier, gravityMultiplier);
            RecalculateGravity();
        }

        public void RemoveGravityModifier(object source)
        {
            if (source == null) return;
            if (_gravityModifiers.Remove(source))
            {
                RecalculateGravity();
            }
        }

        private void RecalculateGravity()
        {
            float jumpMul = 1f;
            float gravityMul = 1f;
            foreach (var kv in _gravityModifiers.Values)
            {
                jumpMul *= kv.jumpMul;
                gravityMul *= kv.gravityMul;
            }
            JumpHeight = _baseJumpHeight * jumpMul;
            Gravity = _baseGravity * gravityMul;
        }

        public void SetGravityMultiplier(float jumpMultiplier, float gravityMultiplier)
        {
            AddGravityModifier(this, jumpMultiplier, gravityMultiplier);
        }

        public void ResetGravity()
        {
            RemoveGravityModifier(this);
        }

        //점프 쿨타임을 즉시 0으로 만들어준다. 폭포에서 사용을 해야 해서 만들어 둠. 
        public void ResetJumpTimeout()
        {
            _jumpTimeoutDelta = 0f;
        }

        public void ApplyJumpByHeight(float jumpHeight, Vector3 horizontalVelocity)
        {
            // 기존 JumpAndGravity()와 동일한 공식: v = sqrt(h * -2 * g)
            float v = Mathf.Sqrt(jumpHeight * -2f * Gravity);
            ApplyLaunch(v, horizontalVelocity);
        }

        public void ApplyLaunch(float verticalVelocity, Vector3 horizontalVelocity)
        {
            _verticalVelocity = verticalVelocity;

            // 수평 추진이 있으면 한 프레임 분량을 즉시 적용
            if (horizontalVelocity.sqrMagnitude > 0.001f)
            {
                _controller.Move(horizontalVelocity * Time.deltaTime);
            }

            // 점프 발동 시 Grounded 애니메이션 해제
            if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);
        }
        [Command]
        private void CmdPlayDashAnim()
        {
            RpcPlayDashAnim();
        }

        [ClientRpc(includeOwner = false)]
        private void RpcPlayDashAnim()
        {
            // 원격 클라이언트는 InitializeLocalControl()을 거치지 않아
            // _animator 가 null 일 수 있으므로 안전하게 fetch.
            if (_animator == null) TryGetComponent(out _animator);
            if (_animator != null) _animator.SetTrigger("Shift");
        }

    }
}
