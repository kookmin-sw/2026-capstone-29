using Mirror;
using StarterAssets;
using System.Collections;
using UnityEngine;

/// <summary>
/// 근접 무기 등 아이템이 활성화된 동안 소유자의 애니메이션 속도를 조절.
/// - 온라인: 기존 <see cref="AttackSpeed"/>와 동일. 서버에서 적용 → RPC 전파.
/// - 오프라인: 서버/RPC 없이 본인만 바로 적용/해제.
///
/// duration 을 안전장치로 두어 외부 호출이 누락돼도 자동 해제.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class UnifiedAttackSpeed : NetworkBehaviour
{
    [Tooltip("애니메이션 속도 배율 (Animator.speed 에 곱)")]
    [SerializeField] private float animSpeedMultiplier = 1.2f;

    [Tooltip("버프 최대 지속시간 (초). Remove() 가 누락돼도 이 시간 이후 자동 해제.")]
    [SerializeField] public float duration = 10f;

    private GameObject _buffedOwner;
    private UnifiedThirdPersonController _controller;
    private Animator _animator;

    // 복원용 원본값 저장 변수
    private float _originalAnimSpeed = 1f;

    private bool _applied;
    private Coroutine _durationRoutine;

    /// <summary>오프라인/온라인 공용 진입점.</summary>
    public void ApplyTo(GameObject owner)
    {
        if (_applied) return;
        if (owner == null) return;

        if (AuthorityGuard.IsOffline)
        {
            ApplyToLocal(owner);
            return;
        }

        // 온라인: 서버에서만 권위 동작
        if (!NetworkServer.active) return;

        ApplyToServer(owner);
    }

    /// <summary>오프라인/온라인 공용 해제.</summary>
    public void Remove()
    {
        if (!_applied) return;

        if (AuthorityGuard.IsOffline)
        {
            RemoveLocalScope();
            return;
        }

        if (!NetworkServer.active) return;

        RemoveServer();
    }

    // -----------------------------
    // 온라인 (서버 권위)
    // -----------------------------
    [Server]
    private void ApplyToServer(GameObject owner)
    {
        _applied = true;
        _buffedOwner = owner;

        NetworkIdentity ownerIdentity = owner.GetComponent<NetworkIdentity>();

        // 서버에서 즉시 로컬 적용
        ApplyLocally(owner);

        // 원격 클라이언트에 전파
        if (ownerIdentity != null)
        {
            RpcApply(ownerIdentity);
        }

        if (_durationRoutine != null) StopCoroutine(_durationRoutine);
        _durationRoutine = StartCoroutine(DurationSafeguard());
    }

    [Server]
    private void RemoveServer()
    {
        _applied = false;

        if (_durationRoutine != null)
        {
            StopCoroutine(_durationRoutine);
            _durationRoutine = null;
        }

        NetworkIdentity ownerIdentity = _buffedOwner != null
            ? _buffedOwner.GetComponent<NetworkIdentity>()
            : null;

        // 서버에서도 로컬 복원 — RPC 전달 전에 오브젝트가 파괴되는 경우에도 안전하게 원상복구
        if (_buffedOwner != null)
        {
            RemoveLocally(_buffedOwner);
        }

        // 원격 클라이언트에는 RPC 로 전파
        if (ownerIdentity != null)
        {
            RpcRemove(ownerIdentity);
        }

        _buffedOwner = null;
    }

    // -----------------------------
    // 오프라인 경로
    // -----------------------------
    private void ApplyToLocal(GameObject owner)
    {
        _applied = true;
        _buffedOwner = owner;

        ApplyLocally(owner);

        if (_durationRoutine != null) StopCoroutine(_durationRoutine);
        _durationRoutine = StartCoroutine(DurationSafeguard());
    }

    private void RemoveLocalScope()
    {
        _applied = false;

        if (_durationRoutine != null)
        {
            StopCoroutine(_durationRoutine);
            _durationRoutine = null;
        }

        if (_buffedOwner != null)
        {
            RemoveLocally(_buffedOwner);
        }
        _buffedOwner = null;
    }

    // -----------------------------
    // 안전장치 (온라인은 서버에서, 오프라인은 본인이 실행)
    // -----------------------------
    private IEnumerator DurationSafeguard()
    {
        yield return new WaitForSeconds(duration);

        if (_applied)
        {
            Debug.Log("[UnifiedAttackSpeed] duration 만료로 자동 해제");
            Remove();
        }
    }

    // -----------------------------
    // RPC (온라인 전용)
    // -----------------------------
    [ClientRpc]
    private void RpcApply(NetworkIdentity ownerIdentity)
    {
        if (isServer) return;
        if (ownerIdentity == null) return;
        ApplyLocally(ownerIdentity.gameObject);
    }

    [ClientRpc]
    private void RpcRemove(NetworkIdentity ownerIdentity)
    {
        if (isServer) return;
        if (ownerIdentity == null) return;
        RemoveLocally(ownerIdentity.gameObject);
    }

    // -----------------------------
    // 실제 값 적용/복원 (온/오프라인 공용)
    // -----------------------------
    private void ApplyLocally(GameObject owner)
    {
        if (owner == null) return;

        _buffedOwner = owner;
        _controller = owner.GetComponent<UnifiedThirdPersonController>();
        _animator = owner.GetComponentInChildren<Animator>();

        // 애니메이션 속도: 원본 캐시 후 곱
        if (_animator != null && animSpeedMultiplier > 0f)
        {
            _originalAnimSpeed = _animator.speed;
            _animator.speed = _originalAnimSpeed * animSpeedMultiplier;
        }
    }

    private void RemoveLocally(GameObject owner)
    {
        // 애니메이션 속도 원복
        if (_animator != null)
        {
            _animator.speed = _originalAnimSpeed;
        }

        _controller = null;
        _animator = null;
    }

    // 오브젝트가 사라졌을 때도 버프 해제 보장
    private void OnDestroy()
    {
        if (_applied && _buffedOwner != null)
        {
            // _animator null 가능성 있어 RemoveLocally에 위임
            RemoveLocally(_buffedOwner);
        }
    }
}