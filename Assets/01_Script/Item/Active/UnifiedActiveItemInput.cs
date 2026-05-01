using UnityEngine;
using Mirror;

/// <summary>
/// 액티브 아이템 사용 입력 컴포넌트.
/// - 온라인: 기존 <see cref="ActiveItemInput"/>과 동일하게 isLocalPlayer만 입력 처리.
/// - 오프라인: 본인이 무조건 입력 처리.
///
/// UnifiedItemManager 우선, 없으면 legacy ItemManager fallback.
/// StarterAssetsInputs 연동 이후 제거 예정 (원본 주석 그대로 유지).
/// </summary>
public class UnifiedActiveItemInput : NetworkBehaviour
{
    [SerializeField] private KeyCode useActiveKey = KeyCode.Q;

    private UnifiedItemManager unifiedManager;
    private ItemManager legacyManager;

    private void Awake()
    {
        unifiedManager = GetComponent<UnifiedItemManager>();
        if (unifiedManager == null) legacyManager = GetComponent<ItemManager>();
    }

    private void Update()
    {
        // 입력 권한: 오프라인은 본인, 온라인은 로컬 플레이어만
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;

        if (!Input.GetKeyDown(useActiveKey)) return;

        if (unifiedManager != null)
        {
            unifiedManager.RequestUseActive();
        }
        else if (legacyManager != null)
        {
            legacyManager.RequestUseActive();
        }
    }
}