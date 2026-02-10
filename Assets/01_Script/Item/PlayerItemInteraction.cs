using Mirror;
using UnityEngine;

public class PlayerItemInteraction : NetworkBehaviour // 플레이어가 아이템 등 구조물과 상호작용을 하는 경우 적용되는 스크립트. 플레이어에게 해당 스크립트를 부착하면 아이템을 먹을 수 있게 된다.
{
    GameObject player;

    [SyncVar] public GameObject equipmentObj; 
    
    public IEquipment equipment;
    [SerializeField] private float equipmentLimitTime = 10f;
    private float limitTimer = 0f;

    private KeyCode itemKey = KeyCode.C;

    void Update()
    {
        if (!isLocalPlayer) return;

        if (equipmentObj != null)
        {
            // equipment 참조가 없으면 다시 가져오기 (SyncVar 동기화 후)
            if (equipment == null)
                equipment = equipmentObj.GetComponent<IEquipment>();

            limitTimer += Time.deltaTime;
            if (limitTimer > equipmentLimitTime)
            {
                limitTimer = 0f;
                CmdRemoveEquipment();
                return;
            }

            if (Input.GetKeyDown(itemKey))
            {
                Debug.Log("아이템 사용.");
                equipment.Effect();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isLocalPlayer) return;

        if (other.CompareTag("Item"))
        {
            Debug.Log("Item");
            CmdPickupItem(other.gameObject);

            return;
        }

        if (other.CompareTag("Heal"))
        {
            Debug.Log("힐팩");
            CmdDestroyObject(other.gameObject);
            return;
        }

        if (other.CompareTag("Interaction"))
        {
            Debug.Log("구조물");
            return;
        }
    }

    [Command]
    void CmdPickupItem(GameObject itemObj)
    {
        if (itemObj == null) return;

        IFieldItem fieldItem = itemObj.GetComponent<IFieldItem>();
        if (fieldItem == null) return;

        GameObject prefab = fieldItem.GetEquipmentPrefab();
        if (prefab == null) return;

        // 기존 장비 제거
        if (equipmentObj != null)
            NetworkServer.Destroy(equipmentObj);

        // 장비 생성 → 플레이어 자식으로 설정
        GameObject equip = Instantiate(prefab, transform.position, transform.rotation);
        equip.transform.SetParent(transform);
        equip.transform.localPosition = Vector3.zero;
        equip.transform.localRotation = Quaternion.identity;

        NetworkServer.Spawn(equip);

        // SyncVar로 동기화
        equipmentObj = equip;
        limitTimer = 0f;

        // 모든 클라이언트에서 부모 설정 동기화
        RpcAttachEquipment(equip);

        // 필드 아이템 제거
        NetworkServer.Destroy(itemObj);
    }

    [ClientRpc]
    void RpcAttachEquipment(GameObject equip)
    {
        if (equip == null) return;

        equip.transform.SetParent(transform);
        equip.transform.localPosition = Vector3.zero;
        equip.transform.localRotation = Quaternion.identity;

        if (isLocalPlayer)
        {
            equipment = equip.GetComponent<IEquipment>();
            limitTimer = 0f;
        }
    }

    [Command]
    void CmdRemoveEquipment()
    {
        if (equipmentObj != null)
            NetworkServer.Destroy(equipmentObj);

        equipmentObj = null;
    }

    [Command]
    void CmdDestroyObject(GameObject obj)
    {
        if (obj != null)
            NetworkServer.Destroy(obj);
    }
}
