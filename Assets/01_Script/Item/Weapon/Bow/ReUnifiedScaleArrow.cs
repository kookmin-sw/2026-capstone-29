using Mirror;
using UnityEngine;

/// <summary>
/// - 온라인: 서버에서만 물리 처리 (NetworkTransform이 클라이언트에 동기화).
/// - 오프라인: 본인이 물리 처리.
/// </summary>
public class UnifiedScaleArrow : NetworkBehaviour
{
    [Tooltip("중력 배율")]
    [SerializeField] private float gravityScale = 0.3f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // 권위: 온라인은 서버, 오프라인은 본인
        bool hasAuthority = AuthorityGuard.IsOffline || isServer;
        if (!hasAuthority) return;

        rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
    }
}