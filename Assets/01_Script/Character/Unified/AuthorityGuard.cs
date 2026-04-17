using Mirror;
using UnityEngine;

/// <summary>
/// "이 캐릭터를 내가 조작해도 되는가?" 판단을 한 곳에 모은 헬퍼.
/// - Mirror가 실행 중이 아닐 때(= 오프라인/싱글 모드): 항상 true.
/// - Mirror가 실행 중일 때: NetworkIdentity.isLocalPlayer 기준.
/// </summary>
public static class AuthorityGuard
{
    /// <summary>Mirror가 서버/클라이언트 어느 것으로도 실행되지 않으면 오프라인.</summary>
    public static bool IsOffline => !NetworkClient.active && !NetworkServer.active;

    /// <summary>해당 GameObject를 로컬에서 조작해도 되는가?</summary>
    public static bool IsLocallyControlled(GameObject go)
    {
        if (go == null) return false;
        if (IsOffline) return true;

        if (go.TryGetComponent(out NetworkIdentity id))
            return id.isLocalPlayer;

        // NetworkIdentity가 없는 오브젝트 → 로컬 전용으로 간주
        return true;
    }

    /// <summary>이 NetworkBehaviour/NetworkIdentity가 권위 있는 소유자인가?</summary>
    public static bool IsLocallyControlled(NetworkIdentity id)
    {
        if (IsOffline) return true;
        return id != null && id.isLocalPlayer;
    }
}
