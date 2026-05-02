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

            // 네트워크 모드 + 원격 플레이어면 입력/물리 비활성
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

            JumpAndGravity();
            GroundedCheck();
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
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
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
                        _animator.SetTrigger(_animIDShift);
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
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                if (_hasAnimator) _animator.SetBool(_animIDGrounded, true);
                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);
                }

                if (_jumpTimeoutDelta >= 0.0f) _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f) _fallTimeoutDelta -= Time.deltaTime;
                else if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity) _verticalVelocity += Gravity * Time.deltaTime;
            if (_hasAnimator) _animator.SetFloat(_animIDVerticalSpeed, _verticalVelocity);
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
    }
}
