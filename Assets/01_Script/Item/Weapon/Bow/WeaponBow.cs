using UnityEngine;

// 활 무기 컨트롤러.
// 좌클릭 유지 → 화살 생성 및 활에 귀속, 당긴 시간에 비례하여 발사 위력 증가.
// 좌클릭 해제 → 활이 바라보는 방향으로 화살 발사.
public class WeaponBow : MonoBehaviour
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

    // 현재 장전 중인 화살
    private WeaponArrow loadedArrow;
    private float chargeTimer;
    private bool isCharging;
    private float lifeTimer;

    /// 현재 차징 비율 (0~1). 외부에서 UI 등에 활용 가능.
    public float ChargeRatio => isCharging ? Mathf.Clamp01(chargeTimer / maxChargeTime) : 0f;

    private void Update()
    {
        if (arrow == null) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer > itemStat.availableTime)
        {
            Destroy(this.gameObject);
            Destroy(arrow);
            return;
        }
        // 좌클릭 시작 → 화살 장전
        if (Input.GetMouseButtonDown(0) && !isCharging)
        {
            BeginCharge();
        }

        // 좌클릭 유지 → 차징 시간 누적
        if (isCharging)
        {
            chargeTimer += Time.deltaTime;

            // 장전된 화살이 활 위치를 따라다님
            FollowNockPoint();
        }

        // 좌클릭 해제 → 발사
        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            ReleaseArrow();
        }
    }

    // 화살을 생성하고 활에 장전한다.
    private void BeginCharge()
    {
        Transform spawnPoint = nockPoint != null ? nockPoint : transform;

        GameObject arrowObj = Instantiate(arrow, spawnPoint.position, spawnPoint.rotation);
        loadedArrow = arrowObj.GetComponent<WeaponArrow>();

        if (loadedArrow == null)
        {
            Debug.LogError("[WeaponBow] arrowPrefab에 WeaponArrow 컴포넌트가 없습니다.");
            Destroy(arrowObj);
            return;
        }

        // 화살을 장전 상태로 설정 (발사 전까지 자체 로직 비활성)
        loadedArrow.SetNocked(true);

        chargeTimer = 0f;
        isCharging = true;
    }

    // 장전된 화살이 활의 nockPoint를 따라가도록 위치/회전 갱신.
    private void FollowNockPoint()
    {
        if (loadedArrow == null)
        {
            // 화살이 외부 요인으로 파괴된 경우 차징 취소
            CancelCharge();
            return;
        }

        Transform spawnPoint = nockPoint != null ? nockPoint : transform;
        loadedArrow.transform.position = spawnPoint.position;
        loadedArrow.transform.rotation = spawnPoint.rotation;
    }

    // 차징된 시간에 비례하는 속도로 화살을 발사한다.
    private void ReleaseArrow()
    {
        if (loadedArrow == null)
        {
            CancelCharge();
            return;
        }

        float ratio = Mathf.Clamp01(chargeTimer / maxChargeTime);
        float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, ratio);

        // 활이 바라보는 방향 (forward)
        Vector3 direction = (nockPoint != null ? nockPoint : transform).forward;

        loadedArrow.Launch(direction, launchSpeed);

        loadedArrow = null;
        chargeTimer = 0f;
        isCharging = false;
    }

    // 차징을 취소하고 상태를 초기화한다.
    private void CancelCharge()
    {
        if (loadedArrow != null)
            Destroy(loadedArrow.gameObject);

        loadedArrow = null;
        chargeTimer = 0f;
        isCharging = false;
    }
}