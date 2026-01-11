using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharDataContainer : MonoBehaviour
{
    public static CharDataContainer Instance; // 어디서든 접근 가능하게 만듦

    // 선택된 캐릭터의 프리팹을 저장할 변수
    public GameObject p1SelectedPrefab;
    public GameObject p2SelectedPrefab;

    private void Awake()
    {
        // 싱글톤 패턴: 이 오브젝트는 단 하나만 존재해야 함
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀔 때 파괴되지 않음!
        }
        else
        {
            Destroy(gameObject); // 중복 생성되면 삭제
        }
    }
}