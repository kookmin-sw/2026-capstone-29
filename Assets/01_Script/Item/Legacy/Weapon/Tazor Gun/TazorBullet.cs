using UnityEngine;
using Mirror;

// 화살 오브젝트.
// 장전(Nocked) 상태에서는 활을 따라다니며 대기.
// 발사(Launched) 후에는 지정된 방향으로 날아가며, 10초 경과 또는 피격 판정 시 파괴.
public class TazorBullet : NetworkBehaviour, IWeaponHitBox
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [SyncVar] private GameObject owner;

    [Header("히트박스")]
    [SerializeField] private CharacterHitBox hitBox;

    // 내부 상태. SyncVar로 상태 동기화.
    [SyncVar] private bool isNocked;      // 발사 준비 상태
    [SyncVar] private bool isLaunched;    // 발사된 상태

    [SyncVar] private Vector3 flyDirection;
    [SyncVar] private float flySpeed;

    private float lifeTimer;

    private void Awake()
    {
        if (hitBox == null)
        {
            hitBox = GetComponent<CharacterHitBox>();
            hitBox.damage = itemStat.damage;
            flySpeed = itemStat.speed;
        }
    }

    private void Update()
    {
        // 장전 상태에서는 아무것도 하지 않음 (WeaponTazorGun이 위치를 제어)
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

    // 장전 상태 설정. true이면 총알이 총에 귀속되어 대기.
    public void SetNocked(bool nocked)
    {
        isNocked = nocked;
        isLaunched = false;
        lifeTimer = 0f;

        if (hitBox != null)
        {
            hitBox.DisableHitbox();
        }
    }

    // 총알을 발사한다. WeaponTazorGun에서 호출.
    public void Launch(Vector3 direction)
    {
        isNocked = false;
        isLaunched = true;
        lifeTimer = 0f;

        flyDirection = direction.normalized;
        flySpeed = itemStat.speed;
        transform.rotation = Quaternion.LookRotation(flyDirection);

        // 화살이 날아가는 방향을 바라보도록 회전
        transform.rotation = Quaternion.LookRotation(flyDirection);
        if (hitBox != null)
        {
            hitBox.EnableHitbox();
        }

        // 클라이언트에도 발사 상태를 즉시 전파
        RpcLaunch(flyDirection, flySpeed);
    }

    [ClientRpc]
    private void RpcEnableHitbox()
    {
        if (hitBox != null)
            hitBox.EnableHitbox();
    }
    [ClientRpc]
    private void RpcLaunch(Vector3 direction, float speed)
    {
        isNocked = false;
        isLaunched = true;
        flyDirection = direction;
        flySpeed = speed;
        transform.rotation = Quaternion.LookRotation(flyDirection);

        if (hitBox != null)
            hitBox.EnableHitbox();
    }
    public void SetOwner(GameObject user)
    {
        owner = user;
    }
    public GameObject GetOwner()
    {
        return owner;
    }

}