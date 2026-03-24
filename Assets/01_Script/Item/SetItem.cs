using UnityEngine;
using Mirror;


//각 아이템에 부착되어있어야 하는 스크립트. ItemPickUp에서 아이템을 감지하여 SetItem을 통해 플레이어에게 달려있는 ItemManager에 부착한다.
public class SetItem : NetworkBehaviour, IEquip
{
    [SerializeField] ScriptableObject itemAsset;

    public void Save(GameObject user, GameObject item)
    {
        if (user == null || item == null) return;

        // 클라이언트에서 서버로 요청
        CmdSave(user, item);
    }

    [Command(requiresAuthority = false)]
    void CmdSave(GameObject user, GameObject item)
    {
        ItemManager im = user.GetComponent<ItemManager>();
        if (im == null) return;

        if (item.CompareTag("Weapon"))
        {
            Debug.Log("무기 감지.");
            im.weapon = itemAsset as IWeapon;
            im.GetWeapon();
            GameObject weaponObj = im.weapon.SummonWeapon(user.transform.position, Quaternion.identity);
            NetworkServer.Spawn(weaponObj);
            im.weaponAvailable = im.weapon.AvailableTime();

            // 모든 클라이언트에 장착 사실 알림
            RpcOnWeaponEquipped(user);
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
            // 필드 아이템 처리
        }

        // 필드 아이템 오브젝트 제거
        NetworkServer.Destroy(item);
    }

    [ClientRpc]
    void RpcOnWeaponEquipped(GameObject user)
    {
        Debug.Log("아이템 장착!");
    }
}