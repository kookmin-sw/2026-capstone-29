using Mirror;
using UnityEngine;

public class PlayerScript : NetworkBehaviour
{
    public GameObject bulletPrefab;

    // 색깔을 바꿀 재질에 접근하기 위해 렌더러가 필요
    public override void OnStartLocalPlayer()
    {
        // 이 함수는 내 캐릭터가 생성될 때 딱 한 번 실행
        // 내 캐릭터의 색깔만 파란색으로 바꾼다
        GetComponent<MeshRenderer>().material.color = Color.blue;
    }

    void Update()
    {
        // 내 캐릭터가 아니면 조종하지 못하게 막음
        if (!isLocalPlayer) { return; }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 moveVec = new Vector3(moveX, 0, moveZ).normalized;

        transform.position += moveVec * 5f * Time.deltaTime;
        transform.LookAt(transform.position + moveVec);

        // 마우스 좌클릭시 발사 요청
        if(Input.GetMouseButtonDown(0))
        {
            CmdFire();
        }
    }

    [Command] // 클라이언트가 호출하지만, 실제 실행은 서버에서 되는 함수들, 함수 이름이 Cmd로 시작해야함
    void CmdFire()
    {
        // 서버에서 총알 생성
        GameObject bullet = Instantiate(bulletPrefab, transform.position + transform.forward, transform.rotation);

        // 총알 스크립트 가져와서 주인을 자신으로 설정
        BulletScript bulletScript = bullet.GetComponent<BulletScript>();
        bulletScript.owner = gameObject;

        // 모든 클라이언트에서 총알 생성하라고 명령
        NetworkServer.Spawn(bullet);

        // 총알에 힘을 가해서 날리기
        bullet.GetComponent<Rigidbody>().velocity = transform.forward * 15f;


        // 일정 시간 후에 삭제
        Destroy(bullet, 2.0f);
    }


}