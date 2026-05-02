using UnityEngine;
using System.Collections;
using StarterAssets;

/// <summary>
/// 폭포 지형 오브젝트.
/// 플레이어가 트리거 영역 안에 있는 동안:
///   1) 점프 가능 상태(Grounded)를 강제로 유지한다.
///   2) 중력을 약화시켜 천천히 떨어지게 한다.
/// 단, 한 번 점프할 때마다 코루틴으로 관리되는 쿨타임이 발생하며, 그 시간 동안에는 점프가 막힌다.
/// DefaultExecutionOrder로 Update 실행 순서를 앞으로 당겨, 점프가 초기화되지 않는 상황 방지.
/// </summary>
[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(-100)]
public class WaterfallObject : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("폭포 효과를 받을 플레이어의 태그.")]
    [SerializeField] private string PlayerTag = "Player";

    [Header("Jump Settings")]
    [Tooltip("폭포 안에서 점프했을 때 적용되는 점프 높이.")]
    [SerializeField] private float WaterfallJumpHeight = 1.5f;

    [Tooltip("폭포 안에서 한 번 점프한 뒤, 다시 점프가 가능해지기까지의 쿨타임(초).")]
    [SerializeField] private float JumpCooldown = 0.6f;

    [Header("Gravity Settings (물 속에서 천천히 떨어지기)")]
    [Tooltip("폭포 안에서의 중력 배율. 1보다 작을수록 천천히 떨어진다. 예: 0.3 → 30% 중력")]
    [Range(0.05f, 2f)]
    [SerializeField] private float WaterGravityMultiplier = 0.8f;

    [Tooltip("폭포 안에서의 점프 높이 배율. 폭포 점프 높이는 별도(WaterfallJumpHeight)로 관리하므로 보통 1로 둔다.")]
    [SerializeField] private float WaterJumpMultiplier = 1f;

    [Header("Debug")]
    [SerializeField] private bool DebugLog = false;

    // 현재 폭포에 접촉 중인 플레이어 컨트롤러
    private UnifiedThirdPersonController _player;
    private StarterAssetsInputs _playerInput;
    private bool _onCooldown = false;
    private Coroutine _cooldownRoutine;
    private float _cachedOriginalJumpHeight;

    // 중력 modifier가 현재 등록되어 있는지
    private bool _gravityModifierApplied = false;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(PlayerTag)) return;

        UnifiedThirdPersonController controller = other.GetComponent<UnifiedThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInParent<UnifiedThirdPersonController>();

        if (controller == null)
        {
            if (DebugLog) Debug.LogWarning($"[Waterfall] {other.name}에 UnifiedThirdPersonController가 없습니다.");
            return;
        }

        _player = controller;
        _playerInput = _player.GetComponent<StarterAssetsInputs>();

        // 즉시 점프 가능 상태로 설정
        _player.ResetJumpTimeout();

        // 중력 효과 적용 
        _player.AddGravityModifier(this, WaterJumpMultiplier, WaterGravityMultiplier);
        _gravityModifierApplied = true;

        if (DebugLog) Debug.Log($"[Waterfall] 플레이어 진입 / 중력 ×{WaterGravityMultiplier} 적용");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(PlayerTag)) return;

        UnifiedThirdPersonController controller = other.GetComponent<UnifiedThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInParent<UnifiedThirdPersonController>();

        if (controller == null || controller != _player) return;

        if (_cooldownRoutine != null)
        {
            StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = null;
        }

        if (_player != null && _onCooldown)
        {
            _player.JumpHeight = _cachedOriginalJumpHeight;
        }

        // 중력 modifier 제거 
        if (_player != null && _gravityModifierApplied)
        {
            _player.RemoveGravityModifier(this);
            _gravityModifierApplied = false;
        }

        _onCooldown = false;
        _player = null;
        _playerInput = null;

        if (DebugLog) Debug.Log($"[Waterfall] 플레이어 이탈 / 중력 효과 제거");
    }

    private void Update()
    {
        if (_player == null) return;
        if (_onCooldown) return;

        _player.Grounded = true;

        if (_playerInput != null && _playerInput.jump)
        {
            TriggerWaterfallJump();
        }
    }

    private void TriggerWaterfallJump()
    {
        if (_player == null) return;

        _cachedOriginalJumpHeight = _player.JumpHeight;
        _player.JumpHeight = WaterfallJumpHeight;

        if (DebugLog) Debug.Log($"[Waterfall] 점프 발동! 높이 : {WaterfallJumpHeight}, 쿨타임 : {JumpCooldown}s");

        if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
        _cooldownRoutine = StartCoroutine(JumpCooldownRoutine());
    }

    private IEnumerator JumpCooldownRoutine()
    {
        _onCooldown = true;

        yield return null; // 한 프레임 대기 - 이때 플레이어 JumpAndGravity()가 점프를 발동시킨다

        if (_player != null)
        {
            _player.JumpHeight = _cachedOriginalJumpHeight;
        }

        float remaining = JumpCooldown - Time.deltaTime;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        if (_player != null)
        {
            _player.ResetJumpTimeout();
        }

        _onCooldown = false;
        _cooldownRoutine = null;

        if (DebugLog) Debug.Log("[Waterfall] 쿨타임 종료, 다시 점프 가능.");
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화되거나 파괴되어도 플레이어에게 효과가 남아있으면 안 되므로 정리
        if (_cooldownRoutine != null)
        {
            StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = null;
        }

        if (_player != null && _onCooldown)
        {
            _player.JumpHeight = _cachedOriginalJumpHeight;
        }

        if (_player != null && _gravityModifierApplied)
        {
            _player.RemoveGravityModifier(this);
            _gravityModifierApplied = false;
        }

        _onCooldown = false;
        _player = null;
        _playerInput = null;
    }
}