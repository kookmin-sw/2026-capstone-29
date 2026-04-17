using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class CameraLocalInitializer : MonoBehaviour
{
    private void Start()
    {
        // --- 입력 강제 페어링/스킴 전환 ---
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        var pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            if (Keyboard.current != null)
                InputUser.PerformPairingWithDevice(Keyboard.current, pi.user);

            if (Mouse.current != null)
                InputUser.PerformPairingWithDevice(Mouse.current, pi.user);

            pi.SwitchCurrentActionMap("Player");

            if (Keyboard.current != null && Mouse.current != null)
                pi.SwitchCurrentControlScheme("KeyboardMouse", Keyboard.current, Mouse.current);
            else if (Keyboard.current != null)
                pi.SwitchCurrentControlScheme("KeyboardMouse", Keyboard.current);

            Debug.Log($"[PI after pair] scheme={pi.currentControlScheme}, devices={pi.devices.Count}");
        }
        else
        {
            Debug.LogError("[PI] PlayerInput 컴포넌트가 없습니다.");
        }

        // --- 카메라 연결 ---
        CinemachineVirtualCamera vcam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            Debug.LogError("게임 씬에 CinemachineVirtualCamera가 없습니다!");
            return;
        }

        Transform target = transform.Find("PlayerCameraRoot");
        if (target != null)
        {
            vcam.Follow = target;
            vcam.LookAt = target;
        }
        else
        {
            Debug.LogError("플레이어 프리팹 내부에 'PlayerCameraRoot'를 찾을 수 없습니다.");
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}