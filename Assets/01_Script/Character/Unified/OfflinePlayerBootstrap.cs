using Mirror;
using UnityEngine;

/// <summary>
/// 싱글플레이(오프라인)에서 씬에 미리 배치된 플레이어 오브젝트가
/// Mirror의 NetworkIdentity 처리로 인해 자동 비활성화되는 문제를 우회한다.
///
/// 사용법:
/// 1) 싱글용 게임 씬에 빈 GameObject를 하나 만들고 이름을 "_OfflineBootstrap" 등으로 짓는다.
/// 2) 이 컴포넌트를 해당 오브젝트에 붙인다.
/// 3) <see cref="playerObject"/> 슬롯에 씬에 배치된 플레이어 GameObject를 연결한다.
///
/// 네트워크(호스트/클라)로 들어오면 <see cref="AuthorityGuard.IsOffline"/>가 false이므로
/// 이 스크립트는 아무 것도 하지 않는다. → 네트워크 플레이에는 영향 없음.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class OfflinePlayerBootstrap : MonoBehaviour
{
    [Header("싱글플레이 대상")]
    [Tooltip("씬에 미리 배치된 플레이어 GameObject. Mirror에 의해 꺼졌을 수 있음.")]
    [SerializeField] private GameObject playerObject;

    [Header("옵션")]
    [Tooltip("true면 오프라인 모드에서 플레이어의 NetworkIdentity를 비활성화한다. " +
             "Mirror가 이후에 간섭하지 못하게 하는 안전장치.")]
    [SerializeField] private bool disableNetworkIdentity = true;

    [Tooltip("부트스트랩 완료 후 로그를 남길지 여부.")]
    [SerializeField] private bool verbose = true;

    private void Awake()
    {
        // 온라인(호스트/클라로 접속)이면 Mirror가 정상 처리하므로 건드리지 않는다.
        if (!AuthorityGuard.IsOffline)
        {
            if (verbose) Debug.Log("[OfflinePlayerBootstrap] 네트워크 모드 감지 — 아무 것도 하지 않음.");
            return;
        }

        if (playerObject == null)
        {
            Debug.LogWarning("[OfflinePlayerBootstrap] playerObject 슬롯이 비어 있습니다. Inspector에서 씬의 플레이어를 연결하세요.");
            return;
        }

        // Mirror의 scene-NetworkIdentity 처리 우회
        if (disableNetworkIdentity && playerObject.TryGetComponent(out NetworkIdentity id))
        {
            id.enabled = false;
            if (verbose) Debug.Log($"[OfflinePlayerBootstrap] {playerObject.name}의 NetworkIdentity를 비활성화했습니다.");
        }

        // 플레이어 재활성화
        if (!playerObject.activeSelf)
        {
            playerObject.SetActive(true);
            if (verbose) Debug.Log($"[OfflinePlayerBootstrap] {playerObject.name}을 활성화했습니다.");
        }
        else if (verbose)
        {
            Debug.Log($"[OfflinePlayerBootstrap] {playerObject.name}은 이미 활성 상태.");
        }
    }
}
