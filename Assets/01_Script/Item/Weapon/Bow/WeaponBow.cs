using Mirror;
using UnityEngine;

// 활 무기 컨트롤러.
// 좌클릭 유지 → 화살 생성 및 활에 귀속, 당긴 시간에 비례하여 발사 위력 증가.
// 좌클릭 해제 → 활이 바라보는 방향으로 화살 발사.
public class WeaponBow : NetworkBehaviour, IPlayerWeapon
{
    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("화살 설정")]
    [SerializeField] private GameObject arrow;

    [Tooltip("화살이 생성될 위치 (활의 시위 중앙). 비워두면 자신의 Transform 사용.")]
    [SerializeField] private Transform nockPoint;

    [Header("차징 설정")]
    [Tooltip("최소 발사 속도 (당기자마자 놓았을 때)")]
    [SerializeField] private float minLaunchSpeed = 5f;

    [Tooltip("최대 발사 속도 (완전히 당겼을 때)")]
    [SerializeField] private float maxLaunchSpeed = 40f;

    [Tooltip("최대 속도에 도달하는 데 걸리는 시간 (초)")]
    [SerializeField] private float maxChargeTime = 1.4f;

    [Header("추적 설정")]
    [Tooltip("플레이어 기준 화살 위치 오프셋")]
    [SerializeField] public Vector3 followPositionOffset;

    [Tooltip("플레이어 기준 화살 회전 오프셋")]
    [SerializeField] public Vector3 followRotationOffset;

    [Header("히트박스")]
    [Tooltip("활 오브젝트의 히트박스. 비워두면 자식에서 자동 탐색.")]
    [SerializeField] private CharacterHitBox weaponHitbox;

    [Header("활 애니메이션")]
    [Tooltip("활의 애니메이션 컨트롤러.")]
    [SerializeField] private BowAnimationController bowAnim;

    // 현재 장전 중인 화살-서버사이드 관리
    private WeaponArrow loadedArrow;
    [SyncVar] private GameObject loadedArrowObj;

    //차징 상태-클라이언트 관리
    private float chargeTimer;
    private bool isCharging;
    private float lifeTimer;

    //쿨타임
    private float recoveryTimer;
    private bool isRecovered = true;



    //소유자 참조
    [SerializeField] public GameObject owner;

    /// 현재 차징 비율 (0~1). 외부에서 UI 등에 활용 가능.
    public float ChargeRatio => isCharging ? Mathf.Clamp01(chargeTimer / maxChargeTime) : 0f;


    private void Awake()
    {
        if (weaponHitbox != null)
        {
            weaponHitbox.damage = itemStat.damage;
            weaponHitbox.DisableHitbox();
        }
    }

    public void SetUser(GameObject user)
    {
        owner = user;
        var model = user.GetComponent<ICharacterModel>();
        if (model != null) model.RequestSetHasBow(true);
        // 서버에서 호출되면 모든 클라이언트에 전파
        RpcSetUser(user);
    }

    [ClientRpc]
    private void RpcSetUser(GameObject user)
    {
        owner = user;
        WeaponEquipHandler handler = GetComponent<WeaponEquipHandler>();
        if (handler != null)
            handler.Equip(user);
    }

    private void Update()
    {
        if (arrow == null) return;

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
            if (!isRecovered)
            {
                recoveryTimer += Time.deltaTime;
                if (recoveryTimer >= itemStat.RecoveryDelay)
                {
                    isRecovered = true;
                    //recoveryTimer = 0;
                }
            }
            if (lifeTimer > itemStat.availableTime)
            {
                if (owner != null)
                {
                    var model = owner.GetComponent<ICharacterModel>();
                    if (model != null)
                    {
                        model.RequestSetHasBow(false);
                    }
                }
                //모든 클라이언트에서 복원
                RpcUnequip();

                if (loadedArrowObj != null)
                    NetworkServer.Destroy(loadedArrowObj);

                
                NetworkServer.Destroy(gameObject);
                return;
            }
        }

        //장전된 화살이 활의 nockPoint를 따라가도록 위치/회전 갱신.
        if (isCharging && loadedArrowObj != null)
        {
            /*
            if (loadedArrow == null)
            {
                // 화살이 외부 요인으로 파괴된 경우 차징 취소
                CancelCharge();
                return;
            }
            */

            Transform spawnPoint = nockPoint != null ? nockPoint : transform;
            loadedArrowObj.transform.position = spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset);
            loadedArrowObj.transform.rotation = spawnPoint.rotation * 
                Quaternion.Euler(followRotationOffset.x, followRotationOffset.y, followRotationOffset.z);
        }

        // 좌클릭 시작 → 화살 장전
        if (Input.GetMouseButtonDown(0) && !isCharging && isOwned && isRecovered)
        {
            isCharging = true;
            chargeTimer = 0;
            CmdBeginCharge();

            // 활 당기기 애니메이션
            var model = owner.GetComponent<ICharacterModel>();
            if (model != null) model.RequestSetBowDraw(true);
        }
        

        // 좌클릭 유지 → 차징 시간 누적
        if (isCharging && isOwned && Input.GetMouseButton(0))
        {
            chargeTimer += Time.deltaTime;
        }

        // 좌클릭 해제 → 발사
        if (Input.GetMouseButtonUp(0) && isCharging && isOwned)
        {
            float ratio = Mathf.Clamp01(chargeTimer / maxChargeTime);
            isCharging = false; 
            chargeTimer = 0f;
            
            CmdReleaseArrow(ratio);

            // 발사 애니메이션
            var model = owner.GetComponent<ICharacterModel>();
            if (model != null) model.RequestBowRelease();
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
        if (!isRecovered) return;
        Transform spawnPoint = nockPoint != null ? nockPoint : transform;

        GameObject arrowObj = Instantiate(arrow, spawnPoint.position + spawnPoint.TransformDirection(followPositionOffset), 
            spawnPoint.rotation * Quaternion.Euler(followRotationOffset.x, followRotationOffset.y, followRotationOffset.z));
        NetworkServer.Spawn(arrowObj);

        loadedArrowObj = arrowObj;
        loadedArrow = arrowObj.GetComponent<WeaponArrow>();

        if (loadedArrow == null)
        {
            Debug.LogError("[WeaponBow] arrowPrefab에 WeaponArrow 컴포넌트가 없습니다.");
            NetworkServer.Destroy(arrowObj);
            return;
        }

        // 화살을 장전 상태로 설정 (발사 전까지 자체 로직 비활성)
        loadedArrow.SetNocked(true);
        loadedArrow.SetOwner(owner);

        isCharging = true;

        bowAnim.RpcSetPull(true);
        //클라잉언트에 장전 상태 알림.
        RpcOnBeginCharge();
    }

    [ClientRpc]
    void RpcOnBeginCharge()
    {
        isCharging = true;
    }



    // 차징된 시간에 비례하는 속도로 화살을 발사한다.
    [Command(requiresAuthority = true)]
    private void CmdReleaseArrow(float chargeRatio)
    {
        if (loadedArrow == null)
        {
            CancelCharge();
            return;
        }

        float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, chargeRatio);

        // 활이 바라보는 방향 (forward)
        Transform spawnPoint = (nockPoint != null ? nockPoint : transform);
        Quaternion adjusted = spawnPoint.rotation * Quaternion.Euler(followRotationOffset);
        Vector3 direction = adjusted * Vector3.forward;

        loadedArrow.Launch(direction, launchSpeed);

        loadedArrow = null;
        chargeTimer = 0f;
        isCharging = false;

        RpcOnRelease();
        bowAnim.RpcSetPull(false);
        isRecovered = false;
        recoveryTimer = 0f;
    }

    [ClientRpc]
    void RpcOnRelease()
    {
        isCharging = false;
    }

    // 차징을 취소하고 상태를 초기화한다.
    private void CancelCharge()
    {
        if (loadedArrow != null)
        {
            NetworkServer.Destroy(loadedArrow.gameObject);
        }

        loadedArrow = null;
        chargeTimer = 0f;
        isCharging = false;
    }

    public void ThrowWeapon()
    {
        Debug.Log("이걸 왜던짐?ㅋ");
        return;
    }
}