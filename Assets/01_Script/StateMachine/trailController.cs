using UnityEngine;
using Tiny;

public class trailController : MonoBehaviour
{
    [SerializeField] private Trail trail;

    private void Awake()
    {
        if (trail == null)
            trail = GetComponentInChildren<Trail>(true);

        // 시작할 때는 꺼둠
        if (trail != null)
            trail.enabled = false;
    }

    // Animation Event에서 호출
    public void TrailOn()
    {
        if (trail == null) return;
        if (trail.enabled) return;  // 이미 켜져 있으면 무시
        trail.enabled = true;
    }

    public void TrailOff()
    {
        if (trail == null) return;
        if (!trail.enabled) return; // 이미 꺼져 있으면 무시
        trail.Clear();
        trail.enabled = false;
    }
}