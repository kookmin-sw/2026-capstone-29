using StarterAssets;
using System.Collections;
using UnityEngine;

// 섭취 즉시 이동속도 배율이 적용되고, duration 경과 후 자동 해제.
// 배율 값을 1.0 미만으로 설정하면 감속 디버프 아이템으로도 사용 가능.

[CreateAssetMenu(menuName = "Item/Passive/ConvertSpeed/Effect")]
public class ConvertSpeed : ScriptableObject, IPassive
{
    [Header("아이템 설정")]
    [SerializeField] private float duration = 10f;
    [SerializeField] private float moveSpeedMultiplier = 1.3f; // 1.0 미만이면 감속

    public float AvailableTime => duration;

    // 효과 본체. ItemManager가 StartCoroutine으로 실행한다.
    // 패시브이므로 플레이어 입력 없이 장착 즉시 호출됨.
    public virtual IEnumerator Activate(GameObject owner)
    {
        if (owner == null) yield break;

        Debug.Log("[ConvertSpeed] 활성화 시작");

        UnifiedThirdPersonController controller = owner.GetComponent<UnifiedThirdPersonController>();
        float originalMoveMul = 1f;
        bool moveApplied = false;

        if (controller != null)
        {
            originalMoveMul = controller.GetSpeedMultiplier();
            controller.SetSpeedMultiplier(originalMoveMul * moveSpeedMultiplier);
            moveApplied = true;
        }

        yield return new WaitForSeconds(duration);

        if (moveApplied && controller != null)
        {
            controller.SetSpeedMultiplier(originalMoveMul);
        }

        Debug.Log("[ConvertSpeed] 활성화 종료");
    }

    // 중도 해제 시 호출 (ItemManager 타이머가 StopCoroutine 후 부름).
    // ScriptableObject는 원본 값을 안전하게 기억 못 하므로 배율로 나눠서 원복.
    public virtual void OnDeactivate(GameObject owner)
    {
        if (owner == null) return;
        RemoveEffect(owner);
    }

    protected virtual void ApplyEffect(GameObject owner) { }

    protected virtual void RemoveEffect(GameObject owner)
    {
        UnifiedThirdPersonController controller = owner.GetComponent<UnifiedThirdPersonController>();
        if (controller != null && moveSpeedMultiplier > 0f)
        {
            controller.SetSpeedMultiplier(controller.GetSpeedMultiplier() / moveSpeedMultiplier);
        }
    }
}