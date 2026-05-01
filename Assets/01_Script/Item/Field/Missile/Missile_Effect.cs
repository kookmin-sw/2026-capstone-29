using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 운석 효과.
/// duration 동안 순차적으로 n개의 운석을 떨어뜨린다.
/// 각 운석: 랜덤 xz 위치 → Raycast로 y 결정 → 데칼(경고 원) 스폰 → m초 후 폭발 이펙트 + 데미지 판정.
///
/// Unified 변환:
/// - 권위 판정: isServer → AuthorityGuard.IsOffline || isServer
/// - Spawn/Destroy 채널: 오프라인은 Instantiate + 하드닝 / Destroy
/// - [Server] 어트리뷰트는 호스트 모드에서만 의미 있으므로 제거하고 권위 가드로 대체.
///   ScriptableObject가 호출하는 Initialize는 어차피 외부에서 권위 판정 후 호출되므로 안전.
/// </summary>
public class Missile_Effect : FieldEffect
{
    [Header("운석 설정")]
    [Tooltip("떨어뜨릴 운석 총 개수")]
    [SerializeField] private int missileCount = 5;

    [Tooltip("운석 생성 간격(초)")]
    [SerializeField] private float spawnInterval = 1f;

    [Tooltip("경고 원 표시 후 폭발까지의 시간(초)")]
    [SerializeField] private float warningDuration = 2f;

    [Tooltip("폭발 데미지")]
    [SerializeField] private float damage = 30f;

    [Tooltip("폭발 반경 (데칼 반경과 동일)")]
    [SerializeField] private float explosionRadius = 3f;

    [Header("낙하 범위 (두 꼭짓점으로 정의되는 직사각형)")]
    [SerializeField] private Vector2 cornerA = new Vector2(-10f, -10f);
    [SerializeField] private Vector2 cornerB = new Vector2(10f, 10f);

    [Header("Raycast 설정")]
    [SerializeField] private float raycastHeight = 100f;
    [SerializeField] private float raycastDistance = 200f;
    [SerializeField] private LayerMask groundLayerMask;

    [Header("프리팹")]
    [Tooltip("경고 원 데칼 프로젝터 프리팹 (NetworkIdentity 필수)")]
    [SerializeField] private GameObject warningDecalPrefab;

    [Tooltip("폭발 이펙트 프리팹 (NetworkIdentity 필수)")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Tooltip("폭발 이펙트 자동 제거 시간(초)")]
    [SerializeField] private float explosionLifetime = 2f;

    private readonly List<GameObject> _activeDecals = new List<GameObject>();
    private readonly List<GameObject> _activeExplosions = new List<GameObject>();

    private bool HasAuthority => AuthorityGuard.IsOffline || isServer;

    public override void Initialize(float duration)
    {
        base.Initialize(duration);

        if (!HasAuthority) return;

        StartCoroutine(MissileSequence());
    }

    private IEnumerator MissileSequence()
    {
        for (int i = 0; i < missileCount; i++)
        {
            SpawnSingleMissile();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnSingleMissile()
    {
        // 1) 랜덤 xz 위치 결정
        float x = Random.Range(Mathf.Min(cornerA.x, cornerB.x), Mathf.Max(cornerA.x, cornerB.x));
        float z = Random.Range(Mathf.Min(cornerA.y, cornerB.y), Mathf.Max(cornerA.y, cornerB.y));

        // 2) 위에서 아래로 Raycast → y 좌표 결정
        Vector3 rayOrigin = new Vector3(x, raycastHeight, z);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayerMask))
        {
            Debug.LogWarning($"[Missile] Raycast 실패: ({x}, {z}) 위치에 바닥 없음. 스킵.");
            return;
        }

        Vector3 impactPoint = hit.point;

        // 3) 경고 → 폭발 시퀀스 시작
        StartCoroutine(MissileStrike(impactPoint));
    }

    private IEnumerator MissileStrike(Vector3 position)
    {
        // 데칼 생성
        GameObject decal = null;
        if (warningDecalPrefab != null)
        {
            Vector3 decalPos = position + Vector3.up * 0.5f;
            decal = Instantiate(warningDecalPrefab, decalPos, Quaternion.identity);

            SpawnNetworkObject(decal);
            _activeDecals.Add(decal);
        }

        // 경고 후 일정시간 대기
        yield return new WaitForSeconds(warningDuration);

        // 폭발 피해 반경 처리
        Collider[] hits = Physics.OverlapSphere(position, explosionRadius);
        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            ICharacterModel player = col.GetComponentInParent<ICharacterModel>();
            if (player != null)
            {
                player.RequestTakeDamage(damage);
            }
        }

        // 데칼 제거
        if (decal != null)
        {
            _activeDecals.Remove(decal);
            DestroyNetworkObject(decal);
        }

        // 폭발 이펙트 오브젝트
        if (explosionEffectPrefab != null)
        {
            GameObject explosion = Instantiate(explosionEffectPrefab, position, Quaternion.identity);

            SpawnNetworkObject(explosion);
            _activeExplosions.Add(explosion);

            // 일정 시간 후 폭발 이펙트 제거
            StartCoroutine(CleanupExplosion(explosion, explosionLifetime));
        }
    }

    private IEnumerator CleanupExplosion(GameObject explosion, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (explosion != null)
        {
            _activeExplosions.Remove(explosion);
            DestroyNetworkObject(explosion);
        }
    }

    private void OnDestroy()
    {
        if (!HasAuthority) return;

        // 장판이 조기 파괴될 경우 남아있는 데칼/이펙트 정리
        foreach (var decal in _activeDecals)
        {
            if (decal == null) continue;
            DestroyNetworkObject(decal);
        }
        _activeDecals.Clear();

        foreach (var explosion in _activeExplosions)
        {
            if (explosion == null) continue;
            DestroyNetworkObject(explosion);
        }
        _activeExplosions.Clear();
    }

    //스폰의 온/오프라인 처리를 함수로 별도 처리
    private static void SpawnNetworkObject(GameObject obj)
    {
        if (obj == null) return;

        if (AuthorityGuard.IsOffline)
        {
            HardenOfflineObject(obj);
            return;
        }

        if (obj.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
        {
            NetworkServer.Spawn(obj);
        }
    }

    //디스폰의 온/오프라인 처리를 함수로 별도 처리
    private static void DestroyNetworkObject(GameObject obj)
    {
        if (obj == null) return;

        if (AuthorityGuard.IsOffline)
        {
            Destroy(obj);
            return;
        }

        if (obj.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
        {
            NetworkServer.Destroy(obj);
        }
        else
        {
            Destroy(obj);
        }
    }

    private static void HardenOfflineObject(GameObject obj)
    {
        if (obj == null) return;
        if (!AuthorityGuard.IsOffline) return;

        if (obj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;

        if (!obj.activeSelf) obj.SetActive(true);
    }
}