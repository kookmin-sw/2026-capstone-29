using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TempGameManager : MonoBehaviour
{

    [SerializeField] public GameObject itemPrefab;

    private void Update()
    {
        if (Input.GetKeyDown("o"))
        {
            SpawnItem(Vector3.zero);
        }
    }


    [Server]
    public void SpawnItem(Vector3 pos)
    {
        var item = Instantiate(itemPrefab, pos, Quaternion.identity);
        NetworkServer.Spawn(item);
    }

}
