using System;
using System.Collections.Generic;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

/// <summary>
/// 가중치가 설정된 스폰 구역를 정의하여 아이템 군을 스폰한다.
///
/// - Mirror 환경: 서버에서만 스폰하고 NetworkServer.Spawn() 으로 동기화한다.
/// - 오프라인 환경: 일반 Instantiate 로 동작한다.
///
/// 사용법
///   1) 빈 GameObject 에 이 컴포넌트 추가
///   2) areas 에 박스를 여러 개 넣고 각각 weight, yOffset 지정
///      - Box 좌표는 이 ItemSpawner GameObject 의 로컬 좌표 기준
///      - 가중치 비율로 박스가 선택됨 (예: 0.3 vs 0.7)
///   3) groups 에 아이템 군과 프리팹/최대수/주기 설정
///   4) 아이템 픽업 시 UnifiedItemPickUp 에서 자동으로 NotifyItemConsumed 호출
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Serializable]
    public class Area
    {
        [Tooltip("디버그/식별용 이름.")]
        public string name = "Area";

        [Tooltip("이 ItemSpawner GameObject 의 로컬 좌표 기준 박스 중심.")]
        public Vector3 center = Vector3.zero;

        [Tooltip("로컬 좌표 기준 박스 크기 (각 축 길이).")]
        public Vector3 size = new Vector3(2f, 1f, 2f);

        [Tooltip("이 박스가 스폰 후보로 선택될 가중치. 0 이하이면 후보에서 제외된다.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("이 박스에서 스폰될 때 Y 값에 더해지는 보정값. (지면에 살짝 띄울 때 사용)")]
        public float yOffset = 0f;
    }

    [Header("스폰 영역")]
    [Tooltip("스폰 후보 박스들. 각 박스의 weight 에 따라 확률적으로 선택된다.")]
    public List<Area> areas = new List<Area> { new Area() };

    [Header("아이템 군")]
    public List<ItemSpawnGroup> groups = new List<ItemSpawnGroup>();

    [Header("스폰 시도 제어")]
    [Tooltip("랜덤 위치를 잡았을 때 다른 오브젝트와 겹치는지 체크할지 여부.")]
    public bool checkOverlap = false;

    [Tooltip("Overlap 체크 시 사용할 반지름.")]
    public float overlapCheckRadius = 0.5f;

    [Tooltip("Overlap 체크 시 충돌로 간주할 레이어.")]
    public LayerMask overlapMask = ~0;

    [Tooltip("좌표 후보 재시도 횟수. 모두 실패하면 마지막 후보를 그냥 사용한다.")]
    [Min(1)] public int maxPlacementAttempts = 8;

    // 픽업된 인스턴스 → 어느 군 소속이었는지 역매핑
    readonly Dictionary<GameObject, ItemSpawnGroup> _instanceToGroup = new Dictionary<GameObject, ItemSpawnGroup>();

    //현재 서버 권한이 있는지 여부. Mirror 미사용 환경에서는 항상 true
    bool HasAuthority
    {
        get
        {
#if MIRROR
           
            if (NetworkServer.active) return true;
            if (NetworkClient.active && !NetworkServer.active) return false;

            return true;
#else
            return true;
#endif
        }
    }

    void Start()
    {
        // 서버/오프라인에서만 초기 스폰 수행
        if (!HasAuthority) return;

        foreach (var group in groups)
        {
            if (group.fillOnStart)
            {
                int needed = group.maxConcurrent;
                for (int i = 0; i < needed; i++)
                {
                    TrySpawnOne(group);
                }
            }
        }
    }

    void Update()
    {
        if (!HasAuthority) return;

        float dt = Time.deltaTime;

        // 각 군의 대기 타이머를 감소시키고, 0 이하가 되면 재스폰
        foreach (var group in groups)
        {
            for (int i = group.pendingRespawnTimers.Count - 1; i >= 0; i--)
            {
                group.pendingRespawnTimers[i] -= dt;
                if (group.pendingRespawnTimers[i] <= 0f)
                {
                    group.pendingRespawnTimers.RemoveAt(i);
                    TrySpawnOne(group);
                }
            }

            // 누군가 외부에서 그냥 Destroy 해버린 경우 정리
            for (int i = group.liveInstances.Count - 1; i >= 0; i--)
            {
                if (group.liveInstances[i] == null)
                    group.liveInstances.RemoveAt(i);
            }
        }
    }


    //  아이템이 사용되었을 때 호출. UnifiedItemPickUp.cs에서 호출할 예정
    // 서버는 해당 아이템이 어떤 군 소속인지 찾아서 재스폰 타이머를 시작한다.
    public void NotifyItemConsumed(GameObject item)
    {
        if (item == null) return;
        if (!HasAuthority) return;

        if (_instanceToGroup.TryGetValue(item, out ItemSpawnGroup group))
        {
            group.liveInstances.Remove(item);
            _instanceToGroup.Remove(item);
            group.pendingRespawnTimers.Add(group.respawnDelay);
        }
        // 매핑이 없으면 = 이 스포너가 만든 아이템이 아님. 무시.
    }
     

    // 각 스폰 박스 이름으로 아이템 사용 통지. (인스턴스 매핑이 없거나, 픽업 측에서 GameObject 참조를 잃은 경우 폴백)
    public void NotifyItemConsumedByGroup(string groupName)
    {
        if (!HasAuthority) return;
        var group = groups.Find(g => g.groupName == groupName);
        if (group == null) return;
        group.pendingRespawnTimers.Add(group.respawnDelay);
    }

    //아이템의 스폰을 시도하는 함수
    bool TrySpawnOne(ItemSpawnGroup group)
    {
        if (group.prefabs == null || group.prefabs.Length == 0) return false;
        if (group.liveInstances.Count >= group.maxConcurrent) return false;

        Area area = PickAreaByWeight();
        if (area == null) return false;

        Vector3 pos = FindPlacement(area);
        Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

        GameObject prefab = group.prefabs[UnityEngine.Random.Range(0, group.prefabs.Length)];
        if (prefab == null) return false;

        GameObject inst = Instantiate(prefab, pos, rot);

        // 픽업 측에서 이 스포너를 다시 찾을 수 있도록 마커 부착
        var origin = inst.GetComponent<SpawnedItemOrigin>();
        if (origin == null) origin = inst.AddComponent<SpawnedItemOrigin>();
        origin.spawner = this;

#if MIRROR
        // Mirror 모드에서 NetworkIdentity 가 붙어있다면 네트워크 스폰
        if (NetworkServer.active && inst.GetComponent<NetworkIdentity>() != null)
        {
            NetworkServer.Spawn(inst);
        }
#endif

        group.liveInstances.Add(inst);
        _instanceToGroup[inst] = group;
        return true;
    }

    //소환할 area를 가중치를 기반으로 정함
    Area PickAreaByWeight()
    {
        float total = 0f;
        foreach (var a in areas)
        {
            if (a == null) continue;
            if (a.weight <= 0f) continue;
            total += a.weight;
        }
        if (total <= 0f) return null;

        float r = UnityEngine.Random.value * total;
        float acc = 0f;
        foreach (var a in areas)
        {
            if (a == null || a.weight <= 0f) continue;
            acc += a.weight;
            if (r <= acc) return a;
        }
        // 부동소수 오차 폴백
        for (int i = areas.Count - 1; i >= 0; i--)
        {
            if (areas[i] != null && areas[i].weight > 0f)
                return areas[i];
        }
        return null;
    }

    //박스 내부의 랜덤 점을 월드 좌표로 반환.
    Vector3 GetRandomPointInArea(Area area)
    {
        Vector3 half = area.size * 0.5f;
        Vector3 localPoint = area.center + new Vector3(
            UnityEngine.Random.Range(-half.x, half.x),
            UnityEngine.Random.Range(-half.y, half.y),
            UnityEngine.Random.Range(-half.z, half.z)
        );
        Vector3 world = transform.TransformPoint(localPoint);
        world.y += area.yOffset;
        return world;
    }

    //정한 area를 기준으로 구체적인 소환 좌표를 특정
    Vector3 FindPlacement(Area area)
    {
        Vector3 candidate = GetRandomPointInArea(area);
        if (!checkOverlap) return candidate;

        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            if (!Physics.CheckSphere(candidate, overlapCheckRadius, overlapMask, QueryTriggerInteraction.Ignore))
                return candidate;
            candidate = GetRandomPointInArea(area);
        }
        return candidate; // 다 실패하면 랜덤 장소를 선정
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        DrawAreas(new Color(0.2f, 1f, 0.4f, 0.10f), new Color(0.2f, 1f, 0.4f, 0.6f));
    }

    void OnDrawGizmosSelected()
    {
        DrawAreas(new Color(0.2f, 1f, 0.4f, 0.25f), new Color(0.2f, 1f, 0.4f, 1f));
    }

    void DrawAreas(Color fill, Color wire)
    {
        if (areas == null) return;
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        foreach (var a in areas)
        {
            if (a == null) continue;
            // 가중치 0 인 박스는 옅게 표시
            float alphaMul = a.weight > 0f ? 1f : 0.3f;
            Gizmos.color = new Color(fill.r, fill.g, fill.b, fill.a * alphaMul);
            Gizmos.DrawCube(a.center, a.size);
            Gizmos.color = new Color(wire.r, wire.g, wire.b, wire.a * alphaMul);
            Gizmos.DrawWireCube(a.center, a.size);
        }
        Gizmos.matrix = prev;
    }
#endif
}