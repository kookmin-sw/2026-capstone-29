using System.Collections.Generic;
using UnityEngine;
/*
 플레이어가 '주는 데미지'에 대한 배율을 일시적으로 증폭시키는 컴포넌트.
 배터리 같은 패시브 아이템이 AddStack()으로 스택을 쌓고,
 CharacterHitBox가 히트박스를 활성화하는 순간(=공격 시도) ConsumeOneAttempt()를 호출해 CharacterHitbox의 multipleDMG에 반영하고, 스택을 소모한다.
 이 구조를 활용하여 디버프도 구현할 수 있겠다.
*/
public class DamageAmplifier : MonoBehaviour
{
    private class Stack
    {
        public readonly object Owner;    // 스택을 걸어둔 플레이어
        public readonly float Multiplier;
        public int RemainingHits;

        public Stack(object owner, float multiplier, int hits)
        {
            Owner = owner;
            Multiplier = multiplier;
            RemainingHits = hits;
        }
    }

    private readonly List<Stack> _stacks = new List<Stack>();

    //현재 다음 공격에 적용될 곱연산 배율. 활성 스택이 없으면 1f의 배율을 가지게 된다.
    public float Multiplier
    {
        get
        {
            float m = 1f;
            for (int i = 0; i < _stacks.Count; i++)
                m *= _stacks[i].Multiplier;
            return m;
        }
    }

    // 현재 보유한 스택 개수. UI 표시 등에 유용
    public int StackCount => _stacks.Count;


    // 새 배율 스택을 추가
    public void AddStack(object owner, float multiplier, int hits)
    {
        if (hits <= 0 || multiplier <= 0f) return;
        _stacks.Add(new Stack(owner, multiplier, hits));
    }


    // owner가 소유한 스택을 하나 선입선출로 제거. 없으면 false.
    
    public bool RemoveStack(object owner)
    {
        for (int i = 0; i < _stacks.Count; i++)
        {
            if (ReferenceEquals(_stacks[i].Owner, owner))
            {
                _stacks.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    // DamageAmplifier에 추가. 동일 프레임에서 발생하는 EnableHitbox
    private int _lastAttemptFrame = -1;

    // 모든 활성 스택의 RemainingHits를 1 감소시키고 0이 된 스택은 제거. CharacterHitBox.EnableHitbox에서 호출.
    public void ConsumeOneAttempt()
    {
        if (_lastAttemptFrame == Time.frameCount) return; // 같은 프레임 중복 무시
        _lastAttemptFrame = Time.frameCount;

        // 기존 소모 로직
        for (int i = _stacks.Count - 1; i >= 0; i--)
        {
            _stacks[i].RemainingHits--;
            if (_stacks[i].RemainingHits <= 0)
                _stacks.RemoveAt(i);
        }
    }

    // 특정 owner의 스택이 아직 남아있는지 조회.
    public bool HasStackFrom(object owner)
    {
        for (int i = 0; i < _stacks.Count; i++)
            if (ReferenceEquals(_stacks[i].Owner, owner)) return true;
        return false;
    }
}