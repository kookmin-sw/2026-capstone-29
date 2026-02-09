using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharSlotPrefab : MonoBehaviour
{
    [Header("캐릭터 이미지")]
    public Image charImg;
    public Button slotButton;

    [Header("플레이어가 캐릭터를 선택했을 때 이미지")]
    public GameObject choiceP1;
    public GameObject choiceP2;
    public GameObject choiceAll;


    private int myIndex;
    private CharSelectController controller;

    public void ChangeChar(Sprite charSprite)
    {
        charImg.sprite = charSprite;
    }
    // 슬롯 초기화 
    public void SetupSlot(int index, Sprite sprite, CharSelectController ctrl)
    {
        myIndex = index;
        charImg.sprite = sprite;
        controller = ctrl;

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => controller.OnSlotClicked(myIndex));

        PlayerChoice(false, false);
    }

    // 선택 판정
    public void PlayerChoice(bool isP1, bool isP2)
    {
        TurnOff();
        if (isP1 && isP2)
        {
            choiceAll.SetActive(true);
        }
        else if (isP1)
        {
            choiceP1.SetActive(true);
        }
        else if (isP2)
        {
            choiceP2.SetActive(true);
        }
    }
    
    private void TurnOff()
    {
        choiceP1.SetActive(false);
        choiceP2.SetActive(false);
        choiceAll.SetActive(false);
    }
}
