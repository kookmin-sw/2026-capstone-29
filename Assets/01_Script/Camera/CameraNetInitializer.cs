using UnityEngine;
using Mirror;
using Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class CameraNetInitializer : NetworkBehaviour
{
    public override void OnStartLocalPlayer()
    {
        // --- 입력 강제 페어링/스킴 전환 ---
        var pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            // 기존에 이상하게 잡힌 디바이스가 있으면 정리(선택)
            // pi.user.UnpairDevicesAndRemoveUser(); // 버전에 따라 없을 수 있음

            // PerformPairingWithDevice 로 "현재 pi.user"에 디바이스를 페어링
            if (Keyboard.current != null)
                InputUser.PerformPairingWithDevice(Keyboard.current, pi.user);

            if (Mouse.current != null)
                InputUser.PerformPairingWithDevice(Mouse.current, pi.user);

            // 액션맵/스킴 강제
            pi.SwitchCurrentActionMap("Player");

            // 네 Control Scheme 이름이 KeyboardMouse 임 (스샷 기준)
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

        // --- 카메라 연결(기존 코드) ---
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
