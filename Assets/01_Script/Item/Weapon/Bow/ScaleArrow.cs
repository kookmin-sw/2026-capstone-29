using UnityEngine;

public class ScaleArrow : MonoBehaviour
{
    [Tooltip("중력 배율. 1이 기본, 낮출수록 낙차 감소")]
    [SerializeField] private float gravityScale = 0.3f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // 기본 중력 끄기
    }

    private void FixedUpdate()
    {
        rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
    }
}
