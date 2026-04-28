using Mirror;
using UnityEngine;

/// <summary>
/// 필드 아이템(장판) 프리팹의 베이스 컴포넌트.
/// 자식 클래스가 트리거 콜백을 오버라이드해서 실제 효과를 정의.
///
/// 장판 프리팹 구성 권장:
/// - NetworkIdentity (필수, NetworkServer.Spawn용)
/// - Collider (isTrigger = true)
/// - 시각 표현(파티클, 메시 등)
/// - 이 클래스 상속 컴포넌트
///
/// 자식 클래스 예 (Effect_PoisonField 등):
/// - protected override void OnPlayerEnter(ICharacterModel player) { player.RequestTakeDamage(5f); }
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public abstract class FieldEffect : NetworkBehaviour
{

    protected float lifetime;

    /// <summary>
    /// 템플릿이 스폰 직후 호출. duration을 알려주는 용도.
    /// 자식이 시간 기반 효과(예: 점진적 강화)를 만들 때 활용.
    /// </summary>
    public virtual void Initialize(float duration)
    {
        lifetime = duration;
    }

    // ===== 트리거 콜백 (서버에서만 실제 효과 적용) =====

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;
        ICharacterModel player = ResolvePlayer(other);
        if (player != null) OnPlayerEnter(player);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isServer) return;
        ICharacterModel player = ResolvePlayer(other);
        if (player != null) OnPlayerStay(player);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isServer) return;
        ICharacterModel player = ResolvePlayer(other);
        if (player != null) OnPlayerExit(player);
    }

    /// <summary>
    /// Collider에서 ICharacterModel(=플레이어)을 추출.
    /// "Player" 태그 1차 필터로 잡잡한 콜라이더(아이템, 환경 등) 제외.
    /// </summary>
    private ICharacterModel ResolvePlayer(Collider other)
    {
        if (!other.CompareTag("Player")) return null;
        return other.GetComponentInParent<ICharacterModel>();
    }

    // ===== 자식 오버라이드 지점 =====

    /// <summary>플레이어가 장판에 처음 진입했을 때.</summary>
    protected virtual void OnPlayerEnter(ICharacterModel player) { }

    /// <summary>플레이어가 장판 안에 머무는 동안 매 프레임.</summary>
    protected virtual void OnPlayerStay(ICharacterModel player) { }

    /// <summary>플레이어가 장판을 벗어났을 때.</summary>
    protected virtual void OnPlayerExit(ICharacterModel player) { }
}