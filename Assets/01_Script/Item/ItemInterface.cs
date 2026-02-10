using UnityEngine;

public interface IFieldItem
{
    GameObject GetEquipmentPrefab();
}

public interface IEquipment // 주워서 사용하는 아이템의 효과
{
    public void Effect();


}
