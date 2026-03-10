using UnityEngine;

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

public interface IWeapon // 근접 무기
{
    public void Attack();
}



public interface IActive // 액티브 아이템
{
}

public interface IPassive // 패시브 아이템
{    public void Apply();
}

public interface IField // 필드 아이템
{

}

