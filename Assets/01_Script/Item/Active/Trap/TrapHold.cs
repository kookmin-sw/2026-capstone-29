using UnityEngine;

/// 덫에 걸린 플레이어에게 일정 시간 이동 봉쇄
public class TrapHold : MonoBehaviour
{
    // 비활성화할 이동/입력 스크립트 클래스명. 프로젝트에 맞게 필요시 확장.
    private static readonly string[] BlockBehaviourByName = new string[]
    {
        "ThirdPersonController",
        "StarterAssetsInputs"
    };

    private float remaining;
    private Vector3 lockedPosition;
    private bool active;

    // 원래 상태 백업
    private CharacterController controller;
    private bool controllerWasEnabled;

    private Rigidbody rb;
    private bool rbWasKinematic;

    private MonoBehaviour[] blockedBehaviours;
    private bool[] blockedBehavioursPrevEnabled;

    // 속박 시작. 진입 시점 위치 그대로 잠금
    public void Begin(float duration)
    {
        BeginAt(duration, transform.position);
    }

    //플레이어를 덫(anchor)의 x,z로 끌어당기고 y는 현재 위치로 유지
    public void BeginAtAnchor(float duration, Vector3 anchor)
    {
        // anchor의 x,z + 현재 y
        Vector3 lockTo = new Vector3(anchor.x, transform.position.y, anchor.z);
        BeginAt(duration, lockTo);
    }

    private void BeginAt(float duration, Vector3 lockTo)
    {
        if (active)
        {
            Refresh(duration);
            // 이미 묶여있는 상태에서 새 덫에 또 걸린다면, 새 anchor로 위치도 갱신
            lockedPosition = lockTo;
            transform.position = lockTo;
            return;
        }

        remaining = duration;
        lockedPosition = lockTo;
        transform.position = lockTo; // 즉시 위치 변경
        active = true;

        BackupAndDisable();
    }

    public void Refresh(float duration)
    {
        if (duration > remaining) remaining = duration;
    }

    private void BackupAndDisable()
    {
        // CharacterController
        controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controllerWasEnabled = controller.enabled;
            controller.enabled = false;
        }

        // Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rbWasKinematic = rb.isKinematic;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // 이동/입력 스크립트 비활성
        MonoBehaviour[] all = GetComponents<MonoBehaviour>();
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == this) continue;
            if (IsBlockTarget(all[i])) count++;
        }

        blockedBehaviours = new MonoBehaviour[count];
        blockedBehavioursPrevEnabled = new bool[count];

        int idx = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == this) continue;
            if (!IsBlockTarget(all[i])) continue;

            blockedBehaviours[idx] = all[i];
            blockedBehavioursPrevEnabled[idx] = all[i].enabled;
            all[i].enabled = false;
            idx++;
        }
    }

    private static bool IsBlockTarget(MonoBehaviour mb)
    {
        if (mb == null) return false;
        string typeName = mb.GetType().Name;
        for (int i = 0; i < BlockBehaviourByName.Length; i++)
        {
            if (typeName == BlockBehaviourByName[i]) return true;
        }
        return false;
    }

    private void Update()
    {
        if (!active) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            Restore();
            Destroy(this);
        }
    }

    private void LateUpdate()
    {
        if (!active) return;

        // 각자 클라이언트 혹은 로컬에서 위치 강제 고정.
        transform.position = lockedPosition;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void Restore()
    {
        active = false;

        if (controller != null)
        {
            controller.enabled = controllerWasEnabled;
            controller = null;
        }

        if (rb != null)
        {
            rb.isKinematic = rbWasKinematic;
            rb = null;
        }

        if (blockedBehaviours != null)
        {
            for (int i = 0; i < blockedBehaviours.Length; i++)
            {
                if (blockedBehaviours[i] != null)
                    blockedBehaviours[i].enabled = blockedBehavioursPrevEnabled[i];
            }
            blockedBehaviours = null;
            blockedBehavioursPrevEnabled = null;
        }
    }

    private void OnDestroy()
    {
        if (active) Restore();
    }
}