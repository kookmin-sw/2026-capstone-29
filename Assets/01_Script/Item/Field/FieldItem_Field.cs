using System.Collections;
using Mirror;
using UnityEngine;

/*
    ScriptableObject 기반 필드 아이템 템플릿.
    "장판 프리팹을 일정 시간 동안 맵에 띄워둔다"가 공통 동작.
    실제 효과(플레이어 감지, 데미지/버프 적용 등)는 장판 프리팹 자체(NetworkBehaviour)가 책임진다.

    라이프사이클:
    1) 필드의 픽업 오브젝트 SetItem이 이 에셋을 ItemManager.field에 주입
    2) ItemManager가 장착 즉시 this.Activate()를 StartCoroutine으로 실행
    3) Activate가 fieldPrefab을 NetworkServer.Spawn
    4) duration 경과 → 장판 Destroy → 코루틴 종료
       또는 ItemManager 타이머 만료 → OnDeactivate에서 장판 강제 정리

    실제 아이템을 만들 때:
    - 이 클래스를 상속하거나 그대로 사용
    - 인스펙터에서 fieldPrefab(NetworkServer.Spawn 가능한 NetworkIdentity 가진 프리팹) 지정
    - 장판 프리팹 자체에 FieldEffectBase 상속 컴포넌트를 붙여 효과 정의
*/
[CreateAssetMenu(menuName = "Item/Field/Templete")]
public class FieldItem_Field : ScriptableObject, IField
{
    [Header("아이템 설정")]
    [SerializeField] private float duration = 10f;

    [Header("장판 프리팹 (NetworkIdentity 필수)")]
    [Tooltip("Mirror 등록된 스폰 가능 프리팹. FieldEffectBase 상속 컴포넌트가 효과를 담당.")]
    [SerializeField] private GameObject fieldPrefab;

    [Header("스폰 위치 설정")]
    [Tooltip("월드 좌표 고정 스폰 위치. 자식 클래스에서 GetSpawnPosition()을 오버라이드해 동적으로 결정 가능.")]
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    public float AvailableTime => duration;

    // 현재 활성화된 장판 인스턴스 (OnDeactivate에서 회수용)
    // 같은 SO 에셋이 동시에 여러 번 발동될 가능성은 낮지만, 그래도 단일 참조로 관리.
    private GameObject _spawnedField;

    public virtual IEnumerator Activate()
    {
        Debug.Log($"[FieldItem] {name} 장판 스폰 시작");

        if (fieldPrefab == null)
        {
            Debug.LogError($"[FieldItem] {name}: fieldPrefab이 비어있음. 인스펙터 확인 필요.");
            yield break;
        }

        // 장판 스폰 (서버 권위)
        Vector3 pos = GetSpawnPosition();
        _spawnedField = Instantiate(fieldPrefab, pos, Quaternion.identity);

        // NetworkIdentity 있으면 서버 스폰. 없으면 일반 인스턴스 (싱글플레이 폴백).
        if (_spawnedField.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
        {
            Debug.Log("스폰!");
            NetworkServer.Spawn(_spawnedField);
        }

            // 장판이 자기 수명을 알 수 있도록 duration 알려주기 (선택적)
            FieldEffect effect = _spawnedField.GetComponent<FieldEffect>();
        if (effect != null) effect.Initialize(duration);

        // duration 동안 살아있음
        yield return new WaitForSeconds(duration);

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

    //위치를 동적으로 정하여 아이템 오브젝트를 스폰화려면 해당 항목을 오버라이드.
    protected virtual Vector3 GetSpawnPosition() => spawnPosition;

    private void DespawnField()
    {
        if (_spawnedField == null) return;

        if (_spawnedField.GetComponent<NetworkIdentity>() != null && NetworkServer.active)
            NetworkServer.Destroy(_spawnedField);
        else
            Destroy(_spawnedField);

        _spawnedField = null;
    }
}