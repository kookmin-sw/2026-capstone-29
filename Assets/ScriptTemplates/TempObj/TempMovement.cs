using Mirror;
using UnityEngine;

public class TempMovement : NetworkBehaviour
{
    [SerializeField] float speed = 10f;
    [SerializeField] float jumpForce = 5f;
    [SerializeField] Rigidbody rb;

    // Update is called once per frame
    void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }
        CmdPlayerMove();
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CmdJump();
        }
    }

    [Command]
    private void CmdPlayerMove() // 방향키를 이용한 조작은 모바일로 이식하는 과정에서 대체된다.
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(x * speed,0,  z * speed);

        this.transform.position += move * Time.deltaTime;

    }

    [Command]
    void CmdJump()
    {
        if (IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

}
