using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerItem_Gun : NetworkBehaviour, IEquipment // 총 오브젝트에 부착.
{
    [SerializeField] private GameObject bulletObj;

    [SerializeField] private float cooltime = 1;
    [SerializeField] private float equipmentLimitTime = 10;
    [SerializeField] private bool canShot = true;

    private float coolTimer = 0;
    private float limitTimer = 0;

    [Server]
    void Update()
    {
        // 회전은 모든 클라이언트에서 동기화
        if (transform.parent != null)
        {
            transform.rotation = transform.parent.rotation;
            transform.position = transform.parent.position + transform.parent.forward * 1.5f;
        }

        if (!isServer) return;

        coolTimer += Time.deltaTime;
        if (coolTimer >= cooltime)
        {
            canShot = true;
            coolTimer = 0f;
        }

        limitTimer += Time.deltaTime;
        if (limitTimer >= equipmentLimitTime)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }
    }

    public void Effect() // 아이템의 효과
    {
        CmdShoot(this.gameObject);
    }

    [Command(requiresAuthority = false)]
    void CmdShoot(GameObject owner)
    {
        if (!canShot) return;

        // 서버에서 총알 생성
        GameObject bullet = Instantiate(bulletObj, transform.position + transform.forward, transform.rotation);

        // 총알 스크립트 가져와서 주인을 자신으로 설정
        //Bullet bulletScript = bullet.GetComponent<BulletScript>();
        //bulletScript.owner = gameObject;

        // 모든 클라이언트에서 총알 생성하라고 명령
        NetworkServer.Spawn(bullet);

        // 총알에 힘을 가해서 날리기
        bullet.GetComponent<Rigidbody>().velocity = transform.forward * 15f;
        coolTimer = 0;
        canShot = false;
    }
}
