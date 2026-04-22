using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 씬에 미리 배치된 오브젝트(필드 아이템 등)가 Mirror의 NetworkIdentity 처리로
/// 싱글플레이에서 자동 비활성화되는 문제를 다중 대상으로 우회한다.
///
/// <see cref="OfflinePlayerBootstrap"/>의 리스트 버전.
///
/// 사용법:
/// 1) 씬에 빈 GameObject를 만든다 (예: "_OfflineSceneBootstrap").
/// 2) 이 컴포넌트를 붙이고 <see cref="targets"/>에 활성화할 필드 아이템들을 등록한다.
/// 3) 온라인(호스트/클라)이면 자동으로 스킵 → 네트워크 플레이에는 영향 없음.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class OfflineSceneObjectBootstrap : MonoBehaviour
{
    [Header("싱글플레이 대상 목록")]
    [Tooltip("오프라인 모드에서 확실하게 활성화시킬 오브젝트들.")]
    [SerializeField] private List<GameObject> targets = new List<GameObject>();

    [Header("옵션")]
    [Tooltip("true면 각 대상의 NetworkIdentity를 비활성화한다(Mirror 간섭 차단).")]
    [SerializeField] private bool disableNetworkIdentity = true;

    [Tooltip("Start 이후에도 잠시 동안 비활성화되는지 감시해서 다시 켠다.")]
    [SerializeField] private int watchFrames = 10;

    [Tooltip("로그 출력 여부.")]
    [SerializeField] private bool verbose = false;

    private void Awake()
    {
        if (!AuthorityGuard.IsOffline)
        {
            if (verbose) Debug.Log("[OfflineSceneObjectBootstrap] 네트워크 모드 — 아무 것도 하지 않음.");
            return;
        }
        ApplyAll("Awake");
    }

    private void Start()
    {
        if (!AuthorityGuard.IsOffline) return;
        ApplyAll("Start");
        if (watchFrames > 0) StartCoroutine(WatchActive());
    }

    private IEnumerator WatchActive()
    {
        for (int i = 0; i < watchFrames; i++)
        {
            yield return null;
            ApplyAll($"Watch#{i}", silentIfAlreadyOn: true);
        }
    }

    private void ApplyAll(string phase, bool silentIfAlreadyOn = false)
    {
        foreach (var obj in targets)
        {
            if (obj == null) continue;

            if (disableNetworkIdentity && obj.TryGetComponent(out NetworkIdentity id))
            {
                if (id.enabled) id.enabled = false;
            }

            // 부모 체인까지 전부 켜 주기
            Transform t = obj.transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                t = t.parent;
            }

            if (verbose && !(silentIfAlreadyOn && obj.activeInHierarchy))
                Debug.Log($"[OfflineSceneObjectBootstrap][{phase}] {obj.name} 활성화.");
        }
    }
}
