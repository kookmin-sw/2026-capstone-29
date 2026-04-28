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

    // 모든 플레이어에 대해 다음 데미지 적용 시기 기록. 서버에서만 활용함.
    private readonly Dictionary<ICharacterModel, float> _nextTickTime = new Dictionary<ICharacterModel, float>();

    // 서버에서는 대미지 처리

    protected override void OnPlayerEnter(ICharacterModel player)
    {
        _nextTickTime[player] = Time.time + damageTakeCycle;
    }

    protected override void OnPlayerStay(ICharacterModel player)
    {
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

    // 로컬에서는 안개 효과 연출을 카메라에 적용.

    protected override void OnLocalPlayerEnter(ICharacterModel player)
    {

        if (MistFogController.Instance != null)
        {
            MistFogController.Instance.ActivateMist();
        }
    }

    protected override void OnLocalPlayerExit(ICharacterModel player)
    {
        if (MistFogController.Instance != null)
        {
            MistFogController.Instance.DeactivateMist();
        }
    }

    private void OnDestroy()
    {
        if (MistFogController.Instance != null)
        {
            MistFogController.Instance.DeactivateMist();
        }
    }
}