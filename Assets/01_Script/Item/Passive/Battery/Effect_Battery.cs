using System.Collections;
using UnityEngine;


// 배터리 효과 : 발동 후 '1회의 공격'에 한해 '주는 데미지'를 지정 배율만큼 증폭시킨다.
[CreateAssetMenu(menuName = "Item/Passive/Battery/Effect")]
public class Effect_Battery : PassiveTemplate
{
    [Header("배터리 설정")]
    [Tooltip("공격 데미지 배율 (2 = 2배).")]
    [SerializeField] private float damageMultiplier = 2f;

    [Tooltip("효과가 적용될 공격 횟수.")]
    [SerializeField] private int hitCount = 1;

    private class BatteryStackKey
    {
        public readonly Effect_Battery asset;
        public readonly GameObject player;
        public BatteryStackKey(Effect_Battery a, GameObject p) { asset = a; player = p; }
    }

    // 같은 owner에게 RemoveStack 호출 시 정확히 걸어둔 스택을 찾기 위한 캐시.
    private readonly System.Collections.Generic.Dictionary<GameObject, BatteryStackKey> _keys
        = new System.Collections.Generic.Dictionary<GameObject, BatteryStackKey>();

    protected override void ApplyEffect(GameObject owner)
    {
        if (owner == null) return;

        DamageAmplifier amp = owner.GetComponent<DamageAmplifier>();
        if (amp == null) amp = owner.AddComponent<DamageAmplifier>();

        BatteryStackKey key = new BatteryStackKey(this, owner);
        _keys[owner] = key;

        amp.AddStack(key, damageMultiplier, hitCount);
        Debug.Log($"[Effect_Battery] 장착 → {owner.name} 공격력 {damageMultiplier}배 / {hitCount}회");
    }


    protected override void RemoveEffect(GameObject owner)
    {
        if (owner == null) return;
        if (!_keys.TryGetValue(owner, out BatteryStackKey key)) return;

        DamageAmplifier amp = owner.GetComponent<DamageAmplifier>();
        if (amp != null && amp.HasStackFrom(key))
        {
            amp.RemoveStack(key);
            Debug.Log($"[Effect_Battery] duration 만료 전 미사용 → 스택 회수 ({owner.name})");
        }
        else
        {
            // 이미 ConsumeOneHit()으로 자연 소모된 경우
            Debug.Log($"[Effect_Battery] 정상 소모 완료 ({owner.name})");
        }

        _keys.Remove(owner);
    }
}