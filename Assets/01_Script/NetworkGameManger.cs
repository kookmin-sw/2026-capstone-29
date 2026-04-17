using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class NetworkGameManger : NetworkBehaviour
{
    public static NetworkGameManger instance; 
    public static event Action OnGameOverEvent; // 게임 오버시 발생하는 이벤트
    private InGameUIManger uiManager;

    [Header("UI 연결")]
    public Text timerText; // 중앙 타이머 텍스트
    public Image p1HealthBar; // 줄어들 체력바
    public Image p2HealthBar; 
    public GameObject gameOverPanel; // 게임 오버 패널
    public Text gameOverText; // 게임 종료 텍스트
    public Button TitleButton; // 타이틀로 돌아가는 버튼

    [Header("Scene Setting")]
    public string titleSceneName = "TitleScene";

    [Header("Game Settting")]
    [SyncVar(hook = nameof(OnTimerChanged))] // 시간 변경시 변경 함수 호출
    public int remainingTime = 300; // 300초 타이머 
    
    // 스폰 포인트 세팅
    [Header("Spawn Setting")]
    public Transform[] spawnPoints; // 여러 스폰포인트이기에 배열형태

    // 게임 종료시 
    [SyncVar]
    private bool isGameOver = false;

    [SyncVar(hook = nameof(OnWinnerIndexChanged))]
    private int gameOverWinnerIndex = -1; // 1 = P1 승, 2 = P2 승, 0 = 무승부

    private NetworkCharacterModel player1;
    private NetworkCharacterModel player2;

    public bool _isLeavingVoluntarily = false; // 클라이언트 자발적 종료시 UI 등장 방지

    private void Start()
    {
        uiManager = FindObjectOfType<InGameUIManger>(); // ui 매니저 찾아서 등록
    }

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
        while(remainingTime > 0 && !isGameOver)
        {
            yield return new WaitForSeconds(1f);
            remainingTime--;
        }
        
        // 시간 초과시 목숨, 체력으로 승자 결정
        if(!isGameOver)
        {
            DetermineWinner();
        }
    }

    // 시간초과시 승부 결정 로직 - 임시로 체력만
    [Server]
    private void DetermineWinner()
    {
        if (player1 == null || player2 == null)
        {
            TriggerGameOver(0); // 무승부
            return;
        }

        if (player1.remaingLives > player2.remaingLives)
            TriggerGameOver(1);
        else if (player2.remaingLives > player1.remaingLives)
            TriggerGameOver(2);
        else if(player1.currentHealth > player2.currentHealth)
            TriggerGameOver(1);
        else if(player2.currentHealth > player1.currentHealth)
            TriggerGameOver(2);
        else
            TriggerGameOver(0); // 동점 무승부
    }

    // NetworkCharacterModel에서 호출됨
    public void OnPlayerGameOver(NetworkCharacterModel deadPlayer)
    {
        if (!isServer || isGameOver) return;
 
        if (deadPlayer == player1)
            TriggerGameOver(2); // P1이 졌으니 P2 승
        else if (deadPlayer == player2)
            TriggerGameOver(1); // P2가 졌으니 P1 승
    }

    // 게임 종료 트리거 
    [Server]
    private void TriggerGameOver(int winnerIndex)
    {
        isGameOver = true;
        gameOverWinnerIndex = winnerIndex;
        Debug.Log($"Game Over! Winner Index: {winnerIndex}");
    }

    // WinnerIndex hook
    void OnWinnerIndexChanged(int oldV, int newV)
    {
        if(isGameOver)
            ShowGameOverUI();
    }

    // 게임 종료시 UI 등장
    private void ShowGameOverUI()
    {
        // 마우스 다시 컨트롤 할 수 있도록
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 카메라, 입력 잠금
        OnGameOverEvent?.Invoke();
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
 
        if (gameOverText != null)
        {
            switch (gameOverWinnerIndex)
            {
                case 1: gameOverText.text = "Player 1  Win!"; break;
                case 2: gameOverText.text = "Player 2  Win!"; break;
                default: gameOverText.text = "Draw!"; break;
            }
        }

    }

    // 타이틀로 돌아가기 버튼 클릭
    public void OnReturnToTitleClicked()
    {
        // 호스트/서버이면 모든 클라이언트를 끊고 서버도 종료
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            // 호스트 모드: 모든 클라이언트에게 씬 이동 명령 후 서버 종료
            StartCoroutine(StopHostAndLoadTitle());
        }
        else if (NetworkClient.isConnected)
        {
            // 클라이언트가 먼저 연결 해제
            _isLeavingVoluntarily = true;
            StartCoroutine(StopClientAndLoadTitle());
        }
        else
        {
            // 이미 연결이 끊긴 클라이언트가 버튼 누를 때 - 오브젝트가 비활성이므로 코루틴 x
            LoadTitleDirectly();
        }
    }
    
    // 호스트가 타이틀 버튼을 누른 경우 - 신 매니저 파괴 후 타이틀 화면으로 이동
    private IEnumerator StopHostAndLoadTitle()
    {
        yield return new WaitForSeconds(0.5f); // 클라이언트 RPC 전달 대기
        if (NetworkManager.singleton is MatchManager matchManager)
        {
            matchManager.ReturnToTitle();
        }
        else
        {
            NetworkManager.singleton.StopHost();
            SceneManager.LoadScene(titleSceneName);
        }
    }

    // 클라이언트가 먼저 타이틀 버튼을 누른 경우 - 타이틀화면 변경동안 기존 매니저 제거
    private IEnumerator StopClientAndLoadTitle()
    {
        yield return new WaitForSeconds(0.5f);
        NetworkManager.singleton.StopClient();
        Destroy(NetworkManager.singleton.gameObject); // 신 파괴하고 한 프레임 대기하고 신 전환
        yield return null;
        SceneManager.LoadScene(titleSceneName);
    }

    // 연결이 끊킨 클라이언트가 타이틀로 돌아가는 경우
    private void LoadTitleDirectly()
    {
        if(NetworkManager.singleton != null)
        {
            DestroyImmediate(NetworkManager.singleton.gameObject); // 프레임 대기 없이 즉시 파괴
        }

        SceneManager.LoadScene(titleSceneName);
    }

    // 서버가 끊킨 클라이언트 경우 UI가 나타나도록
    public void ForceShowDisconnectUI()
    {
        if (_isLeavingVoluntarily) return; // 자발적 종료시 UI 뜨지 않도록

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnGameOverEvent?.Invoke(); // 입력 차단

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = "호스트와 연결이 끊켰습니다.";
    }   

    // 캐릭터 스폰시 UI와 연결 - NetworkCharacterModel 내부의 체력
    public void RegisterPlayer(NetworkCharacterModel model)
    {
        // uiManager 재탐색 과정
        if(uiManager == null) uiManager = FindObjectOfType<InGameUIManger>();

        // 플레이어 등록
        if(player1 == null)
        {
            player1 = model;
            uiManager?.RegisterHealthBar(1, model.currentHealth); // ui 매니저를 통해 체력바 등록
            player1.OnHealthChanged += uiManager.OnP1HealthChanged; // 체력바 변경 관리
        }
        else if(player2 == null)
        {
            player2 = model;
            uiManager?.RegisterHealthBar(2, model.currentHealth);
            player2.OnHealthChanged += uiManager.OnP2HealthChanged;
        }
    }

    // 타이머 텍스트 변경 함수
    void OnTimerChanged(int oldV, int newV)
    {
        if(timerText != null)
            timerText.text = newV.ToString();
    }

    // 플레이어 리스폰(서버에서)
    [Server]
    public void RespawnPlayer(NetworkCharacterModel character)
    {
        // 스폰포인트 미설정시
        if(spawnPoints == null || spawnPoints.Length == 0) return;
        
        // 플레이어 1은 0번, 플레이어 2는 1번 스폰위치에
        int spawnIndex = 0;
        if(character == player1 && spawnPoints.Length > 1)
            spawnIndex =1;
        
        Transform spawnPoint = spawnPoints[spawnIndex];

        // 모든 클라이언트에서 위치 이동
        RpcTeleportPlayer(character.netIdentity, spawnPoint.position, spawnPoint.rotation);
    }

    // 모든 클라이언트에서 위치 이동 - 리스폰
    [ClientRpc]
    private void RpcTeleportPlayer(NetworkIdentity targetIdentity, Vector3 position, Quaternion rotation)
    {
        if(targetIdentity == null) return;
        
        // 잠시 컨트롤러 비활성화 후 이동
        CharacterController cc = targetIdentity.GetComponent<CharacterController>();
        if(cc != null) cc.enabled = false;

        // 이동
        targetIdentity.transform.position = position;
        targetIdentity.transform.rotation = rotation;

        // 컨트롤러 다시 활성화
        if(cc != null) cc.enabled = true;
    }
}
