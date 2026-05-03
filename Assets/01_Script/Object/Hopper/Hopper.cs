using UnityEngine;
using StarterAssets;

/// <summary>
/// 점프대(Hopper) 지형 오브젝트.
/// 플레이어가 트리거 영역에 진입하면 점프 버튼 입력과 무관하게 자동으로 점프시킨다.
/// 점프 방향은 "위 + 플레이어가 입력한 이동 방향"으로 결정되며, 입력이 없으면 순수하게 위로만 발사한다.
/// 입력 방향 변환은 플레이어 컨트롤러의 RotationMode(CameraYaw / CameraToPlayer)와 동일한 방식을 따른다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Hopper : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("점프대 효과를 받을 플레이어의 태그.")]
    [SerializeField] private string PlayerTag = "Player";

    [Header("Jump Settings")]
    [Tooltip("점프대로 도달할 점프 높이(m). 일반 JumpHeight보다 크게 설정하는 것을 권장.")]
    [SerializeField] private float JumpHeight = 5.0f;

    [Tooltip("플레이어 입력 방향으로의 수평 추진 속도(m/s). 0이면 수평 이동 없음.")]
    [SerializeField] private float HorizontalBoost = 6.0f;

    [Tooltip("재사용 쿨타임(초). 한 번 발동 후 이 시간 동안은 같은 점프대가 재발동되지 않는다. " +
             "OnTriggerEnter 중복 호출 / 짧은 시간에 다시 밟는 케이스 방지용.")]
    [SerializeField] private float ReactivationCooldown = 0.3f;

    [Header("Effects (Optional)")]
    [SerializeField] private AudioClip LaunchSound;
    [SerializeField, Range(0f, 1f)] private float LaunchSoundVolume = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool DebugLog = false;

    private float _lastLaunchTime = -999f;

    private Camera _mainCamera;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(PlayerTag)) return;

        if (Time.time - _lastLaunchTime < ReactivationCooldown) return;

        UnifiedThirdPersonController controller = other.GetComponent<UnifiedThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInParent<UnifiedThirdPersonController>();

        if (controller == null)
        {
            if (DebugLog) Debug.LogWarning($"[Hopper] {other.name}에 UnifiedThirdPersonController가 없습니다.");
            return;
        }

        LaunchPlayer(controller);
    }

    // 플레이어 발사
    private void LaunchPlayer(UnifiedThirdPersonController controller)
    {
        StarterAssetsInputs inputs = controller.GetComponent<StarterAssetsInputs>();
        Vector3 horizontalVelocity = Vector3.zero;

        if (inputs != null && inputs.move.sqrMagnitude > 0.01f && HorizontalBoost > 0f)
        {
            horizontalVelocity = ComputeWorldDirection(controller, inputs.move) * HorizontalBoost;
        }

        // 점프 발동
        controller.ApplyJumpByHeight(JumpHeight, horizontalVelocity);

        
        // ResetJumpTimeout으로 미리 0으로 만들어둬서 착지 직후에 언제든 점프를 바로 할 수 있게 설정.
        controller.ResetJumpTimeout();

        _lastLaunchTime = Time.time;

        if (LaunchSound != null)
        {
            AudioSource.PlayClipAtPoint(LaunchSound, transform.position, LaunchSoundVolume);
        }

        if (DebugLog)
        {
            Debug.Log($"[Hopper] 발동! 높이={JumpHeight}m, 수평속도={horizontalVelocity.magnitude:F1}m/s, 방향={horizontalVelocity.normalized}");
        }
    }

    /// <summary>
    /// 플레이어 입력(2D move 벡터)을 플레이어 컨트롤러의 회전 모드에 맞춰 월드 방향으로 변환한다.
    /// UnifiedThirdPersonController.Move()의 회전 분기 로직과 동일한 방식이라
    /// 회전 모드(CameraYaw / CameraToPlayer)가 바뀌어도 일관되게 동작한다.
    /// </summary>
    private Vector3 ComputeWorldDirection(UnifiedThirdPersonController controller, Vector2 moveInput)
    {
        if (_mainCamera == null) _mainCamera = Camera.main;

        // 입력 방향 (XZ 평면)
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        // 카메라 기준 yaw 결정 — 플레이어 컨트롤러와 동일 로직
        float referenceYaw;
        if (controller.rotationMode == CharacterRotationMode.CameraYaw)
        {
            // 일반 3인칭: 메인 카메라 yaw 직접 사용
            referenceYaw = (_mainCamera != null) ? _mainCamera.transform.eulerAngles.y : 0f;
        }
        else
        {
            // 아레나/탑다운: 카메라→플레이어 벡터 기반
            if (_mainCamera != null)
            {
                Vector3 cameraToPlayer = (controller.transform.position - _mainCamera.transform.position).normalized;
                referenceYaw = Mathf.Atan2(cameraToPlayer.x, cameraToPlayer.z) * Mathf.Rad2Deg;
            }
            else
            {
                referenceYaw = 0f;
            }
        }

        // 입력 방향 + 카메라 기준 yaw → 월드 방향
        float worldYaw = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + referenceYaw;
        return Quaternion.Euler(0f, worldYaw, 0f) * Vector3.forward;
    }

    private void OnDrawGizmosSelected()
    {
        // 점프 방향 시각화: 위 + 점프대 forward (대표 방향)
        Vector3 origin = transform.position;
        Vector3 up = Vector3.up * Mathf.Min(JumpHeight, 5f);
        Vector3 forward = transform.forward * Mathf.Min(HorizontalBoost * 0.3f, 3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + up);
        Gizmos.DrawLine(origin, origin + up + forward);
    }
}