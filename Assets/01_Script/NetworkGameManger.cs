using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cinemachine;

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

    // ICharacterModel 기반으로 변경. NetworkCharacterModel / UnifiedCharacterModel 모두 허용.
    private ICharacterModel player1;
    private ICharacterModel player2;

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

        if (player1.RemainingLives > player2.RemainingLives)
            TriggerGameOver(1);
        else if (player2.RemainingLives > player1.RemainingLives)
            TriggerGameOver(2);
        else if(player1.CurrentHealth > player2.CurrentHealth)
            TriggerGameOver(1);
        else if(player2.CurrentHealth > player1.CurrentHealth)
            TriggerGameOver(2);
        else
            TriggerGameOver(0); // 동점 무승부
    }

    // NetworkCharacterModel / UnifiedCharacterModel 에서 호출됨
    public void OnPlayerGameOver(ICharacterModel deadPlayer)
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

        // 슬로우 모션 연출
        ICharacterModel winner = winnerIndex == 1 ? player1 : (winnerIndex == 2 ? player2 : null);
        ICharacterModel loser  = winnerIndex == 1 ? player2 : (winnerIndex == 2 ? player1 : null);

        NetworkBehaviour winnerNB = winner as NetworkBehaviour;
        NetworkBehaviour loserNB  = loser  as NetworkBehaviour;

        if (winnerNB != null)
        {
            // 슬로우모션 + 승리연출 RPC (서버에서 한 번만 호출)
            RpcStartSlowMotionAndVictory(winnerNB.netIdentity, loserNB != null ? loserNB.netIdentity : null);
        }
    }

    // WinnerIndex hook
    void OnWinnerIndexChanged(int oldV, int newV)
    {
        // RpcStartSlowMotionAndVictory에서 처리하게됨
    }

    // 슬로우 모션 -> 승리 모션 전체 연출
    [ClientRpc]
    private void RpcStartSlowMotionAndVictory(NetworkIdentity winnerIdentity, NetworkIdentity loserIdentity)
    {
        StartCoroutine(SlowMotionVictorySequence(winnerIdentity, loserIdentity));
    }

    // 게임 종료 시 승리모션 코루틴
    private IEnumerator SlowMotionVictorySequence(NetworkIdentity winnerIdentity, NetworkIdentity loserIdentity)
    {
        // 플레이어와 애니메이터 탐색
        var winnerModel = winnerIdentity?.GetComponent<UnifiedCharacterModel>();
        var winnerAnim  = winnerIdentity?.GetComponent<Animator>();

        // 1단계: 슬로우모션 시작 - 애니메이터 속도 변경
        float slowSpeed = 0.15f;
        if (winnerAnim != null) winnerAnim.speed = slowSpeed;

        // 카메라: 패자 줌인
        Transform slowTarget = loserIdentity != null ? loserIdentity.transform : winnerIdentity?.transform;
        if (slowTarget != null)
            StartCoroutine(SlowMotionCamera(slowTarget));

        // 슬로우모션 감상 시간 (realtime 기준)
        yield return new WaitForSecondsRealtime(3.0f);

        // 2단계: Animator 속도 복구 + 승리/패배 모션 
        if (winnerAnim != null) winnerAnim.speed = 1f;

        winnerModel?.TriggerVictory();

        // 카메라: 승자 정면으로 전환
        if (winnerIdentity != null)
            yield return StartCoroutine(MoveCameraToWinner(winnerIdentity.transform));
        
        // 애니메이션 재생 대기
        yield return new WaitForSeconds(3.5f);

        // 게임 종료 UI 표시
        ShowGameOverUI();
    }   

    // 슬로우 모션 연출 카메라 - 패자 줌인
    private IEnumerator SlowMotionCamera(Transform target)
    {
        var vcam = UnityEngine.Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam == null) yield break;

        // 패자 쪽으로 가까이 줌인 (1.0m 앞, 눈높이)
        if (victoryAnchor != null) Destroy(victoryAnchor);
        victoryAnchor = new GameObject("SlowMoAnchor");
        victoryAnchor.transform.position = target.position
                                        + target.forward * 1.0f
                                        + Vector3.up * 1.6f;
        victoryAnchor.transform.LookAt(target.position + Vector3.up * 1.4f);

        vcam.Follow = victoryAnchor.transform;
        vcam.LookAt = target.Find("PlayerCameraRoot") ?? target;

        yield return new WaitForSecondsRealtime(2.0f);
    }

    // 게임 종료시 승자 카메라 이동 - 로컬에서만
    private GameObject victoryAnchor; // 나중에 Destroy용

    private IEnumerator MoveCameraToWinner(Transform winner)
    {
        var vcam = UnityEngine.Object.FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam == null) yield break;

        // 카메라 위치/회전 계산
        Vector3 targetPos = winner.position + winner.forward * 1.6f + Vector3.up * 1.5f;
        Vector3 lookTarget = winner.position + Vector3.up * 1.4f;

        // LookAt 타겟 설정 - 승자 정면 위치로
        Transform camRoot = winner.Find("PlayerCmeraroot") ?? winner;
        vcam.Follow = victoryAnchor.transform;
        vcam.LookAt = camRoot;

        // 카메라 부드럽게 이동
        float duration = 1.5f;
        float elapsed = 0f;

        Vector3 startPos = victoryAnchor.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // EaseInOut 커브 적용 (자연스러운 가감속)
            float smoothT = t * t * (3f - 2f * t);

            victoryAnchor.transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            victoryAnchor.transform.LookAt(lookTarget);

            yield return null;
        }

        victoryAnchor.transform.position = targetPos;
    }

    // 게임 종료시 UI 등장
    private void ShowGameOverUI()
    {
        Debug.Log($"[ShowGameOverUI] 실행됨. isClient={isClient}, isServer={isServer}");
    
        if (victoryAnchor != null)
        {
            Destroy(victoryAnchor);
            victoryAnchor = null;
        }

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

    // 캐릭터 스폰시 UI와 연결 - ICharacterModel의 체력 이벤트 사용
    public void RegisterPlayer(ICharacterModel model)
    {
        // uiManager 재탐색 과정
        if(uiManager == null) uiManager = FindObjectOfType<InGameUIManger>();

        // 플레이어 등록
        if(player1 == null)
        {
            player1 = model;
            uiManager?.RegisterHealthBar(1, model.CurrentHealth); // ui 매니저를 통해 체력바 등록
            uiManager?.RegisterLives(1, (model as UnifiedCharacterModel)?.maxLives ?? 1, model.RemainingLives);

            player1.OnHealthChanged += uiManager.OnP1HealthChanged; // 체력바 변경 관리
            player1.OnLivesChanged += uiManager.OnP1LivesChanged; // 목숨 변경 이벤트 구독
        }
        else if(player2 == null)
        {
            player2 = model;
            uiManager?.RegisterHealthBar(2, model.CurrentHealth);
            uiManager?.RegisterLives(2, (model as UnifiedCharacterModel)?.maxLives ?? 1, model.RemainingLives);

            player2.OnHealthChanged += uiManager.OnP2HealthChanged;
            player2.OnLivesChanged += uiManager.OnP2LivesChanged; // 목숨 변경 이벤트 구독
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
    public void RespawnPlayer(ICharacterModel character)
    {
        // 스폰포인트 미설정시
        if(spawnPoints == null || spawnPoints.Length == 0) return;

        // ICharacterModel에서 NetworkIdentity 추출 (구현체는 NetworkBehaviour 기반).
        NetworkBehaviour nb = character as NetworkBehaviour;
        if(nb == null) return;

        // 플레이어 1은 0번, 플레이어 2는 1번 스폰위치에
        int spawnIndex = 0;
        if(character == player1 && spawnPoints.Length > 1)
            spawnIndex = 1;

        Transform spawnPoint = spawnPoints[spawnIndex];

        RpcResetAnimatorState(nb.netIdentity);

        // 모든 클라이언트에서 위치 이동
        RpcTeleportPlayer(nb.netIdentity, spawnPoint.position, spawnPoint.rotation);
    }

    [ClientRpc]
    void RpcResetAnimatorState(NetworkIdentity targetIdentity)
    {
        if(targetIdentity == null) return;
        Animator animator = targetIdentity.GetComponentInChildren<Animator>();
        if(animator == null) return;

        // 필요한 다른 트리거가 있다면 함께 리셋
        animator.Rebind();

        // 강제로 Idle 상태로 복귀시키고 싶다면
        animator.Play("Movement", 0, 0f);
        animator.Update(0f);
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
