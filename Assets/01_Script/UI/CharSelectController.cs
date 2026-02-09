using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterData
{
    public string charName;
    public Sprite charSprite;
    public GameObject characterPrefab; 
}

public class CharSelectController : MonoBehaviour
{
    [Header("설정")]
    public GameObject charGrid;
    public GameObject charSlotPrefab; // 슬롯 프리팹

    [Header("캐릭터 데이터 (테스트)")]
    public CharacterData[] characterList;

    private List<CharSlotPrefab> createdSlots = new List<CharSlotPrefab>();

    // 플레이어가 선택한 위치
    private int p1SelectedIndex = -1;
    private int p2SelectedIndex = -1;

    // 현재 누구 차례인지 (테스트용)
    public bool isPlayer1Turn = true;

    void Start()
    {
        GenerateSlots();
    }

    // 슬롯 생성
    void GenerateSlots()
    {
        // 기존 슬롯 제거
        foreach (Transform child in charGrid.transform)
        {
            Destroy(child.gameObject);
        }
        createdSlots.Clear();

        for (int i = 0; i < characterList.Length; i++)
        {
            GameObject go = Instantiate(charSlotPrefab, charGrid.transform);
            CharSlotPrefab slotScript = go.GetComponent<CharSlotPrefab>();
            slotScript.SetupSlot(i, characterList[i].charSprite, this);
            createdSlots.Add(slotScript);
        }
    }

    // 슬롯이 클릭되었을 때
    public void OnSlotClicked(int index)
    {
        if (isPlayer1Turn) p1SelectedIndex = index;
        else p2SelectedIndex = index;
        RefreshAllSlotsUI();
    }

    public void RefreshAllSlotsUI()
    {
        for (int i = 0; i < createdSlots.Count; i++)
        {
            bool isP1 = p1SelectedIndex == i;
            bool isP2 = p2SelectedIndex == i;

            createdSlots[i].PlayerChoice(isP1, isP2);
        }
    }
    public void OnGameStartBtnClick()
    {
        if (p1SelectedIndex == -1 || p2SelectedIndex == -1)
        {
            Debug.Log("캐릭터를 모두 선택해주세요!");
            return;
        }

        CharDataContainer.Instance.p1SelectedPrefab = characterList[p1SelectedIndex].characterPrefab;
        CharDataContainer.Instance.p2SelectedPrefab = characterList[p2SelectedIndex].characterPrefab;
    }
}
