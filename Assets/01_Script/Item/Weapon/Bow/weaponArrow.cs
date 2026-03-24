using UnityEngine;
using Mirror;

// 화살 오브젝트.
// 장전(Nocked) 상태에서는 활을 따라다니며 대기.
// 발사(Launched) 후에는 지정된 방향으로 날아가며, 10초 경과 또는 피격 판정 시 파괴.
public class WeaponArrow : NetworkBehaviour
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [HideInInspector] public GameObject owner;

    // 내부 상태. SyncVar로 상태 동기화.
    [SyncVar] private bool isNocked;      // 활에 장전된 상태
    [SyncVar] private bool isLaunched;    // 발사된 상태
    
    [SyncVar] private Vector3 flyDirection;
    [SyncVar] private float flySpeed;

    private float lifeTimer;

    private void Update()
    {
        // 장전 상태에서는 아무것도 하지 않음 (WeaponBow가 위치를 제어)
        if (isNocked) return;

        // 발사된 상태에서만 이동 및 수명 체크
        if (!isLaunched) return;

        // 전방 이동
        transform.position += flyDirection * flySpeed * Time.deltaTime;

        // 수명 체크
        if (isServer)
        {

            lifeTimer += Time.deltaTime;
            if (lifeTimer >= itemStat.availableTime)
            {
                NetworkServer.Destroy(gameObject);
            }
        }
    }

    // 장전 상태 설정. true이면 화살이 활에 귀속되어 대기.
    public void SetNocked(bool nocked)
    {
        isNocked = nocked;
        isLaunched = false;
        lifeTimer = 0f;
    }

    // 화살을 발사한다. WeaponBow에서 호출.
    public void Launch(Vector3 direction, float speed)
    {
        isNocked = false;
        isLaunched = true;
        lifeTimer = 0f;

        flyDirection = direction.normalized;
        flySpeed = speed;

        // 화살이 날아가는 방향을 바라보도록 회전
        transform.rotation = Quaternion.LookRotation(flyDirection);
    }

    // 충돌 시 피격 판정 처리. 발사 상태일 때만 반응. 서버에서만 판정 감지하도록 설정.
    // Collider의 Is Trigger를 켜두거나, OnCollisionEnter로 변경 가능.
    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        if (!isLaunched) return;

        // 활 자체나 발사자와의 충돌은 무시 (필요에 따라 태그/레이어로 필터링)
        if (other.CompareTag("Player")) return;

        // TODO: 데미지 처리

        NetworkServer.Destroy(this.gameObject);
    }

}