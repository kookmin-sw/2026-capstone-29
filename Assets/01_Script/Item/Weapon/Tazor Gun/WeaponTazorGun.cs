using Mirror;
using UnityEngine;

public class WeaponTazorGun : NetworkBehaviour, IPlayerWeapon
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("tazor bullet 설정")]
    [SerializeField] private GameObject tb;

    [Tooltip("총알이 생성될 위치 (선두). 비워두면 자신의 Transform 사용.")]
    [SerializeField] private Transform nockPoint;

    [Header("추적 설정")]
    [Tooltip("플레이어 기준 화살 위치 오프셋")]
    [SerializeField] public Vector3 followPositionOffset;

    [Tooltip("플레이어 기준 화살 회전 오프셋")]
    [SerializeField] public Vector3 followRotationOffset;

    [Header("히트박스")]
    [Tooltip("오브젝트의 히트박스. 비워두면 자식에서 자동 탐색.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    // 현재 장전 중인 총알-서버사이드 관리
    private TazorBullet loadedBullet;
    [SyncVar] private GameObject loadedBulletObj;

    //차징 상태-클라이언트 관리
    private float lifeTimer;
    private int usedChance; // 사용한 횟수


    //소유자 참조
    [SerializeField] public GameObject owner;

    private void Awake()
    {
        if (weaponHitbox != null)
        {
            weaponHitbox.damage = itemStat.damage;
            weaponHitbox.EnableHitbox();
        }
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        CmdBeginCharge(); // 이 시점에선 네트워크가 준비되어 있으므로 정상 작동
    }


    public void SetUser(GameObject user)
    {
        owner = user;
        var model = user.GetComponent<NetworkCharacterModel>();
        //if (model != null) model.ServerSetHasBow(true);
        // 서버에서 호출되면 모든 클라이언트에 전파
        RpcSetUser(user, "CombatGirls_Sword_Shield/root/add_weapon_r");
    }

    [ClientRpc]
    private void RpcSetUser(GameObject user, string socketPath)
    {
        owner = user;
        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
            handler.Equip(user, socketPath);
    }

    private void Update()
    {
        if (tb == null) return;

        //부모 위치/방햐 추적 - 양측 클라이언트
        if (transform.parent != null)
        {
            transform.rotation = transform.parent.rotation;
            transform.position = transform.parent.position + transform.parent.forward * 1.5f;
        }

        //수명 관리는 서버에서만 해준다.
        if (isServer)
        {
            lifeTimer += Time.deltaTime;
            
            if (lifeTimer > itemStat.availableTime || usedChance >= itemStat.useableTime)
            {
                if (owner != null)
                {
                    var model = owner.GetComponent<NetworkCharacterModel>();
                    if (model != null)
                    {
                        model.ServerSetHasBow(false);
                    }
                }
                //모든 클라이언트에서 복원
                RpcUnequip();

                if (loadedBulletObj != null)
                    NetworkServer.Destroy(loadedBulletObj);


                NetworkServer.Destroy(gameObject);
                return;
            }
        }


        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        if (loadedBulletObj != null)
        {
            loadedBulletObj.transform.position = spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset);
            loadedBulletObj.transform.rotation = spawnPoint.rotation *
                Quaternion.Euler(followRotationOffset.x, followRotationOffset.y, followRotationOffset.z);
        }
        

        // 좌클릭 시작 → 총알 발사
        if (Input.GetMouseButtonDown(0) && isOwned)
        {
            //발사 로직 가져와라.
            CmdShot();

            var model = owner.GetComponent<NetworkCharacterModel>();
            //if (model != null) model.CmdSetBowDraw(true); // 여기서 애니메이션 조작.
        }

    }
    [ClientRpc]
    private void RpcUnequip()
    {
        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
            handler.Unequip();
    }
    // 화살을 생성하고 활에 장전한다.
    [Command(requiresAuthority = true)]
    private void CmdBeginCharge()
    {
        Transform spawnPoint = nockPoint != null ? nockPoint : transform;

        GameObject bulletObj = Instantiate(tb, spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset),
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset.x, followRotationOffset.y, followRotationOffset.z));
        NetworkServer.Spawn(bulletObj);

        loadedBulletObj = bulletObj;
        loadedBullet = bulletObj.GetComponent<TazorBullet>();

        if (loadedBullet == null)
        {
            Debug.LogError("[WeaponBow] arrowPrefab에 WeaponArrow 컴포넌트가 없습니다.");
            NetworkServer.Destroy(bulletObj);
            return;
        }
    }




    // 차징된 시간에 비례하는 속도로 화살을 발사한다.
    [Command(requiresAuthority = true)]
    private void CmdShot()
    {
        if (loadedBullet == null)
        {
            CancelShot();
            return;
        }

        // 활이 바라보는 방향 (forward)
        Transform spawnPoint = (nockPoint != null ? nockPoint : transform);
        Quaternion adjusted = spawnPoint.rotation * Quaternion.Euler(followRotationOffset);
        Vector3 direction = adjusted * Vector3.forward;

        usedChance++;

    }


    // 차징을 취소하고 상태를 초기화한다.
    private void CancelShot()
    {
        if (loadedBullet != null)
        {
            NetworkServer.Destroy(loadedBullet.gameObject);
        }
    }

    public void ThrowWeapon()
    {
        Debug.Log("이걸 왜던짐?ㅋ");
        return;
    }
}