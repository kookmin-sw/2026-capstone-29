using Mirror;
using UnityEngine;

public class WeaponMelee : NetworkBehaviour, IPlayerWeapon, IWeaponHitBox
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("히트박스")]
    [Tooltip("근접 무기의 히트박스. 비워두면 자신에서 자동 탐색.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    [Header("던지기 설정")]
    [Tooltip("던질 때 발사 속도")]

    [SyncVar] private GameObject owner;
    [SyncVar] private bool isThrown;
    [SyncVar] private Vector3 flyDirection;
    [SyncVar] private float flySpeed;
    [SyncVar] private float rollSpeed;

    private float lifeTimer;

    private void Awake()
    {
        if (weaponHitbox == null)
        {
            weaponHitbox = GetComponent<CharacterHitBox>();
        }

        if (weaponHitbox != null)
        {
            weaponHitbox.damage = itemStat.damage;
            weaponHitbox.DisableHitbox();
        }

    }

    public void SetUser(GameObject user)
    {
        owner = user;

        // 서버에서 히트박스 활성화
        if (weaponHitbox != null)
            weaponHitbox.EnableHitbox();

        RpcSetUser(user, "CombatGirls_Sword_Shield/root/add_weapon_r");
    }

    [ClientRpc]
    private void RpcSetUser(GameObject user, string socketPath)
    {
        owner = user;

        // 클라이언트에서도 히트박스 활성화
        if (weaponHitbox != null)
            weaponHitbox.EnableHitbox();

        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
            handler.Equip(user, socketPath);
    }


    public void SetOwner(GameObject user) { owner = user; }
    public GameObject GetOwner() { return owner; }


    private void Update()
    {
        if (isThrown)
        {
            transform.position += flyDirection * flySpeed * Time.deltaTime;
            transform.Rotate(Vector3.right, rollSpeed * Time.deltaTime, Space.Self);
        }

        if (isServer)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer > itemStat.availableTime)
            {
                // 아직 장착 중이라면 해제
                if (!isThrown)
                {
                    RpcUnequip();
                }

                NetworkServer.Destroy(gameObject);
                return;
            }
        }

        // 던지기 입력 (소유 클라이언트만). 이 구조는 Input 받는 위치에서 한번에 처리해야 한다.
        if (isOwned && !isThrown && Input.GetKeyDown("e"))
        {
            // 던질 방향: 소유자의 전방
            Vector3 direction = owner != null
                ? owner.transform.forward
                : transform.forward;

            CmdThrowWeapon(direction);
        }
    }

    //StartAssetsInputs에서 이 함수를 호출하면 된다. 인터페이스 IPlayerWeapon을 GetComponent하여 ThrowWeapon을 호출하면 사용 가능.
    public void ThrowWeapon()
    {
        if (isOwned && !isThrown)
        {
            // 던질 방향: 소유자의 전방
            Vector3 direction = owner != null
                ? owner.transform.forward
                : transform.forward;

            CmdThrowWeapon(direction);
        }
    }

    [Command(requiresAuthority = true)]
    private void CmdThrowWeapon(Vector3 direction)
    {
        if (isThrown) return; // 이미 던진 상태면 무시

        // 소켓에서 분리
        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
            handler.Unequip();

        // 상태 설정 (SyncVar → 모든 클라이언트 자동 동기화)
        isThrown = true;
        flyDirection = direction.normalized;
        flySpeed = itemStat.speed;
        rollSpeed = itemStat.rollSpeed;
        lifeTimer = 0f; // 던진 시점부터 수명 재시작

        // 던진 방향을 바라보도록 회전
        transform.rotation = Quaternion.LookRotation(flyDirection);



        // 클라이언트에 던짐 상태 알림 (SyncVar 외 추가 처리용)
        RpcOnThrown(flyDirection);
    }


    // 모든 클라이언트에 던짐 상태 반영.
    //SyncVar가 동기화하지만, 즉시 반영이 필요한 로직에도 사용 가능.

    [ClientRpc]
    private void RpcOnThrown(Vector3 direction)
    {
        isThrown = true;
        // 소켓 분리
        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
            handler.Unequip();

        // 히트박스 활성화
        if (weaponHitbox != null)
            weaponHitbox.EnableHitbox();

        // 방향 설정
        transform.rotation = Quaternion.LookRotation(direction);
    }


    [ClientRpc]
    private void RpcUnequip()
    {
        if (weaponHitbox != null)
            weaponHitbox.DisableHitbox();

        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
            handler.Unequip();
    }
}