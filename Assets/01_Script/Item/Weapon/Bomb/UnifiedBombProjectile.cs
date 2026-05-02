using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 폭탄 투사체.
///
/// Rigidbody의 물리 시뮬레이션을 쓰지 않고, 매 프레임 코드가 직접 transform.position을 갱신.
/// 발사 정보(initialVelocity, gravity)만 SyncVar로 동기화하여 모든 클라가 같은 수식으로 계산하므로 위치 동기화 불필요.
/// 충돌은 Raycast로 서버 측에서 판정.
/// 충돌 발생 시 SyncVar(hasLanded, landedPos)로 전파 → 모든 클라가 그 자리에서 정지.
///
/// 오프라인: 본인이 발사/충돌/폭발 모두 처리. SyncVar 대신 _local 필드로 동작.
/// 온라인: 서버가 충돌 판정/폭발 데미지 처리. 클라는 SyncVar 받아 위치만 자체 계산.
///
/// </summary>
public class UnifiedBombProjectile : NetworkBehaviour
{
    // 발사 정보 (SyncVar) — 모든 클라가 이 값으로 같은 궤적을 계산
    [SyncVar] private Vector3 syncVelocity;
    [SyncVar] private Vector3 syncGravity = new Vector3(0f, -9.81f, 0f);
    [SyncVar] private bool isLaunched;
    [SyncVar] private bool hasLanded;
    [SyncVar] private Vector3 landedPos;
    [SyncVar] private float launchTime;     // NetworkTime.time 기준 발사 시각

    // 오프라인 폴백
    private Vector3 _localVelocity;
    private Vector3 _localGravity = new Vector3(0f, -9.81f, 0f);
    private bool _localLaunched;
    private bool _localLanded;
    private Vector3 _localLandedPos;
    private float _localLaunchTime;

    // 폭발 파라미터
    private float _fuseDuration = 1.5f;
    private float _radius = 4f;
    private float _damage = 50f;
    private GameObject _explosionPrefab;
    private float _explosionLifetime = 2f;
    private GameObject _ownerObj;

    // 런타임 상태
    private Vector3 _currentVelocity;   // 매 프레임 누적되는 현재 속도, 각 클라이언트 로컬에서 사용
    private bool _exploded;

    // 충돌 판정용 레이어 - 인스펙터에서 설정.
    [SerializeField] private LayerMask collisionMask = ~0; 

    private bool HasAuthority => AuthorityGuard.IsOffline || isServer;
    private bool IsLaunchedNow() => AuthorityGuard.IsOffline ? _localLaunched : isLaunched;
    private bool IsLandedNow() => AuthorityGuard.IsOffline ? _localLanded : hasLanded;
    private Vector3 GetVelocity() => AuthorityGuard.IsOffline ? _localVelocity : syncVelocity;
    private Vector3 GetGravity() => AuthorityGuard.IsOffline ? _localGravity : syncGravity;
    private float GetLaunchTime() => AuthorityGuard.IsOffline ? _localLaunchTime : launchTime;
    private Vector3 GetLandedPos() => AuthorityGuard.IsOffline ? _localLandedPos : landedPos;

    // 외부 호출 — UnifiedWeaponBomb이 Spawn 후 호출
    /// 발사 정보 + 폭발 파라미터 주입. 서버 또는 오프라인에서만 호출.
    public void Configure(
        Vector3 initialVelocity,
        float fuseDuration,
        float radius,
        float damage,
        GameObject explosionPrefab,
        float explosionLifetime,
        GameObject ownerObj)
    {
        _fuseDuration = Mathf.Max(0f, fuseDuration);
        _radius = radius;
        _damage = damage;
        _explosionPrefab = explosionPrefab;
        _explosionLifetime = explosionLifetime;
        _ownerObj = ownerObj;

        Vector3 g = Physics.gravity;
        float now = (float)(AuthorityGuard.IsOffline ? Time.timeAsDouble : NetworkTime.time);

        // 로컬 + SyncVar 양쪽 세팅
        _localVelocity = initialVelocity;
        _localGravity = g;
        _localLaunched = true;
        _localLandedPos = Vector3.zero;
        _localLanded = false;
        _localLaunchTime = now;

        if (!AuthorityGuard.IsOffline)
        {
            syncVelocity = initialVelocity;
            syncGravity = g;
            isLaunched = true;
            hasLanded = false;
            landedPos = Vector3.zero;
            launchTime = now;
        }

        _currentVelocity = initialVelocity;

        // 자기를 던진 캐릭터와의 충돌 무시 — Raycast가 자기 몸을 지면으로 오인하지 않도록
        IgnoreCollisionWithOwner();
    }

