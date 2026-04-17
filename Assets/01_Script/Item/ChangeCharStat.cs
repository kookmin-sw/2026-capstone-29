using System.Collections;
using UnityEngine;
using StarterAssets;


public class ChangeCharStat : MonoBehaviour
{
    private static MonoBehaviour AsMono(ICharacterModel target) => target as MonoBehaviour;

    //지속적인 대미지 부여
    public static Coroutine ApplyDamageOverTime(
        ICharacterModel target,
        float damagePerTick, //1틱에 들어가는 대미지
        float duration, // 지속 시간
        float tickInterval = 1f// 1틱으로 정의한 시간
        )
    {
        if (target == null) return null;
        MonoBehaviour host = AsMono(target);
        if (host == null) return null;
        return host.StartCoroutine(DamageOverTimeRoutine(target, damagePerTick, duration, tickInterval));
    }

    //지속적인 힐 부여(안쓰지 않을까)
    public static Coroutine ApplyHealOverTime(
        ICharacterModel target,
        float healPerTick,
        float duration,
        float tickInterval = 1f)
    {
        if (target == null) return null;
        MonoBehaviour host = AsMono(target);
        if (host == null) return null;
        // 음수 대미지로 회복
        return host.StartCoroutine(DamageOverTimeRoutine(target, -healPerTick, duration, tickInterval));
    }

    //1회에 걸쳐 부여하는 대미지
    public static void ApplyDamageInstant(ICharacterModel target, float damage)
    {
        if (target == null) return;
        target.RequestTakeDamage(damage);
    }

    //1회에 걸쳐 부여하는 힐(안쓰지않을까)
    public static void ApplyHealInstant(ICharacterModel target, float heal)
    {
        if (target == null) return;
        target.RequestTakeDamage(-heal);
    }

    private static IEnumerator DamageOverTimeRoutine(
        ICharacterModel target,
        float amountPerTick,
        float duration,
        float tickInterval)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null || target.IsDead) yield break;

            target.RequestTakeDamage(amountPerTick);

            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }


    public static Coroutine ApplySpeedModifier(
        ThirdPersonController target,
        float multiplier, //이동 속도 변경 계수
        float duration //지속 시간
        )
    {
        if (target == null) return null;
        return target.StartCoroutine(SpeedModifierRoutine(target, multiplier, duration));
    }

    private static IEnumerator SpeedModifierRoutine(
        ThirdPersonController target,
        float multiplier,
        float duration)
    {
        if (target == null) yield break;

        float originalMultiplier = target.GetSpeedMultiplier();
        target.SetSpeedMultiplier(multiplier);

        yield return new WaitForSeconds(duration);

        // 효과 종료 후 원래 배율로 복원
        if (target != null)
            target.SetSpeedMultiplier(originalMultiplier);
    }

}