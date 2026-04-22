using System.Collections;
using Mirror;
using StarterAssets;
using UnityEngine;


// 근접 무기 등 아이템이 활성화된 동안 소유자의 애니메이션 속도를 조절. WeaponMelee에서 ApplyTo / Remove 를 호출.
// duration 을 안전장치로 두어 외부 호출이 누락돼도 자동 해제.
[RequireComponent(typeof(NetworkIdentity))]
public class AttackSpeed : NetworkBehaviour
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


    [Server]
    public void ApplyTo(GameObject owner)
    {
        if (_applied) return;
        if (owner == null) return;

        _applied = true;
        _buffedOwner = owner;

        NetworkIdentity ownerIdentity = owner.GetComponent<NetworkIdentity>();

        // 서버에서 즉시 로컬 적용
        ApplyLocally(owner);

        // 원격 클라이언트에 전파
        if (ownerIdentity != null && NetworkServer.active)
        {
            RpcApply(ownerIdentity);
        }

        if (_durationRoutine != null) StopCoroutine(_durationRoutine);
        _durationRoutine = StartCoroutine(DurationSafeguard());
    }


    // 출력속도 정상화
    [Server]
    public void Remove()
    {
        if (!_applied) return;
        _applied = false;

        if (_durationRoutine != null)
        {
            StopCoroutine(_durationRoutine);
            _durationRoutine = null;
        }

        NetworkIdentity ownerIdentity = _buffedOwner != null
            ? _buffedOwner.GetComponent<NetworkIdentity>()
            : null;

        // 서버에서도 로컬 복원 실시로 RPC 전달 전에 오브젝트가 파괴되는 경우에도 안전하게 원상복구.
        if (_buffedOwner != null)
        {
            RemoveLocally(_buffedOwner);
        }

        // 2) 원격 클라이언트에는 RPC 로 전파
        if (ownerIdentity != null && NetworkServer.active)
        {
            RpcRemove(ownerIdentity);
        }

        _buffedOwner = null;
    }

    private IEnumerator DurationSafeguard()
    {
        yield return new WaitForSeconds(duration);

        if (_applied)
        {
            Debug.Log("[WeaponSpeedBuff] duration 만료로 자동 해제");
            Remove();
        }
    }

    // 모든 클라이언트에서 실제 속도 값 변경
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

    // 로컬 적용 / 해제
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
            _animator.speed = _originalAnimSpeed;
            RemoveLocally(_buffedOwner);
        }
    }
}