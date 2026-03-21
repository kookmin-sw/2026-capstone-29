using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon_Bomb : MonoBehaviour
{

    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("FBX 세팅")]
    [SerializeField] public GameObject ExplosionEffect;

    // 내부 상태
    private bool isNocked;      // 폭탄이 장전된 상태
    private bool isLaunched;    // 폭탄을 던진 상태
    private float lifeTimer;
    private Vector3 flyDirection;
    private float flySpeed;

    private void Update()
    {

        // 수명 체크-폭탄을 들어올린 시점으로부터 타이머를 돌린다.
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= itemStat.availableTime)
        {
            Explosion();
            DestroyBomb();
        }

        // 장전 상태에서는 아무것도 하지 않음 (WeaponBow가 위치를 제어)
        if (isNocked) return;

        // 발사된 상태에서만 이동체크
        if (!isLaunched) return;

        // 전방 이동
        transform.position += flyDirection * flySpeed * Time.deltaTime;

    }

    // 장전 상태 설정. true이면 화살이 활에 귀속되어 대기.
    public void SetNocked(bool nocked)
    {
        isNocked = nocked;
        isLaunched = false;
        lifeTimer = 0f;
    }

    // 폭탄을 던진다.Spawner_Bomb에서 호출.
    public void Launch(Vector3 direction, float speed)
    {
        isNocked = false;
        isLaunched = true;
        lifeTimer = 0f;

        flyDirection = direction.normalized;
        flySpeed = speed;

        // 폭탄이 날아가는 방향을 바라보도록 회전
        transform.rotation = Quaternion.LookRotation(flyDirection);
    }

    private void Explosion()
    {
        Instantiate(ExplosionEffect, this.transform.position, Quaternion.identity);
    }

    private void DestroyBomb()
    {
        Destroy(gameObject);
    }
}
