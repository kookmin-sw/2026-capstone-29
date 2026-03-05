using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class InputDebugger : NetworkBehaviour
{
    void Start()
    {
        if (!isLocalPlayer) return;
        Debug.Log("[InputDebugger] Local player ready");

         var pi = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        Debug.Log($"[PI] enabled={pi.enabled}, currentMap={pi.currentActionMap?.name}, scheme={pi.currentControlScheme}, devices={pi.devices.Count}");
    }

    // PlayerInput(Behavior=Send Messages)가 호출할 멤버 메서드들
    public void OnMove(InputValue value)
    {
        if (!isLocalPlayer) return;
        Debug.Log($"[Input] Move {value.Get<Vector2>()}");
    }

    public void OnLook(InputValue value)
    {
        if (!isLocalPlayer) return;
        Debug.Log($"[Input] Look {value.Get<Vector2>()}");
    }

    public void OnJump(InputValue value)
    {
        if (!isLocalPlayer) return;
        Debug.Log($"[Input] Jump {value.isPressed}");
    }

    public void OnSprint(InputValue value)
    {
        if (!isLocalPlayer) return;
        Debug.Log($"[Input] Sprint {value.isPressed}");
    }
}
