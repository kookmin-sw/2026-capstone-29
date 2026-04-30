using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 운석 효과.
/// duration 동안 순차적으로 n개의 운석을 떨어뜨린다.
/// 각 운석: 랜덤 xz 위치 → Raycast로 y 결정 → 데칼(경고 원) 스폰 → m초 후 폭발 이펙트 + 데미지 판정.
/// 서버가 판정을 담당하고, 데칼/이펙트는 NetworkServer.Spawn으로 모든 클라에 동기화.
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
    [Tooltip("직사각형의 첫 번째 꼭짓점 (x, z)")]
    [SerializeField] private Vector2 cornerA = new Vector2(-10f, -10f);

    [Tooltip("직사각형의 두 번째 꼭짓점 (x, z)")]
    [SerializeField] private Vector2 cornerB = new Vector2(10f, 10f);

    [Header("Raycast 설정")]
    [Tooltip("위에서 아래로 Raycast할 시작 높이")]
    [SerializeField] private float raycastHeight = 100f;

    [Tooltip("Raycast 최대 거리")]
    [SerializeField] private float raycastDistance = 200f;

    [Tooltip("바닥/지형 레이어")]
    [SerializeField] private LayerMask groundLayerMask;

    [Header("프리팹")]
    [Tooltip("경고 원 데칼 프로젝터 프리팹 (NetworkIdentity 필수)")]
    [SerializeField] private GameObject warningDecalPrefab;

    [Tooltip("폭발 이펙트 프리팹 (NetworkIdentity 필수)")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Tooltip("폭발 이펙트 자동 제거 시간(초)")]
    [SerializeField] private float explosionLifetime = 2f;

    // 활성 데칼 추적 (OnDestroy 시 정리용)
    private readonly List<GameObject> _activeDecals = new List<GameObject>();
    private readonly List<GameObject> _activeExplosions = new List<GameObject>();

    public override void Initialize(float duration)
    {
        base.Initialize(duration);

        if (!isServer) return;

        StartCoroutine(MissileSequence());
    }

    [Server]
    private IEnumerator MissileSequence()
    {
        for (int i = 0; i < missileCount; i++)
        {
            SpawnSingleMissile();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    [Server]
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

        // 3) 경고 데칼 스폰
        StartCoroutine(MissileStrike(impactPoint));
    }

    [Server]
    private IEnumerator MissileStrike(Vector3 position)
    {
        // --- 경고 데칼 생성 ---
        GameObject decal = null;
        if (warningDecalPrefab != null)
        {
            // 데칼 프로젝터는 위에서 아래로 투사하므로 약간 위에 배치
            Vector3 decalPos = position + Vector3.up * 0.5f;
            decal = Instantiate(warningDecalPrefab, decalPos, Quaternion.identity);

            // 데칼 크기를 explosionRadius에 맞춰 스케일 조정
            // DecalProjector의 크기는 프리팹 기본값 기준이므로,
            // 프리팹 제작 시 1m 반경 기준으로 만들면 여기서 스케일로 맞출 수 있다.
            // 또는 DecalProjector 컴포넌트에 직접 접근해서 size를 설정할 수도 있다.

            if (decal.GetComponent<NetworkIdentity>() != null)
                NetworkServer.Spawn(decal);

            _activeDecals.Add(decal);
        }

        // --- 경고 시간 대기 ---
        yield return new WaitForSeconds(warningDuration);

        // --- 폭발 데미지 판정 (서버) ---
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

        // --- 경고 데칼 제거 ---
        if (decal != null)
        {
            _activeDecals.Remove(decal);
            if (decal.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
                NetworkServer.Destroy(decal);
            else
                Destroy(decal);
        }

        // --- 폭발 이펙트 스폰 ---
        if (explosionEffectPrefab != null)
        {
            GameObject explosion = Instantiate(explosionEffectPrefab, position, Quaternion.identity);

            if (explosion.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
                NetworkServer.Spawn(explosion);

            _activeExplosions.Add(explosion);

            // 일정 시간 후 폭발 이펙트 제거
            StartCoroutine(CleanupExplosion(explosion, explosionLifetime));
        }
    }

    [Server]
    private IEnumerator CleanupExplosion(GameObject explosion, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (explosion != null)
        {
            _activeExplosions.Remove(explosion);
            if (explosion.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
                NetworkServer.Destroy(explosion);
            else
                Destroy(explosion);
        }
    }

    private void OnDestroy()
    {
        if (!isServer) return;

        // 장판이 조기 파괴될 경우 남아있는 데칼/이펙트 정리
        foreach (var decal in _activeDecals)
        {
            if (decal == null) continue;
            if (decal.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
                NetworkServer.Destroy(decal);
            else
                Destroy(decal);
        }
        _activeDecals.Clear();

        foreach (var explosion in _activeExplosions)
        {
            if (explosion == null) continue;
            if (explosion.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
                NetworkServer.Destroy(explosion);
            else
                Destroy(explosion);
        }
        _activeExplosions.Clear();
    }
}