using UnityEngine;
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
{

    public void Effect();
    public float AvailableTime();
}

public interface IPassive // 패시브 아이템
{    public void Apply();
}

public interface IField // 필드 아이템
{

}

