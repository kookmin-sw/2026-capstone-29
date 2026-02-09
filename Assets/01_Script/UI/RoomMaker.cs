using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomMaker : MonoBehaviour
{
    [SerializeField]
    private GameObject roomList; // 룸 리스트
    [SerializeField]
    private GameObject theRoom; // 룸 프리팹
    [SerializeField]
    private InputField roomNameInput;
    private GameObject currentRoom;

    private void Start()
    {
        theRoom.GetComponentInChildren<Text>().text = "방 이름";
    }

    public void RoomMake()
    {
        RoomName();
        currentRoom = Instantiate(theRoom, roomList.transform);
        Button enterBt = currentRoom.GetComponent<Button>();
        enterBt.onClick.AddListener(() => SceneManager.LoadScene("CharSelect"));
        enterBt.gameObject.SetActive(true);
    }
    public void RoomName()
    {
        theRoom.GetComponentInChildren<Text>().text = roomNameInput.text;
        roomNameInput.text = "";
    }
}
