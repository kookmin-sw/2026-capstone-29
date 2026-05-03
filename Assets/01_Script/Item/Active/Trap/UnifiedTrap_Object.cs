using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
/// 덫 본체.
///     트리거 반경 안에 들어온 플레이어를 holdDuration 동안 그 자리에 묶는다.
///     발동 시 플레이어를 덫 중심(x,z)으로 끌어당김. y는 플레이어 본인 값 유지.
///     발동 시 Animator의 trigger를 발동시켜 애니메이션 재생.
///
/// 네트워크 흐름:
///     충돌 판정: 서버 (또는 오프라인 본인)
///     발동 신호 동기화: _triggered SyncVar의 hook → 모든 클라이언트가 자기 Animator의 trigger 발동
///     묶임 효과: TargetRpc → 본인 클라이언트만 자기 캐릭터 봉쇄
///     파괴: 권한자가 holdDuration 후 NetworkServer.Destroy
/// </summary>
public class UnifiedTrap_Object : NetworkBehaviour
{
    [Header("Trap Behavior")]
    [SyncVar] public float lifetime = 30f;
    [SyncVar] public float triggerRadius = 1.2f;
    [SyncVar] public float holdDuration = 3f;
    [SyncVar] public bool consumeOnTrigger = true;

    [Header("Visual")]
    [SyncVar] public Color trapColor = new Color(0.8f, 0.2f, 0.2f, 0.6f);

    [Header("Animation")]
    [Tooltip("발동 시 Animator에 SetTrigger로 호출할 파라미터 이름")]
    [SerializeField] private string triggerParameterName = "Trapped";
    [Tooltip("Animator 컴포넌트 (비워두면 자식에서 자동 검색)")]
    [SerializeField] private Animator animator;

    [SyncVar] private uint ownerNetId;
    private GameObject ownerCache;

    // 발동 신호. 서버가 true로 바꾸면 모든 클라이언트의 hook에서 애니메이션 재생.
    [SyncVar(hook = nameof(OnTriggeredChanged))]
    private bool _triggered;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material trapMaterial;

    private float elapsedTime = 0f;
    private bool _initialized;
    private bool _consumed;
    private bool _destroyScheduled;
    private bool _animationPlayed; // 트리거 중복 발동 가드

