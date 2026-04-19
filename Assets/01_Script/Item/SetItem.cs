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
            NetworkServer.Spawn(weaponObj, user.GetComponent<NetworkIdentity>().connectionToClient);
            IPlayerWeapon ipw = weaponObj.GetComponent<IPlayerWeapon>();
            ipw.SetUser(user);
            im.weaponAvailable = im.weapon.AvailableTime();

            // 모든 클라이언트에 장착 사실 알림
            RpcOnWeaponEquipped(user);
        }
        if (item.CompareTag("Active"))
        {
            IActive activeAsset = itemAsset as IActive;
            im.active = activeAsset;
            im.GetActive();
            im.activeAvailable = activeAsset.AvailableTime;  // 에셋에서 duration 읽어와 세팅
        }
        if (item.CompareTag("Passive"))
        {
            IPassive passiveAsset = itemAsset as IPassive;
            im.passive = passiveAsset;
            im.passiveAvailable = passiveAsset.AvailableTime; // 지속 시간 세팅
            im.GetPassive();                                  // 플래그는 마지막에 세팅 (Update에서 자동 발동 트리거)
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
        Debug.Log("아이템 장착 성공!");
    }
}