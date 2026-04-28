using UnityEngine;
using Mirror;
using Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class CameraNetInitializer : NetworkBehaviour
{
    private bool _isGameOver = false; // 게임 종료 플래그 추가
    private PlayerInput _playerInput;

    // OnEnable / OnDisable 추가
    private void OnEnable()
    {

    }

    private void OnDisable()
    {
        Debug.Log($"[OnDisable] 호출됨. obj={gameObject.name}");
        NetworkGameManger.OnGameOverEvent -= HandleGameOver;
        Application.focusChanged -= OnAppFocusChanged;
    }

    private void Update()
    {
        if (_isGameOver)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // 게임오버 시 PlayerInput 비활성화 + 커서 해제
    private void HandleGameOver()
    {
        Debug.Log($"[HandleGameOver] 실행됨. isLocalPlayer={isLocalPlayer}, isClient={isClient}, isServer={isServer}");

        _isGameOver = true;

        var allInputs = FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsSortMode.None);
        Debug.Log($"[HandleGameOver] 찾은 SAI 개수: {allInputs.Length}");
        foreach (var sai in allInputs)
        {
            sai.cursorLocked = false;
            sai.enabled = false;
        }

        var allPlayerInputs = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        Debug.Log($"[HandleGameOver] 찾은 PlayerInput 개수: {allPlayerInputs.Length}");
        foreach (var pi in allPlayerInputs)
        {
            pi.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Alt+Tab 후 복귀 시 게임오버 상태면 커서 강제 유지
    private void OnAppFocusChanged(bool hasFocus)
    {
        if (hasFocus && _isGameOver)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"[OnStartLocalPlayer] 실행. obj={gameObject.name}, isClient={isClient}, isServer={isServer}");

        NetworkGameManger.OnGameOverEvent += HandleGameOver;
        Application.focusChanged += OnAppFocusChanged;

        Debug.Log("[OnStartLocalPlayer] 이벤트 구독 완료");

        // --- 입력 강제 페어링/스킴 전환 ---
        _playerInput = GetComponent<PlayerInput>();
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput != null)
        {
            if (Keyboard.current != null)
                InputUser.PerformPairingWithDevice(Keyboard.current, _playerInput.user);
            if (Mouse.current != null)
                InputUser.PerformPairingWithDevice(Mouse.current, _playerInput.user);

            _playerInput.SwitchCurrentActionMap("Player");

            if (Keyboard.current != null && Mouse.current != null)
                _playerInput.SwitchCurrentControlScheme("KeyboardMouse", Keyboard.current, Mouse.current);
            else if (Keyboard.current != null)
                _playerInput.SwitchCurrentControlScheme("KeyboardMouse", Keyboard.current);
        }

        // --- 카메라 연결 (기존 코드 유지) ---
        CinemachineVirtualCamera vcam = Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam == null) { Debug.LogError("CinemachineVirtualCamera 없음"); return; }

        Transform target = transform.Find("PlayerCameraRoot");
        if (target != null) { vcam.Follow = target; vcam.LookAt = target; }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
}
