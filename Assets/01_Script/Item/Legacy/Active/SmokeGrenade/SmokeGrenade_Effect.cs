using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
/// ScriptableObject 기반 액티브 아이템.
/// IActive를 구현하며, ItemManager가 StartCoroutine으로 Activate를 실행한다.
///
/// 동작 흐름:
///   1) Activate 호출 → owner 위치에 1차 연막 오브젝트 소환
///   2) secondaryDelay 초 후 → 정사면체 꼭짓점 4곳에 2차 연막 4개 소환
///   3) duration 만료 → 코루틴 자연 종료 (오브젝트들은 자체 lifetime으로 알아서 소멸)
///
/// 정사면체 수치:
///   - tetrahedronEdge 값 하나를 입력하면,
///     중심에서 꼭짓점까지의 거리 = edge * sqrt(6) / 4
///     4개의 꼭짓점은 그 거리에 균등 배치된다.
///
/// Mirror 네트워크:
///   - owner에 NetworkIdentity가 있다고 가정.
///   - 서버에서만 NetworkServer.Spawn()으로 소환.
///   - 소환되는 프리팹(smokePrefab)에는 NetworkIdentity + NetworkSmokeGrenade가 붙어 있어야 한다.
/// </summary>
[CreateAssetMenu(menuName = "Item/Active/SmokeGrenade/Effect")]
public class SmokeGrenade_Effect : ScriptableObject, IActive
{
    [Header("아이템 설정")]
    [SerializeField] private float duration = 12f;

    [Header("연막탄 생성")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 3f);
    [Tooltip("투척 비주얼의 비행 시간 (초)")]
    [SerializeField] private float throwDuration = 0.8f;
    [Tooltip("포물선 최대 높이")]
    [SerializeField] private float throwArcHeight = 3f;

    [Header("연막 오브젝트 생성")]
    [Tooltip("연막탄 투척 이후 연막이 생성되기까지의 시간")]
    [SerializeField] private float secondaryDelay = 2f;
    [Tooltip("연막 간의 거리. 클수록 연막탄의 범위가 넓어진다.")]
    [SerializeField] private float tetrahedronEdge = 6f;

    [Header("연막 설정")]
    [SerializeField] private Color smokeColor = Color.white;
    [SerializeField] private float smokeRadius = 5f;
    [SerializeField] private float smokeLifetime = 10f;

    [Header("프리팹")]
    [Tooltip("투척 수류탄 프리펩")]
    [SerializeField] private GameObject grenadePrefab;
    [Tooltip("연막 효과 프리팹")]
    [SerializeField] private GameObject smokePrefab;

    // ── 인스턴스별 런타임 상태 ──
    // ScriptableObject는 공유 에셋이므로 동시 사용 시 충돌 가능.
    // 멀티 플레이어 환경에서는 owner별 Dictionary로 관리하는 것을 권장.
    private GameObject thrownGrenadeInstance;

    public float AvailableTime => duration;

    public virtual IEnumerator Activate(GameObject owner)
    {
        if (owner == null) yield break;

        Debug.Log("[NetworkSmokeSpawner] 활성화 시작");

        // 연막탄 생성
        // ── 투척 목표 위치 계산 ──
        Vector3 targetPos = CalculatePrimaryPosition(owner.transform);

        // ── 수류탄 비주얼 오브젝트 생성 & 투척 ──
        thrownGrenadeInstance = SpawnThrownGrenade(owner);

        if (thrownGrenadeInstance != null)
        {
            // 포물선 이동: 서버에서 위치를 갱신하면 NetworkTransform이 클라이언트에 동기화
            Vector3 startPos = owner.transform.position + owner.transform.up * 1.5f; // 어깨 높이
            float elapsed = 0f;

            while (elapsed < throwDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / throwDuration);

                // 수평 선형 보간
                Vector3 flatPos = Vector3.Lerp(startPos, targetPos, t);

                // 수직 포물선 (0→peak→0)
                float arc = throwArcHeight * 4f * t * (1f - t);
                flatPos.y += arc;

                if (thrownGrenadeInstance != null)
                    thrownGrenadeInstance.transform.position = flatPos;

                yield return null;
            }

            // 최종 위치 보정
            if (thrownGrenadeInstance != null)
                thrownGrenadeInstance.transform.position = targetPos;
        }
        else
        {
            // 수류탄 비주얼 생성 실패 시 바로 대기
            yield return new WaitForSeconds(throwDuration);
        }

        // ── 1차 연막 소환 (착탄 지점) ──
        SpawnSmokeNetwork(targetPos, owner);

        // 일정 시간 대기 후 연막 생성
        yield return new WaitForSeconds(secondaryDelay);

        // 연막 오브젝트 생성
        Vector3[] vertices = GetTetrahedronVertices(targetPos, tetrahedronEdge);
        for (int i = 0; i < vertices.Length; i++)
        {
            SpawnSmokeNetwork(vertices[i], owner);
        }

