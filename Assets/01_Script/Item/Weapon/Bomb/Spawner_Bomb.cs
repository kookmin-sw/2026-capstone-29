using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner_Bomb : MonoBehaviour
{

    [Header("아이템 정보")]
    [SerializeField] public ItemStatus itemStat;

    [Header("폭탄 오브젝트")]
    [SerializeField] private GameObject bomb;

    [Tooltip("폭탄이 생성될 위치 (플레이어의 손이 되면 좋겠음). 비워두면 자신의 Transform 사용.")]
    [SerializeField] private Transform spawnPos;

    [Header("차징 설정")]
    [Tooltip("최소 발사 속도 (당기자마자 놓았을 때)")]
    [SerializeField] private float minLaunchSpeed = 5f;

    [Tooltip("최대 발사 속도 (완전히 당겼을? 때)")]
    [SerializeField] private float maxLaunchSpeed = 40f;

    [Tooltip("최대 속도에 도달하는 데 걸리는 시간 (초)")]
    [SerializeField] private float maxChargeTime = 2.5f;

    // 현재 장전 중인 폭탄
    private Weapon_Bomb loadedBomb;
    private float chargeTimer;
    private bool isCharging;
    private float lifeTimer;

    private int lifeCounter = 0;
    private int spawnAmt = 3;


    // 현재 차징 비율 (0~1). 외부에서 UI 등에 활용 가능. 
    // 일단 활의 구조와 동일하게 좌클릭 차징 후 좌클릭을 떼면 발사되는 구조로 작성. 의논 후 수정 여부 확인.
    public float ChargeRatio => isCharging ? Mathf.Clamp01(chargeTimer / maxChargeTime) : 0f;

    private void Update()
    {
        if (bomb == null) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer > itemStat.availableTime)
        {
            Destroy(this.gameObject);
            return;
        }
        // 좌클릭 시작 → 폭탄 장전
        if (Input.GetMouseButtonDown(0) && !isCharging)
        {
            BeginCharge();
        }

        // 좌클릭 유지 → 차징 시간 누적
        if (isCharging)
        {
            chargeTimer += Time.deltaTime;

            // 장전된 폭탄이 플레이어를 따라다님
            FollowNockPoint();
        }

        // 좌클릭 해제 → 발사
        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            ReleaseArrow();
            lifeCounter++;
            //3회 발사시 스포너 파괴.
            if (lifeCounter >= spawnAmt)
            {
                Destroy(this.gameObject);
            }
        }
    }

    // 화살을 생성하고 활에 장전한다.
    private void BeginCharge()
    {
        Transform spawnPoint = spawnPos != null ? spawnPos : transform;

        GameObject bombObj = Instantiate(bomb, spawnPoint.position, spawnPoint.rotation);
        loadedBomb = bombObj.GetComponent<Weapon_Bomb>();

        if (loadedBomb == null)
        {
            Debug.LogError("[WeaponBow] arrowPrefab에 WeaponArrow 컴포넌트가 없습니다.");
            Destroy(bombObj);
            return;
        }

        // 폭탄을 장전 상태로 설정 (발사 전까지 자체 로직 비활성)
        loadedBomb.SetNocked(true);

        chargeTimer = 0f;
        isCharging = true;
    }

    // 장전된 폭탄을 던지기 전까지spawnPos를 따라가도록 위치/회전 갱신.
    private void FollowNockPoint()
    {
        if (loadedBomb == null)
        {
            // 폭탄이 외부 요인으로 파괴된 경우 차징 취소
            CancelCharge();
            return;
        }

        Transform spawnPoint = spawnPos != null ? spawnPos : transform;
        loadedBomb.transform.position = spawnPoint.position;
        loadedBomb.transform.rotation = spawnPoint.rotation;
    }

    // 차징된 시간에 비례하는 속도로 화살을 발사한다.
    private void ReleaseArrow()
    {
        if (loadedBomb == null)
        {
            CancelCharge();
            return;
        }

        float ratio = Mathf.Clamp01(chargeTimer / maxChargeTime);
        float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, ratio);

        // 플레이어가 바라보는 방향 (forward)
        Vector3 direction = (spawnPos != null ? spawnPos : transform).forward;

        loadedBomb.Launch(direction, launchSpeed);

        loadedBomb = null;
        chargeTimer = 0f;
        isCharging = false;
    }

    // 차징을 취소하고 상태를 초기화한다.
    private void CancelCharge()
    {
        if (loadedBomb != null)
            Destroy(loadedBomb.gameObject);

        loadedBomb = null;
        chargeTimer = 0f;
        isCharging = false;
    }
}
