using Mirror;
using UnityEngine;

public class ScaleArrow : NetworkBehaviour
{
    [Tooltip("중력 배율")]
    [SerializeField] private float gravityScale = 0.3f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        // 서버에서만 물리 처리
        if (!isServer) return;
        rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
    }
}