        //duration 대기
        float remaining = duration - throwDuration - secondaryDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        Debug.Log("[NetworkSmokeSpawner] 활성화 종료");
    }

    public virtual void OnDeactivate(GameObject owner)
    {
        Debug.Log("[SmokeGrenade_Effect] 비활성화");

        // 던져진 수류탄 제거
        if (thrownGrenadeInstance != null)
        {
            if (NetworkServer.active)
                NetworkServer.Destroy(thrownGrenadeInstance);
            else
                Object.Destroy(thrownGrenadeInstance);

            thrownGrenadeInstance = null;
        }
    }

    private GameObject SpawnThrownGrenade(GameObject owner)
    {
        NetworkIdentity ownerIdentity = owner.GetComponent<NetworkIdentity>();
        if (ownerIdentity == null || !NetworkServer.active)
        {
            Debug.LogWarning("[SmokeGrenade_Effect] 서버가 아니거나 NetworkIdentity가 없음.");
            return null;
        }

        GameObject grenadeObj;
        Vector3 startPos = owner.transform.position + owner.transform.up * 1.5f;

        if (grenadePrefab != null)
        {
            grenadeObj = Instantiate(grenadePrefab, startPos, Quaternion.identity);
        }
        else
        {
            // 프리팹 미설정 시 런타임 폴백: 작은 구체
            grenadeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenadeObj.name = "smoke";
            grenadeObj.transform.position = startPos;
            grenadeObj.transform.localScale = Vector3.one * 0.15f;

            // 콜라이더 제거
            Collider col = grenadeObj.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            if (grenadeObj.GetComponent<NetworkIdentity>() == null)
                grenadeObj.AddComponent<NetworkIdentity>();

            if (grenadeObj.GetComponent<NetworkTransformReliable>() == null)
                grenadeObj.AddComponent<NetworkTransformReliable>();
        }

        NetworkServer.Spawn(grenadeObj);
        return grenadeObj;
    }


    // 1차 소환 위치를 owner 기준으로 계산.
    private Vector3 CalculatePrimaryPosition(Transform ownerTransform)
    {
        return ownerTransform.position
             + ownerTransform.forward * spawnOffset.z
             + ownerTransform.up * spawnOffset.y
             + ownerTransform.right * spawnOffset.x;
    }

    

    // Mirror 네트워크를 통해 연막 오브젝트를 소환한다.
    // 서버에서만 동작. 프리팹이 없으면 런타임 생성 폴백.
    private void SpawnSmokeNetwork(Vector3 position, GameObject owner)
    {
        // 서버 체크: owner의 NetworkIdentity를 통해 확인
        NetworkIdentity ownerIdentity = owner.GetComponent<NetworkIdentity>();
        if (ownerIdentity == null || !NetworkServer.active)
        {
            Debug.LogWarning("[NetworkSmokeSpawner] 서버가 아니거나 NetworkIdentity가 없음.");
            return;
        }

        GameObject smokeObj;

        if (smokePrefab != null)
        {
            // 프리팹 기반 소환
            smokeObj = Instantiate(smokePrefab, position, Quaternion.identity);
        }
        else
        {
            // 프리팹 미설정 시 런타임 생성 폴백
            smokeObj = new GameObject("NetworkSmokeGrenade_Runtime");
            smokeObj.transform.position = position;

            // NetworkIdentity가 있어야 Spawn 가능
            if (smokeObj.GetComponent<NetworkIdentity>() == null)
                smokeObj.AddComponent<NetworkIdentity>();

            if (smokeObj.GetComponent<ThrownGrenade>() == null)
                smokeObj.AddComponent<ThrownGrenade>();
        }

        // 파라미터 주입
        ThrownGrenade smoke = smokeObj.GetComponent<ThrownGrenade>();
        if (smoke != null)
        {
            smoke.smokeColor = smokeColor;
            smoke.maxRadius = smokeRadius;
            smoke.lifetime = smokeLifetime;
        }

        // 네트워크 소환
        NetworkServer.Spawn(smokeObj);
    }

    //center 기준으로 정사면체의 꼭짓점 위치로 연막 오브젝트 생성
    private static Vector3[] GetTetrahedronVertices(Vector3 center, float edge)
    {
        // 외접구 반지름
        float R = edge * Mathf.Sqrt(6f) / 4f;

        Vector3[] verts = new Vector3[4];

        // 꼭대기
        verts[0] = center + new Vector3(0f, R, 0f);

        // 아래 3개 (중심에서 -R/3 높이, 120도 간격)
        float bottomY = -R / 3f;
        float horizontalR = R * Mathf.Sqrt(8f / 9f); // 아래 삼각형의 외접원 반지름

        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120f - 90f) * Mathf.Deg2Rad; // -90°, 30°, 150°
            float x = horizontalR * Mathf.Cos(angle);
            float z = horizontalR * Mathf.Sin(angle);
            verts[i + 1] = center + new Vector3(x, bottomY, z);
        }

        return verts;
    }

    
}