using System.Collections.Generic;
using UnityEngine;
using Mirror;
/*
 플레이어가 '주는 데미지'에 대한 배율을 일시적으로 증폭시키는 컴포넌트.
 배터리 같은 패시브 아이템이 AddStack()으로 스택을 쌓고,
 CharacterHitBox가 히트박스를 활성화하는 순간(=공격 시도) ConsumeOneAttempt()를 호출해 CharacterHitbox의 multipleDMG에 반영하고, 스택을 소모한다.
 이 구조를 활용하여 디버프도 구현할 수 있겠다.
*/
public class DamageAmplifier : NetworkBehaviour
{
    public event System.Action OnAllStacksConsumed;

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

    [SyncVar] private float _syncedMultiplier = 1f;
    [SyncVar] private int _syncedStackCount = 0;


    // 오프라인이면 항상 true, 온라인이면 Mirror의 isServer 사용
    private bool CanMutate => AuthorityGuard.IsOffline || isServer;

    //현재 다음 공격에 적용될 곱연산 배율. 활성 스택이 없으면 1f의 배율을 가지게 된다.
    public float Multiplier
    {
        get
        {
            // 오프라인: SyncVar 동기화 안 되므로 직접 계산
            // 서버: 직접 계산 (SyncVar 쓰기 주체)
            // 클라이언트: SyncVar 값 읽기
            if (AuthorityGuard.IsOffline || isServer)
                return CalcMultiplier();
            return _syncedMultiplier;
        }
    }

    // 현재 보유한 스택 개수. UI 표시 등에 유용
    public int StackCount
    {
        get
        {
            if (AuthorityGuard.IsOffline || isServer)
                return _stacks.Count;
            return _syncedStackCount;
        }
    }

    private float CalcMultiplier()
    {
        float m = 1f;
        for (int i = 0; i < _stacks.Count; i++)
            m *= _stacks[i].Multiplier;
        return m;
    }

    // SyncVar 갱신. 온라인 서버에서만 의미 있음. 오프라인에선 no-op.
    private void RefreshSyncVars()
    {
        if (!CanMutate) return;
        if (!AuthorityGuard.IsOffline)
        {
            _syncedMultiplier = CalcMultiplier();
            _syncedStackCount = _stacks.Count;
        }
    }

    // 새 배율 스택을 추가
    public void AddStack(object owner, float multiplier, int hits)
    {
        if (hits <= 0 || multiplier <= 0f) return;
        _stacks.Add(new Stack(owner, multiplier, hits));
        RefreshSyncVars();
    }


    // owner가 소유한 스택을 하나 선입선출로 제거. 없으면 false.

    public bool RemoveStack(object owner)
    {
        for (int i = 0; i < _stacks.Count; i++)
        {
            if (ReferenceEquals(_stacks[i].Owner, owner))
            {
                _stacks.RemoveAt(i);
                RefreshSyncVars();
                return true;
            }
        }
        return false;
    }

    // DamageAmplifier에 추가. 동일 프레임에서 발생하는 EnableHitbox
    private int _lastAttemptFrame = -1;


    public void ConsumeOneAttempt()
    {
        // 오프라인: 바로 실행
        if (AuthorityGuard.IsOffline)
        {
            ConsumeLocal();
            return;
        }

        // 온라인 서버: 바로 실행
        if (isServer)
        {
            ConsumeLocal();
            return;
        }

        if (isOwned)
        {
            CmdConsumeOneAttempt();
        }
        // else: 원격 플레이어의 DamageAmplifier — 서버가 알아서 처리하므로 무시
    }

    [Command]
    private void CmdConsumeOneAttempt()
    {
        ConsumeLocal();
    }

    private void ConsumeLocal()
    {
        if (_lastAttemptFrame == Time.frameCount) return;
        _lastAttemptFrame = Time.frameCount;

        for (int i = _stacks.Count - 1; i >= 0; i--)
        {
            _stacks[i].RemainingHits--;
            if (_stacks[i].RemainingHits <= 0)
                _stacks.RemoveAt(i);
        }

        RefreshSyncVars();

        if (_stacks.Count == 0)
            OnAllStacksConsumed?.Invoke();
    }

    // 특정 owner의 스택이 아직 남아있는지 조회.
    public bool HasStackFrom(object owner)
    {
        for (int i = 0; i < _stacks.Count; i++)
            if (ReferenceEquals(_stacks[i].Owner, owner)) return true;
        return false;
    }
}