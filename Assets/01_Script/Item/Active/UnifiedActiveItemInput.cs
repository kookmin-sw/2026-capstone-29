using UnityEngine;
using StarterAssets;


public class UnifiedActiveItemInput : MonoBehaviour
{
    private StarterAssetsInputs _input;
    private ICharacterModel _model;
    private UnifiedItemManager unifiedManager;
    private ItemManager legacyManager;
    private UnifiedItemPickUp pickUp;

    private void Awake()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _model = GetComponent<ICharacterModel>();
        unifiedManager = GetComponent<UnifiedItemManager>();
        if (unifiedManager == null) legacyManager = GetComponent<ItemManager>();
        pickUp = GetComponent<UnifiedItemPickUp>();

        if (_input == null)
            Debug.LogError($"[{nameof(UnifiedActiveItemInput)}] StarterAssetsInputs가 없음.");
    }

    private void Update()
    {
        // 입력 권한: 오프라인은 본인, 온라인은 로컬 플레이어만
        if (!AuthorityGuard.IsLocallyControlled(gameObject)) return;
        if (_input == null) return;

        // 액티브 아이템 사용
        if (_input.useActive)
        {
            TryUseActive();
            _input.useActive = false;
        }

        // 아이템 픽업
        if (_input.interaction)
        {
            if (pickUp != null)
                pickUp.TryPickupNearest();

            _input.interaction = false;   // 입력 소비
        }
    }

    private void TryUseActive()
    {
        // 매니저 게이트와 동일한 조건으로 사전 검사.
        // 매니저가 거부할 입력은 모션도 발동 안 시킨다 — 헛동작 방지.
        bool willActivate = false;

        if (unifiedManager != null)
        {
            willActivate = unifiedManager.HasActive() && !unifiedManager.IsActiveRunning();
            if (willActivate) unifiedManager.RequestUseActive();
        }
        else if (legacyManager != null)
        {
            // legacy ItemManager가 동일한 헬퍼를 제공한다고 가정.
            // 없으면 그냥 호출하고 모션은 생략하거나, legacy 매니저에도 동일 헬퍼 추가.
            legacyManager.RequestUseActive();
            willActivate = true;   // legacy 측 정보가 없으면 일단 모션 재생
        }

        if (willActivate && _model != null)
            _model.RequestUseActive();
    }
}