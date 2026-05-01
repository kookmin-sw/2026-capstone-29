using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
/// ScriptableObject 기반 액티브 아이템 (연막탄). <see cref="SmokeGrenade_Effect"/>의 Unified 버전.
/// IActive를 구현하며, UnifiedItemManager가 StartCoroutine으로 Activate를 실행한다.
///
/// 동작 흐름:
///   1) Activate 호출 → owner 위치에 수류탄 비주얼 소환 + 포물선 투척
///   2) 착탄 시점 1차 연막 + secondaryDelay 후 정사면체 꼭짓점 4곳에 2차 연막 4개 소환
///   3) duration 만료 → 코루틴 자연 종료 (오브젝트들은 자체 lifetime으로 알아서 소멸)
///
/// 온/오프라인 분기:
///   - 온라인: 기존 동작 유지. NetworkServer.Spawn으로 등록.
///   - 오프라인: NetworkServer.active == false이므로 단순 Instantiate +
///     NetworkIdentity 비활성(HardenOfflineObject)으로 Mirror 간섭 차단.
/// </summary>
[CreateAssetMenu(menuName = "Item/Active/SmokeGrenade/UnifiedEffect")]
public class UnifiedSmokeGrenade_Effect : ScriptableObject, IActive
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
    [Tooltip("투척 수류탄 프리팹")]
    [SerializeField] private GameObject grenadePrefab;
    [Tooltip("연막 효과 프리팹 (UnifiedThrownGrenade 또는 ThrownGrenade 컴포넌트 보유)")]
    [SerializeField] private GameObject smokePrefab;

    // ── 인스턴스별 런타임 상태 ──
    // ScriptableObject는 공유 에셋이므로 동시 사용 시 충돌 가능.
    // 멀티 플레이어 환경에서는 owner별 Dictionary로 관리하는 것을 권장.
    private GameObject thrownGrenadeInstance;

    public float AvailableTime => duration;

    public virtual IEnumerator Activate(GameObject owner)
    {
        if (owner == null) yield break;

        Debug.Log("[UnifiedSmokeGrenade] 활성화 시작");

        // 투척 목표 위치 계산
        Vector3 targetPos = CalculatePrimaryPosition(owner.transform);

        // 수류탄 비주얼 오브젝트 생성 & 투척
        thrownGrenadeInstance = SpawnThrownGrenade(owner);

        if (thrownGrenadeInstance != null)
        {
            Vector3 startPos = owner.transform.position + owner.transform.up * 1.5f;
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

            if (thrownGrenadeInstance != null)
                thrownGrenadeInstance.transform.position = targetPos;
        }
        else
        {
            yield return new WaitForSeconds(throwDuration);
        }

        // 1차 연막 (착탄 지점)
        SpawnSmoke(targetPos, owner);

        yield return new WaitForSeconds(secondaryDelay);

        // 정사면체 꼭짓점 4곳에 2차 연막
        Vector3[] vertices = GetTetrahedronVertices(targetPos, tetrahedronEdge);
        for (int i = 0; i < vertices.Length; i++)
        {
            SpawnSmoke(vertices[i], owner);
        }

        // duration 잔여 대기
        float remaining = duration - throwDuration - secondaryDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        Debug.Log("[UnifiedSmokeGrenade] 활성화 종료");
    }

    public virtual void OnDeactivate(GameObject owner)
    {
        Debug.Log("[UnifiedSmokeGrenade] 비활성화");

        // 던져진 수류탄 비주얼 제거 (착탄 전 강제 종료된 경우)
        if (thrownGrenadeInstance != null)
        {
            if (AuthorityGuard.IsOffline)
            {
                Object.Destroy(thrownGrenadeInstance);
            }
            else if (NetworkServer.active)
            {
                NetworkServer.Destroy(thrownGrenadeInstance);
            }
            else
            {
                Object.Destroy(thrownGrenadeInstance);
            }

            thrownGrenadeInstance = null;
        }
    }

    // -----------------------------
    // 수류탄 비주얼 스폰 (오프/온라인 공용)
    // -----------------------------
    private GameObject SpawnThrownGrenade(GameObject owner)
    {
        Vector3 startPos = owner.transform.position + owner.transform.up * 1.5f;

        if (AuthorityGuard.IsOffline)
        {
            return SpawnThrownGrenadeLocal(startPos);
        }

        return SpawnThrownGrenadeNetwork(owner, startPos);
    }

    private GameObject SpawnThrownGrenadeLocal(Vector3 startPos)
    {
        GameObject grenadeObj;

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

            Collider col = grenadeObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            // 오프라인 폴백에서는 NetworkIdentity/NetworkTransform 추가하지 않음
        }

        HardenOfflineObject(grenadeObj);
        return grenadeObj;
    }

    private GameObject SpawnThrownGrenadeNetwork(GameObject owner, Vector3 startPos)
    {
        NetworkIdentity ownerIdentity = owner.GetComponent<NetworkIdentity>();
        if (ownerIdentity == null || !NetworkServer.active)
        {
            Debug.LogWarning("[UnifiedSmokeGrenade] 서버가 아니거나 NetworkIdentity가 없음.");
            return null;
        }

        GameObject grenadeObj;

        if (grenadePrefab != null)
        {
            grenadeObj = Instantiate(grenadePrefab, startPos, Quaternion.identity);
        }
        else
        {
            grenadeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenadeObj.name = "smoke";
            grenadeObj.transform.position = startPos;
            grenadeObj.transform.localScale = Vector3.one * 0.15f;

            Collider col = grenadeObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            if (grenadeObj.GetComponent<NetworkIdentity>() == null)
                grenadeObj.AddComponent<NetworkIdentity>();

            if (grenadeObj.GetComponent<NetworkTransformReliable>() == null)
                grenadeObj.AddComponent<NetworkTransformReliable>();
        }

        NetworkServer.Spawn(grenadeObj);
        return grenadeObj;
    }

    // -----------------------------
    // 1차 소환 위치 계산
    // -----------------------------
    private Vector3 CalculatePrimaryPosition(Transform ownerTransform)
    {
        return ownerTransform.position
             + ownerTransform.forward * spawnOffset.z
             + ownerTransform.up * spawnOffset.y
             + ownerTransform.right * spawnOffset.x;
    }

    // -----------------------------
    // 연막 오브젝트 스폰 (오프/온라인 공용)
    // -----------------------------
    private void SpawnSmoke(Vector3 position, GameObject owner)
    {
        if (AuthorityGuard.IsOffline)
        {
            SpawnSmokeLocal(position);
            return;
        }

        SpawnSmokeNetwork(position, owner);
    }

    private void SpawnSmokeLocal(Vector3 position)
    {
        GameObject smokeObj;

        if (smokePrefab != null)
        {
            smokeObj = Instantiate(smokePrefab, position, Quaternion.identity);
        }
        else
        {
            smokeObj = new GameObject("UnifiedThrownGrenade_Runtime");
            smokeObj.transform.position = position;
            // 오프라인에서는 UnifiedThrownGrenade가 NetworkBehaviour라서 NetworkIdentity 필요할 수 있음
            // → HardenOfflineObject에서 NetworkIdentity 비활성화
            if (smokeObj.GetComponent<UnifiedThrownGrenade>() == null)
                smokeObj.AddComponent<UnifiedThrownGrenade>();
        }

        // 파라미터 주입 (UnifiedThrownGrenade 또는 ThrownGrenade 둘 다 호환)
        InjectSmokeParameters(smokeObj);

        HardenOfflineObject(smokeObj);
    }

    private void SpawnSmokeNetwork(Vector3 position, GameObject owner)
    {
        NetworkIdentity ownerIdentity = owner.GetComponent<NetworkIdentity>();
        if (ownerIdentity == null || !NetworkServer.active)
        {
            Debug.LogWarning("[UnifiedSmokeGrenade] 서버가 아니거나 NetworkIdentity가 없음.");
            return;
        }

        GameObject smokeObj;

        if (smokePrefab != null)
        {
            smokeObj = Instantiate(smokePrefab, position, Quaternion.identity);
        }
        else
        {
            smokeObj = new GameObject("NetworkSmokeGrenade_Runtime");
            smokeObj.transform.position = position;

            if (smokeObj.GetComponent<NetworkIdentity>() == null)
                smokeObj.AddComponent<NetworkIdentity>();

            if (smokeObj.GetComponent<UnifiedThrownGrenade>() == null
                && smokeObj.GetComponent<ThrownGrenade>() == null)
                smokeObj.AddComponent<UnifiedThrownGrenade>();
        }

        InjectSmokeParameters(smokeObj);

        NetworkServer.Spawn(smokeObj);
    }

    /// <summary>
    /// UnifiedThrownGrenade 우선, 없으면 legacy ThrownGrenade에 파라미터 주입.
    /// </summary>
    private void InjectSmokeParameters(GameObject smokeObj)
    {
        UnifiedThrownGrenade uSmoke = smokeObj.GetComponent<UnifiedThrownGrenade>();
        if (uSmoke != null)
        {
            uSmoke.smokeColor = smokeColor;
            uSmoke.maxRadius = smokeRadius;
            uSmoke.lifetime = smokeLifetime;
            return;
        }

        ThrownGrenade legacySmoke = smokeObj.GetComponent<ThrownGrenade>();
        if (legacySmoke != null)
        {
            legacySmoke.smokeColor = smokeColor;
            legacySmoke.maxRadius = smokeRadius;
            legacySmoke.lifetime = smokeLifetime;
        }
    }

    // -----------------------------
    // 정사면체 꼭짓점 계산
    // -----------------------------
    private static Vector3[] GetTetrahedronVertices(Vector3 center, float edge)
    {
        // 외접구 반지름
        float R = edge * Mathf.Sqrt(6f) / 4f;

        Vector3[] verts = new Vector3[4];

        // 꼭대기
        verts[0] = center + new Vector3(0f, R, 0f);

        // 아래 3개 (중심에서 -R/3 높이, 120도 간격)
        float bottomY = -R / 3f;
        float horizontalR = R * Mathf.Sqrt(8f / 9f);

        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120f - 90f) * Mathf.Deg2Rad; // -90°, 30°, 150°
            float x = horizontalR * Mathf.Cos(angle);
            float z = horizontalR * Mathf.Sin(angle);
            verts[i + 1] = center + new Vector3(x, bottomY, z);
        }

        return verts;
    }

    // -----------------------------
    // 오프라인 하드닝 헬퍼 (UnifiedSetItem과 동일 패턴)
    // -----------------------------
    private static void HardenOfflineObject(GameObject obj)
    {
        if (obj == null) return;
        if (!AuthorityGuard.IsOffline) return; // 온라인에선 Mirror가 관리

        if (obj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;

        if (!obj.activeSelf) obj.SetActive(true);
    }
}