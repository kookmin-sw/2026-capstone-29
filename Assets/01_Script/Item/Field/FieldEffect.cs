using Mirror;
using UnityEngine;

/// <summary>
/// 두 종류의 콜백을 제공:
/// - 권위 측 (OnPlayerEnter/Stay/Exit): 데미지, 상태변경 등 게임 상태에 영향
///   온라인은 서버에서, 오프라인은 본인이 호출.
/// - 로컬 클라 측 (OnLocalPlayerEnter/Exit): 본인 카메라/UI/사운드 등 시각, 연출
///   오프라인이면 무조건 본인이 로컬 처리, 온라인이면 isLocalPlayer 체크.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public abstract class FieldEffect : NetworkBehaviour
{
    protected float lifetime;

    public virtual void Initialize(float duration)
    {
        lifetime = duration;
    }

    private bool HasAuthority => AuthorityGuard.IsOffline || isServer;

    private void OnTriggerEnter(Collider other)
    {
        ICharacterModel player = ResolvePlayer(other);
        if (player == null) return;

        // 권위 측: 게임 상태 변경
        if (HasAuthority) OnPlayerEnter(player);

        // 로컬 클라 측: 시각/연출 (각자 로컬에서만 적용)
        if (IsLocalPlayer(other)) OnLocalPlayerEnter(player);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!HasAuthority) return;  // Stay는 데미지 틱 등 권위 로직 전용
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
        // 오프라인은 본인이 곧 로컬 플레이어
        if (AuthorityGuard.IsOffline) return true;

        NetworkIdentity ni = other.GetComponentInParent<NetworkIdentity>();
        return ni != null && ni.isLocalPlayer;
    }

    protected virtual void OnPlayerEnter(ICharacterModel player) { }
    protected virtual void OnPlayerStay(ICharacterModel player) { }
    protected virtual void OnPlayerExit(ICharacterModel player) { }
    protected virtual void OnLocalPlayerEnter(ICharacterModel player) { }
    protected virtual void OnLocalPlayerExit(ICharacterModel player) { }
}