using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TempGameManager : MonoBehaviour
{

    [SerializeField] public GameObject itemPrefab;

    private void Awake()
    {
    }

    private void Update()
    {
        if (Input.GetKeyDown("o"))
        {
            SpawnItem(Vector3.zero);
        }
        /*
        if (Input.GetKeyDown("c"))
        {
            ChangeScene();
        }
        */
    }


    [Server]
    public void SpawnItem(Vector3 pos)
    {
        var item = Instantiate(itemPrefab, pos, Quaternion.identity);
        NetworkServer.Spawn(item);
    }

    public void ChangeScene()
    {
        if (SceneManager.GetActiveScene().name == "MirrorTest")
        {
            NetworkManager.singleton.ServerChangeScene("MirrorTestNextScene");

        }
        else if (SceneManager.GetActiveScene().name == "MirrorTestNextScene")
        {
            NetworkManager.singleton.ServerChangeScene("MirrorTest");

        }
    }

}
