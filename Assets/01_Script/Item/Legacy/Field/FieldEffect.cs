using Mirror;
using UnityEngine;

// 두 종류의 콜백을 제공:
// 서버 전용 (OnPlayerEnter/Stay/Exit): 데미지, 상태변경 등 게임 상태에 영향
// 로컬 클라 전용 (OnLocalPlayerEnter/Exit): 본인 카메라/UI/사운드 등 시각, 연출
[RequireComponent(typeof(NetworkIdentity))]
public abstract class FieldEffect : NetworkBehaviour
{
    protected float lifetime;

    public virtual void Initialize(float duration)
    {
        lifetime = duration;
    }


    private void OnTriggerEnter(Collider other)
    {
        ICharacterModel player = ResolvePlayer(other);
        if (player == null) return;

        // 서버 : 게임 상태 변경
        if (isServer) OnPlayerEnter(player);

        // 로컬 클라 : 시각/연출 (각자 로컬에서만 적용)
        if (IsLocalPlayer(other)) OnLocalPlayerEnter(player);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isServer) return;  // Stay는 데미지 틱 등 서버 로직 전용
        ICharacterModel player = ResolvePlayer(other);
        if (player != null) OnPlayerStay(player);
    }

    private void OnTriggerExit(Collider other)
    {
        ICharacterModel player = ResolvePlayer(other);
        if (player == null) return;

        if (isServer) OnPlayerExit(player);
        if (IsLocalPlayer(other)) OnLocalPlayerExit(player);
    }

    private ICharacterModel ResolvePlayer(Collider other)
    {
        if (!other.CompareTag("Player")) return null;
        return other.GetComponentInParent<ICharacterModel>();
    }

    private bool IsLocalPlayer(Collider other)
    {
        NetworkIdentity ni = other.GetComponentInParent<NetworkIdentity>();
        return ni != null && ni.isLocalPlayer;
    }


    protected virtual void OnPlayerEnter(ICharacterModel player) { }
    protected virtual void OnPlayerStay(ICharacterModel player) { }
    protected virtual void OnPlayerExit(ICharacterModel player) { }
    protected virtual void OnLocalPlayerEnter(ICharacterModel player) { }
    protected virtual void OnLocalPlayerExit(ICharacterModel player) { }

}