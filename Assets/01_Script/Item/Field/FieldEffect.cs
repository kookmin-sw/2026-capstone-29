using Mirror;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(NetworkIdentity))]
public abstract class FieldEffect : NetworkBehaviour
{
    protected float lifetime;
    private Coroutine _lifetimeCoroutine;

    private bool HasAuthority => AuthorityGuard.IsOffline || isServer;

    public virtual void Initialize(float duration)
    {
        lifetime = duration;

        if (!HasAuthority) return;

        // 기존 코루틴이 있으면 정리 후 재시작
        if (_lifetimeCoroutine != null)
            StopCoroutine(_lifetimeCoroutine);

        _lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        DestroyFieldObj();
    }

    private void OnTriggerEnter(Collider other)
    {
        ICharacterModel player = ResolvePlayer(other);
        if (player == null) return;
        if (HasAuthority) OnPlayerEnter(player);
        if (IsLocalPlayer(other)) OnLocalPlayerEnter(player);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!HasAuthority) return;
        ICharacterModel player = ResolvePlayer(other);
        if (player != null) OnPlayerStay(player);
    }

    private void OnTriggerExit(Collider other)
    {
        ICharacterModel player = ResolvePlayer(other);
        if (player == null) return;
        if (HasAuthority) OnPlayerExit(player);
        if (IsLocalPlayer(other)) OnLocalPlayerExit(player);
    }

    private ICharacterModel ResolvePlayer(Collider other)
    {
        if (!other.CompareTag("Player")) return null;
        return other.GetComponentInParent<ICharacterModel>();
    }

    private bool IsLocalPlayer(Collider other)
    {
        if (AuthorityGuard.IsOffline) return true;
        NetworkIdentity ni = other.GetComponentInParent<NetworkIdentity>();
        return ni != null && ni.isLocalPlayer;
    }

    protected virtual void OnPlayerEnter(ICharacterModel player) { }
    protected virtual void OnPlayerStay(ICharacterModel player) { }
    protected virtual void OnPlayerExit(ICharacterModel player) { }
    protected virtual void OnLocalPlayerEnter(ICharacterModel player) { }
    protected virtual void OnLocalPlayerExit(ICharacterModel player) { }

    private void DestroyFieldObj()
    {
        Debug.Log("오브젝트 파괴!");
        if (AuthorityGuard.IsOffline)
        {
            Destroy(gameObject);
        }
        else if (gameObject.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
        {
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}