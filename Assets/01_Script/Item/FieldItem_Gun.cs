using Mirror;
using UnityEngine;

public class FieldItem_Gun : NetworkBehaviour, IFieldItem
{
    [SerializeField] private GameObject gunPrefab;

    public GameObject GetEquipmentPrefab()
    {
        return gunPrefab;
    }
}