using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 같은 주기/최대 동시 수량을 공유하는 아이템 묶음을 정의.
/// 예) "회복류" 군 = { 포션, 붕대 }, 최대 3개, 사용 후 10초 뒤 재생성
/// </summary>
[Serializable]
public class ItemSpawnGroup
{
    [Tooltip("디버그/식별용 이름")]
    public string groupName = "Group";

    [Tooltip("이 군에서 스폰될 수 있는 프리팹들. 스폰 시 균등 랜덤 선택.")]
    public GameObject[] prefabs;

    [Tooltip("동시에 월드에 존재할 수 있는 이 군의 최대 수량.")]
    [Min(1)] public int maxConcurrent = 3;

    [Tooltip("아이템이 '소비' 된 후, 다음 스폰까지 기다리는 시간(초). " +
             "주의: 첫 스폰 후 N개를 한 번에 채우는 것이 아니라, '소비 이벤트' 가 발생해야 타이머가 시작된다.")]
    [Min(0f)] public float respawnDelay = 10f;

    [Tooltip("게임 시작 시점에 maxConcurrent 만큼 즉시 채워둘지 여부.")]
    public bool fillOnStart = true;

    // --- 런타임 상태 ---
    [NonSerialized] public readonly List<GameObject> liveInstances = new List<GameObject>();

    // 대기 중인 재스폰 타이머 (소비된 개수만큼 누적된다)
    [NonSerialized] public readonly List<float> pendingRespawnTimers = new List<float>();
}