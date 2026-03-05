using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NetworkGameManger : NetworkBehaviour
{
    public static NetworkGameManger instance; 

    [Header("UI 연결")]
    public Text timerText; // 중앙 타이머 텍스트
    public RectTransform p1HealthGroup; // 플레이어 1 체력그룹
    public RectTransform p1Health; // 줄어들 체력바
    public RectTransform p2HealthGroup;
    public RectTransform p2Health; 
    
    [Header("Game Settting")]
    [SyncVar(hook = nameof(OnTimerChanged))] // 시간 변경시 변경 함수 호출
    public int remainingTime = 300; // 300초 타이머 

    private NetworkCharacterModel player1;
    private NetworkCharacterModel player2;

    private void Awake()
    {
        if(instance == null)
            instance = this; // 게임 매니저 연결
    }

    public override void OnStartServer()
    {
        // 서버에서 타이머 시작
        StartCoroutine(TimerCoroutine());
    }

    [Server]
    private IEnumerator TimerCoroutine()
    {
        while(remainingTime > 0)
        {
            yield return new WaitForSeconds(1f);
            remainingTime--;
        }

        // 0초되면 게임 종료 로직 실행하기(임시)
        Debug.Log("Game Over : Time Up!");
    }

    // 타이머 텍스트 변경 함수
    void OnTimerChanged(int oldV, int newV)
    {
        if(timerText != null)
            timerText.text = newV.ToString();
    }

}
