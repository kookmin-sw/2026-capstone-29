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
    public Image p1HealthBar; // 줄어들 체력바
    public Image p2HealthBar; 
    
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

    // 캐릭터 스폰시 UI와 연결 - NetworkCharacterModel 내부의 체력
    public void RegisterPlayer(NetworkCharacterModel model)
    {
        if(player1 == null)
        {
            player1 = model;
            player1.OnHealthChanged += (health) => p1HealthBar.fillAmount = health / 100f; // 이벤트 연결
            p1HealthBar.fillAmount = player1.currentHealth / 100f; // 초기값 설정
        }
        else if(player2 == null)
        {
            player2 = model;
            player2.OnHealthChanged += (health) => p2HealthBar.fillAmount = health / 100f; // 이벤트 연결
            p2HealthBar.fillAmount = player2.currentHealth / 100f; // 초기값 설정
        }
    }

    // 타이머 텍스트 변경 함수
    void OnTimerChanged(int oldV, int newV)
    {
        if(timerText != null)
            timerText.text = newV.ToString();
    }

}
