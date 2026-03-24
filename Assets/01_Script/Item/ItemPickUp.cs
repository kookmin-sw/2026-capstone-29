using Mirror;
using UnityEngine;

//플레이어에게 부착되어 트리거를 통해 아이템을 감지하는 스크립트. SetItem에게 넘겨준다.
public class ItemPickUp : NetworkBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("트리거 이상 없음");
        // 로컬 플레이어만 아이템 감지
        if (!isLocalPlayer) return;

        GameObject item = other.gameObject;
        IEquip equip = item.GetComponent<IEquip>();

        if (equip != null)
        {
            // 서버에 아이템 획득 요청
            CmdPickUp(item);
        }
    }

    [Command]
    void CmdPickUp(GameObject item)
    {
        if (item == null) return;

        IEquip equip = item.GetComponent<IEquip>();
        if (equip == null) return;

        // 서버에서 아이템 장착 처리
        equip.Save(gameObject, item);

    }
}