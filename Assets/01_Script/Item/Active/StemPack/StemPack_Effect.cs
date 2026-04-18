using StarterAssets;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Item/Active/StemPack/Effect")]
public class StemPack_Effect : ScriptableObject, IActive
{
    [Header("아이템 설정")]
    [SerializeField] private float duration = 8f;
    [SerializeField] private float initialDamage = 10f;   // 사용 즉시 자해 데미지

    [SerializeField] private float moveSpeedMultiplier = 1.2f;   // 이동 속도 배율
    [SerializeField] private float animSpeedMultiplier = 1.2f; // 애니메이션 배율

    public float AvailableTime => duration;

    public virtual IEnumerator Activate(GameObject owner)
    {
        if (owner == null) yield break;

        Debug.Log("[StemPack] 활성화 시작");

        ICharacterModel model = owner.GetComponent<ICharacterModel>();
        if (model != null)
        {
            model.RequestTakeDamage(initialDamage);
        }

        //이동속도 조정
        UnifiedThirdPersonController controller = owner.GetComponent<UnifiedThirdPersonController>();
        float originalMoveMul = 1f;
        bool moveApplied = false;

        if (controller != null)
        {
            originalMoveMul = controller.GetSpeedMultiplier();
            controller.SetSpeedMultiplier(originalMoveMul * moveSpeedMultiplier);
            moveApplied = true;
        }

        //애니메이션 속도 조정
        Animator animator = owner.GetComponentInChildren<Animator>();
        float originalAnimSpeed = 1f;
        bool animApplied = false;

        if (animator != null)
        {
            originalAnimSpeed = animator.speed;
            animator.speed = originalAnimSpeed * animSpeedMultiplier;
            animApplied = true;
        }

        yield return new WaitForSeconds(duration);


        if (moveApplied && controller != null)
        {
            controller.SetSpeedMultiplier(originalMoveMul);
        }
        if (animApplied && animator != null)
        {
            animator.speed = originalAnimSpeed;
        }

        Debug.Log("[StemPack] 활성화 종료");
    }

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

        Animator animator = owner.GetComponentInChildren<Animator>();
        if (animator != null && animSpeedMultiplier > 0f)
        {
            animator.speed /= animSpeedMultiplier;
        }
    }
}