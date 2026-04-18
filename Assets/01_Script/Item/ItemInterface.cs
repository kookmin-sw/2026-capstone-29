using UnityEngine;
using System.Collections;
using UnityEngine.UIElements;

public interface IFieldItem
{
    GameObject GetEquipmentPrefab();
}

public interface IEquip // 주워서 사용하는 아이템의 효과
{
    public void Save(GameObject player, GameObject item);
}

public interface IEquipment // legacy
{
    public void Effect();
}

public interface IWeapon //
{
    public GameObject SummonWeapon(Vector3 pos, Quaternion qt);
    public float AvailableTime();
}

public interface IPlayerWeapon
{
    public void SetUser(GameObject user);
    public void ThrowWeapon();
}

public interface IWeaponHitBox
{
    public void SetOwner(GameObject user);
    public GameObject GetOwner();
}



public interface IActive // 액티브 아이템
{  //아이템의 유효 시간. ItemManager 타이머가 이 값을 사용
    float AvailableTime { get; }
    
    //액티브 효과 본체. ItemManager가 MonoBehaviour의 StartCoroutine으로 실행. 이 코루틴이 끝나거나 중간에 중단되면 효과 종료.
    IEnumerator Activate(GameObject owner);

    // 효과가 중도 해제될 때 호출되는 정리 로직(버프 원복 등). Activate 코루틴이 끝까지 흘러서 자연 종료된 경우에도 호출하는 것을 권장.
    void OnDeactivate(GameObject owner);
}

public interface IPassive // 패시브 아이템
{    public void Apply();
}

public interface IField // 필드 아이템
{

}

