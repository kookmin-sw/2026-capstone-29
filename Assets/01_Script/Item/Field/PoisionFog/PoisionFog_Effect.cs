using System.Collections.Generic;
using UnityEngine;


public class PoisonFog_Effect : FieldEffect
{
    [Header("독안개 설정")]
    [Tooltip("한 번 데미지를 줄 때 적용되는 양.")]
    [SerializeField] private float damagePerTick = 5f;

    [Header("피해 주기")]
    [Tooltip("데미지 적용 주기(초)")]
    [SerializeField] private float damageTakeCycle = 1f;

    // 플레이어별 "다음 데미지 적용 가능 시각" 기록.
    // 여러 명이 안개 안에 있어도 각자의 타이머로 독립 동작.
    private readonly Dictionary<ICharacterModel, float> _nextTickTime = new Dictionary<ICharacterModel, float>();

    protected override void OnPlayerEnter(ICharacterModel player)
    {
        // 진입 즉시 1회 데미지 + 다음 
        //player.RequestTakeDamage(damagePerTick);
        _nextTickTime[player] = Time.time + damageTakeCycle;
    }

    protected override void OnPlayerStay(ICharacterModel player)
    {
        // 키가 없으면 OnPlayerEnter가 누락된 케이스
        if (!_nextTickTime.TryGetValue(player, out float nextTime))
        {
            player.RequestTakeDamage(damagePerTick);
            _nextTickTime[player] = Time.time + damageTakeCycle;
            return;
        }

        if (Time.time >= nextTime)
        {
            player.RequestTakeDamage(damagePerTick);
            _nextTickTime[player] = nextTime + damageTakeCycle;
        }
    }

    protected override void OnPlayerExit(ICharacterModel player)
    {
        _nextTickTime.Remove(player);
    }
}