    // 초기화 진입점
    private void Awake()
    {
        ResolveAnimator();

        if (AuthorityGuard.IsOffline)
        {
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveAnimator();

        // SyncVar 초기값은 hook이 호출되지 않을 수 있으므로 여기서 한 번 더 체크.
        if (_triggered && !_animationPlayed)
        {
            PlayTriggerAnimation();
        }

    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveAnimator();
    }

    private void ResolveAnimator()
    {
        if (animator != null) return;
        animator = GetComponentInChildren<Animator>();
    }

    public void SetOwner(GameObject owner)
    {
        if (owner == null) return;

        ownerCache = owner;

        if (!AuthorityGuard.IsOffline)
        {
            NetworkIdentity nid = owner.GetComponent<NetworkIdentity>();
            if (nid != null)
            {
                ownerNetId = nid.netId;
                Debug.Log($"[UnifiedTrap] SetOwner: owner.name={owner.name}, ownerNetId={ownerNetId}");
            }
            else
            {
                Debug.LogWarning($"[UnifiedTrap] SetOwner: {owner.name}에 NetworkIdentity가 없음");
            }
        }
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        if (elapsedTime >= lifetime)
        {
            DestroyTrap();
            return;
        }

        if (_consumed) return;

        bool hasAuthority = AuthorityGuard.IsOffline || isServer;
        if (!hasAuthority) return;

        DetectAndTriggerPlayer();
    }

    private void DetectAndTriggerPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, triggerRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject playerRoot = FindPlayerRoot(hits[i].gameObject);
            if (playerRoot == null) continue;

            NetworkIdentity hitNid = playerRoot.GetComponent<NetworkIdentity>();
            uint hitNetId = hitNid != null ? hitNid.netId : 0;
            Debug.Log($"[UnifiedTrap] 덫 위에 플레이어 발견: name={playerRoot.name}, netId={hitNetId}, ownerNetId={ownerNetId}, IsOwner={IsOwner(playerRoot)}");

            if (IsOwner(playerRoot)) continue;

            TriggerTrapOn(playerRoot);
            FireTriggerAnimation(); 

            if (consumeOnTrigger)
            {
                _consumed = true;
                ScheduleDestroyAfter(holdDuration);
                return;
            }
        }
    }

    // 모든 클라이언트에 애니메이션 신호 전파
    private void FireTriggerAnimation()
    {
        if (_animationPlayed) return;

        if (AuthorityGuard.IsOffline)
        {
            PlayTriggerAnimation();
        }
        else
        {
            // 서버에서만 SyncVar 변경 가능. SyncVar 변경 → hook이 모든 클라이언트에서 호출됨.
            // (호스트는 서버이자 클라이언트이므로 hook도 자기에게서 호출됨)
            _triggered = true;
        }
    }

    // SyncVar hook. 모든 클라이언트에서 호출됨.
    private void OnTriggeredChanged(bool oldValue, bool newValue)
    {
        if (!newValue) return;
        PlayTriggerAnimation();
    }

    // Animator의 trigger 파라미터를 발동시켜 애니메이션 재생.
    private void PlayTriggerAnimation()
    {
        if (_animationPlayed) return;
        _animationPlayed = true;

        if (animator == null) ResolveAnimator();

        if (animator == null)
        {
            Debug.LogWarning("[UnifiedTrap] Animator를 찾지 못함. 애니메이션 재생 스킵.");
            return;
        }

        if (string.IsNullOrEmpty(triggerParameterName))
        {
            Debug.LogWarning("[UnifiedTrap] triggerParameterName이 비어있음.");
            return;
        }

        animator.SetTrigger(triggerParameterName);
        Debug.Log($"[UnifiedTrap] 애니메이션 트리거 발동: {triggerParameterName}");
    }

    private void ScheduleDestroyAfter(float delay)
    {
        if (_destroyScheduled) return;

        bool hasAuthority = AuthorityGuard.IsOffline || isServer;
        if (!hasAuthority) return;

        _destroyScheduled = true;
        StartCoroutine(DestroyAfterRoutine(delay));
    }

    private IEnumerator DestroyAfterRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (this == null || gameObject == null) yield break;

        DestroyTrap();
    }

    private GameObject FindPlayerRoot(GameObject hit)
    {
        if (hit == null) return null;

        Transform t = hit.transform;
        while (t != null)
        {
            if (t.GetComponent<UnifiedItemManager>() != null) return t.gameObject;
            if (t.GetComponent<ItemManager>() != null) return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    private bool IsOwner(GameObject target)
    {
        if (target == null) return false;

        if (AuthorityGuard.IsOffline)
        {
            return ownerCache != null && target == ownerCache;
        }

        NetworkIdentity nid = target.GetComponent<NetworkIdentity>();
        if (nid == null) return false;
        return nid.netId == ownerNetId;
    }

    private void TriggerTrapOn(GameObject target)
    {
        Vector3 trapPos = transform.position;

        Debug.Log($"[UnifiedTrap] 플레이어 {target.name} 가 덫에 걸림. {holdDuration}초 동안 묶임. anchor={trapPos}");

        if (AuthorityGuard.IsOffline)
        {
            ApplyHoldDirectly(target, holdDuration, trapPos);
            return;
        }

        NetworkIdentity targetNid = target.GetComponent<NetworkIdentity>();
        if (targetNid == null)
        {
            Debug.LogWarning("[UnifiedTrap] 대상에 NetworkIdentity가 없습니다.");
            return;
        }

        bool isHostsOwnCharacter =
            NetworkServer.localConnection != null
            && targetNid.connectionToClient == NetworkServer.localConnection;

        if (isHostsOwnCharacter)
        {
            Debug.Log($"[UnifiedTrap] 대상 {target.name}는 호스트 본인 → 서버 로컬에서 직접 적용");
            ApplyHoldDirectly(target, holdDuration, trapPos);
            return;
        }

        if (targetNid.connectionToClient == null)
        {
            Debug.Log($"[UnifiedTrap] 대상 {target.name}는 connectionToClient가 없음 → 서버 로컬에서 직접 적용");
            ApplyHoldDirectly(target, holdDuration, trapPos);
            return;
        }

        Debug.Log($"[UnifiedTrap] TargetRpc 송신 → conn={targetNid.connectionToClient.connectionId}, netId={targetNid.netId}");
        TargetApplyHold(targetNid.connectionToClient, targetNid.netId, holdDuration, trapPos);
    }

    [TargetRpc]
    private void TargetApplyHold(NetworkConnectionToClient conn, uint targetNetId, float duration, Vector3 anchor)
    {
        Debug.Log($"[UnifiedTrap] TargetRpc 수신: targetNetId={targetNetId}, anchor={anchor}");

        if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity targetNid))
        {
            Debug.LogWarning($"[UnifiedTrap] netId={targetNetId} 를 NetworkClient.spawned에서 찾지 못함");
            return;
        }

        if (targetNid == null)
        {
            Debug.LogWarning("[UnifiedTrap] targetNid가 null");
            return;
        }

        Debug.Log($"[UnifiedTrap] 대상 검증: name={targetNid.name}, isLocalPlayer={targetNid.isLocalPlayer}, isOwned={targetNid.isOwned}");

        ApplyHoldDirectly(targetNid.gameObject, duration, anchor);
    }

    private static void ApplyHoldDirectly(GameObject target, float duration, Vector3 anchor)
    {
        if (target == null) return;

        TrapHold existing = target.GetComponent<TrapHold>();
        if (existing != null)
        {
            existing.BeginAtAnchor(duration, anchor);
        }
        else
        {
            TrapHold effect = target.AddComponent<TrapHold>();
            effect.BeginAtAnchor(duration, anchor);
        }
    }

    private void DestroyTrap()
    {
        bool hasAuthority = AuthorityGuard.IsOffline || isServer;
        if (!hasAuthority) return;

        if (AuthorityGuard.IsOffline) Destroy(gameObject);
        else NetworkServer.Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (trapMaterial != null)
            Destroy(trapMaterial);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(trapColor.r, trapColor.g, trapColor.b, 0.4f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
#endif
}