using System.Collections;
using Mirror;
using UnityEngine;

/*
    "오브젝트를 일정 시간 동안 맵에 띄워둔다"가 공통 동작.
    실제 효과(플레이어 감지, 데미지/버프 적용 등)는 생성 오브젝트가 책임진다.

    라이프사이클:
    1) 필드의 픽업 오브젝트 SetItem이 이 에셋을 ItemManager.field에 주입
    2) ItemManager가 장착 즉시 this.Activate()를 StartCoroutine으로 실행
    3) Activate가 fieldPrefab을 NetworkServer.Spawn
    4) duration 경과 → 장판 Destroy → 코루틴 종료
       또는 ItemManager 타이머 만료 → OnDeactivate에서 장판 강제 정리
*/
[CreateAssetMenu(menuName = "Item/Field/Templete")]
public class FieldItem_Field : ScriptableObject, IField
{
    [Header("아이템 설정")]
    [SerializeField] private float duration = 10f;

    [Header("장판 프리팹 (NetworkIdentity 필수)")]
    [Tooltip("Mirror 등록된 스폰 가능 프리팹. FieldEffect 상속 컴포넌트가 효과를 담당.")]
    [SerializeField] private GameObject fieldPrefab;

    [Header("스폰 위치 설정")]
    [Tooltip("월드 좌표 고정 스폰 위치. 자식 클래스에서 GetSpawnPosition()을 오버라이드해 동적으로 결정 가능.")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    public float AvailableTime => duration;

    // 현재 활성화된 장판 인스턴스 (OnDeactivate에서 회수용)
    private GameObject _spawnedField;

    public virtual IEnumerator Activate()
    {
        Debug.Log($"[FieldItem] {name} 장판 스폰 시작");

        if (fieldPrefab == null)
        {
            Debug.LogError($"[FieldItem] {name}: fieldPrefab이 비어있음. 인스펙터 확인 필요.");
            yield break;
        }

        // 권위가 있을 때만 스폰: 온라인은 서버, 오프라인은 본인
        bool canSpawn = AuthorityGuard.IsOffline || NetworkServer.active;
        if (!canSpawn)
        {
            Debug.LogWarning($"[FieldItem] {name}: 서버가 아니거나 오프라인 모드가 아님. 스폰 스킵.");
            yield break;
        }

        Vector3 pos = GetSpawnPosition();
        _spawnedField = Instantiate(fieldPrefab, pos, Quaternion.identity);

        if (AuthorityGuard.IsOffline)
        {
            HardenOfflineObject(_spawnedField);
        }
        else if (_spawnedField.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
        {
            Debug.Log("[FieldItem] 스폰!");
            NetworkServer.Spawn(_spawnedField);
        }

        yield return null;

        if (_spawnedField != null)
        {
            FieldEffect effect = _spawnedField.GetComponent<FieldEffect>();
            if (effect != null) effect.Initialize(duration);
        }

        // duration 동안 살아있음
        float remaining = Mathf.Max(0f, duration - Time.deltaTime);
        yield return new WaitForSeconds(remaining);

        // 자연 종료 → 정리
        DespawnField();
        Debug.Log($"[FieldItem] {name} 장판 자연 종료");
    }

    public virtual void OnDeactivate()
    {
        // 외부에서 강제 종료된 경우 (ItemManager 타이머 만료 등)
        if (_spawnedField != null)
        {
            DespawnField();
            Debug.Log($"[FieldItem] {name} 외부 종료 → 장판 정리");
        }
    }

    // 위치를 동적으로 정하여 아이템 오브젝트를 스폰하려면 오버라이드.
    protected virtual Vector3 GetSpawnPosition() => spawnPosition;

    private void DespawnField()
    {
        if (_spawnedField == null) return;

        if (AuthorityGuard.IsOffline)
        {
            Destroy(_spawnedField);
        }
        else if (_spawnedField.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
        {
            NetworkServer.Destroy(_spawnedField);
        }
        else
        {
            Destroy(_spawnedField);
        }

        _spawnedField = null;
    }

    // 오프라인에서 Instantiate된 오브젝트가 Mirror NetworkIdentity에 의해 비활성화되거나 SetParent가 막히는 것을 우회.
    private static void HardenOfflineObject(GameObject obj)
    {
        if (obj == null) return;
        if (!AuthorityGuard.IsOffline) return;

        if (obj.TryGetComponent(out NetworkIdentity nid))
            nid.enabled = false;

        if (!obj.activeSelf) obj.SetActive(true);
    }
}