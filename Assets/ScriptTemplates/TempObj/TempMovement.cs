using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TempMovement : MonoBehaviour
{
    [SerializeField] float speed = 10f;

    // Update is called once per frame
    void Update()
    {
        PlayerMove();
    }

    private void PlayerMove() // 방향키를 이용한 조작은 모바일로 이식하는 과정에서 대체된다.
    {
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(x * speed, y * speed, 0);

        this.transform.position += move * Time.deltaTime;

    }
}
