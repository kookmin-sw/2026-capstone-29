using UnityEngine;


//각 아이템에 부착되어있어야 하는 스크립트. ItemPickUp에서 아이템을 감지하여 SetItem을 통해 플레이어에게 달려있는 ItemManager에 부착한다.
public class SetItem : MonoBehaviour, IEquip
{
    [SerializeField] ScriptableObject itemAsset;

    public void Save(GameObject user, GameObject item)
    {
        if (user != null && item != null)
        {
            ItemManager im = user.GetComponent<ItemManager>();

            if (item.CompareTag("Weapon"))
            {

                im.weapon = itemAsset as IWeapon;
                im.GetWeapon();
                im.weapon.SummonWeapon(user.transform.position, Quaternion.identity);
                im.weaponAvailable = im.weapon.AvailableTime();
                Debug.Log("아이템 장착!");
            }
            if (item.CompareTag("Active"))
            {
                im.active = itemAsset as IActive;
                im.GetActive();
                im.activeAvailable = im.active.AvailableTime();
            }
            if (item.CompareTag("Passive"))
            {
                im.passive = itemAsset as IPassive;
                im.GetPassive();
            }
            if (item.CompareTag("Field"))
            {
                // 플레이어에게 가는 것은 아니므로 다른 방식으로 가야함.
            }

        }
        else
        {
        }
    }
}