    private void IgnoreCollisionWithOwner()
    {
        if (_ownerObj == null) return;

        Collider myCol = GetComponent<Collider>();
        if (myCol == null) return;

        Collider[] ownerCols = _ownerObj.GetComponentsInChildren<Collider>();
        foreach (var oc in ownerCols)
        {
            if (oc != null) Physics.IgnoreCollision(myCol, oc, true);
        }
    }

    // SyncVar 훅 — 클라이언트가 늦게 들어왔을 때를 대비한 초기 보정
    public override void OnStartClient()
    {
        base.OnStartClient();

        // 클라가 늦게 합류해서 이미 launch된 폭탄을 받은 경우, 발사 시점부터 경과한 시간만큼 위치를 미리 진행시켜 놓는다. 이후엔 Update에서 같은 수식으로 계속 계산.
        if (isLaunched && !hasLanded)
        {
            float t = (float)NetworkTime.time - launchTime;
            if (t > 0f)
            {
                Vector3 pos = transform.position
                            + syncVelocity * t
                            + 0.5f * syncGravity * t * t;
                transform.position = pos;
                _currentVelocity = syncVelocity + syncGravity * t;
            }
        }
        else if (hasLanded)
        {
            transform.position = landedPos;
        }
    }

    // 위치 갱신 + 권위 측 충돌 판정
    private void Update()
    {
        if (!IsLaunchedNow()) return;

        if (IsLandedNow())
        {
            // 안전하게 정지 위치로 고정 (모든 클라가 같은 위치)
            transform.position = GetLandedPos();
            return;
        }

        // 모든 클라가 자체 시뮬 : 속도 적분 후 위치 적분
        Vector3 prevPos = transform.position;
        Vector3 g = GetGravity();
        _currentVelocity += g * Time.deltaTime;
        Vector3 nextPos = prevPos + _currentVelocity * Time.deltaTime;

        // 권위 측만 충돌 판정하여 SyncVar로 전파
        if (HasAuthority)
        {
            Vector3 segment = nextPos - prevPos;
            float dist = segment.magnitude;
            if (dist > 0.0001f
                && Physics.Raycast(prevPos, segment.normalized, out RaycastHit hit, dist, collisionMask))
            {
                // 자기 자신 콜라이더는 무시
                if (hit.collider != null && hit.collider.transform.root != transform.root)
                {
                    Land(hit.point);
                    return;
                }
            }
        }

        transform.position = nextPos;
    }

    // 착탄 처리 — 서버 측에서만 호출
    private void Land(Vector3 pos)
    {
        // 로컬 + SyncVar 세팅
        _localLanded = true;
        _localLandedPos = pos;
        _localVelocity = Vector3.zero;

        if (!AuthorityGuard.IsOffline)
        {
            hasLanded = true;
            landedPos = pos;
            syncVelocity = Vector3.zero;
        }

        transform.position = pos;
        _currentVelocity = Vector3.zero;

        StartCoroutine(FuseAndExplode());
    }

    private IEnumerator FuseAndExplode()
    {
        if (_fuseDuration > 0f)
            yield return new WaitForSeconds(_fuseDuration);

        Explode();
    }

    // 폭발 이펙트 스폰 + 반경 내 플레이어 데미지.
    private void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        Vector3 pos = transform.position;

        // 데미지 판정
        Collider[] hits = Physics.OverlapSphere(pos, _radius);
        foreach (Collider col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            ICharacterModel player = col.GetComponentInParent<ICharacterModel>();
            if (player != null)
            {
                player.RequestTakeDamage(_damage);
            }
        }

        // 폭발 이펙트 스폰
        if (_explosionPrefab != null)
        {
            GameObject explosion = Instantiate(_explosionPrefab, pos, Quaternion.identity);
            SpawnNetworkObject(explosion);
            StartCoroutine(CleanupExplosion(explosion, _explosionLifetime));
        }

        DestroySelf();
    }

    private IEnumerator CleanupExplosion(GameObject explosion, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (explosion != null)
        {
            DestroyNetworkObject(explosion);
        }
    }

    private void DestroySelf()
    {
        if (AuthorityGuard.IsOffline)
        {
            Destroy(gameObject);
        }
        else if (NetworkServer.active)
        {
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Spawn/Destroy
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}