using UnityEngine;

//플레이어에게 부착되어 트리거를 통해 아이템을 감지하는 스크립트. SetItem에게 넘겨준다.
public class ItemPickUp : MonoBehaviour
{
    [SerializeField] GameObject player;
    ItemManager IM;
    

    [SerializeField] private ScriptableObject WeaponAsset;
    [SerializeField] private ScriptableObject ActiveAsset;
    [SerializeField] private ScriptableObject PassiveAsset;

    private IWeapon weapon;
    private IActive active;
    private IPassive passive;

    
    private void Start()
    {
        IM = player.GetComponent<ItemManager>();

    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject item = other.gameObject;

        if (item.GetComponent<IEquip>() != null)
        {
            item.GetComponent<IEquip>().Save(player, item);
            Destroy(item);
        }
    }

}