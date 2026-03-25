using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.UI; 

public class MatchUIHandler : MonoBehaviour
{
    public NetworkDiscovery networkDiscovery;
    
    [Header("UI Panels")]
    public GameObject matchPanel; // 매칭 패넝

    public Text matchingText; // "매칭 중..." 텍스트
    public Text timeText; // 매칭 시간 텍스트
    public GameObject successText;  // "매칭 성공!" 텍스트
    public GameObject matchCancelButton; // 매칭 종료 버튼

    // 애니메이션 및 타이머를 위한 상태 변수
    private bool isMatching = false;
    private float matchTimer = 0f;
    private float dotTimer = 0f;
    private int dotCount = 0;
    private readonly string baseMatchingText = "매칭 중"; // 변하지 않는 문자

    private void RegisterMessageHandler()
    {
        NetworkClient.RegisterHandler<MatchSuccessMessage>(OnMatchSuccess);
    }

    private void OnDestroy()
    {
        NetworkClient.UnregisterHandler<MatchSuccessMessage>();
    }

    private void Update()
    {
        // 매칭 중일 때만 업데이트
        if(isMatching)
        {
            // 매칭 시간 계산 및 업데이트
            matchTimer += Time.deltaTime;
            int minutes = Mathf.FloorToInt(matchTimer / 60F);
            int seconds = Mathf.FloorToInt(matchTimer - minutes * 60);

            if (timeText != null)
            {
                timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }

            // ... 애니메이션 업데이트
            dotTimer += Time.deltaTime;
            if(dotTimer >= 0.5f)
            {
                dotTimer = 0f;
                dotCount = (dotCount + 1) % 4; // 0, 1, 2, 3 반복

                if(matchingText != null)
                {
                    string dots = new string('.', dotCount);
                    matchingText.text = baseMatchingText + dots;
                }
            }
        }
    }

    // [매치 메이킹] 버튼에 연결
    public void ClickMatchmaking()
    {
        if (matchPanel != null) matchPanel.SetActive(true);

        // 텍스트 오브젝트
        if (matchingText != null) matchingText.gameObject.SetActive(true);
        if (timeText != null) timeText.gameObject.SetActive(true);
        if (matchCancelButton != null) matchCancelButton.SetActive(true);
        if (successText != null) successText.SetActive(false);
        
        RegisterMessageHandler(); // 매칭버튼을 누를 때마다 핸들러 등록

        // 매칭 시 변수 초기화
        isMatching = true;
        matchTimer = 0f;
        dotTimer = 0f;
        dotCount = 0;

        networkDiscovery.StopDiscovery(); // 타이틀 복귀 후 이전 상태가 남아있으면 초기화 후 시작

        networkDiscovery.StartDiscovery();
        float randomDelay = 3.0f + Random.Range(0f, 1.0f);
        Invoke(nameof(StartAsHost), randomDelay);
    }

    // LAN 검색으로 방을 찾았을 때 실행
    public void OnServerFound(ServerResponse response)
    {
        // [조건 추가] 이미 내가 호스트(서버)이거나 클라이언트로 접속 중이라면 무시합니다.
        if (NetworkServer.active || NetworkClient.active) 
        {
            return; 
        }
        
        // 위 조건이 통과되어야만 아래 코드가 실행됩니다.
        Debug.Log("외부 서버를 발견했습니다. 접속을 시도합니다.");
        CancelInvoke(nameof(StartAsHost));
        networkDiscovery.StopDiscovery();
        NetworkManager.singleton.StartClient(response.uri);
    }

    void StartAsHost()
    {
        networkDiscovery.StopDiscovery();
        NetworkManager.singleton.StartHost(); // 호스트 시작 (Online Scene이 없어서 타이틀 유지)
        networkDiscovery.AdvertiseServer();   // 이제 내 방을 주변에 알림
    }

    // 서버가 보낸 메시지를 받았을 때 UI 변경 - 매칭 되었을 때
    void OnMatchSuccess(MatchSuccessMessage msg)
    {
        // [핵심 해결책] 씬 전환 중 오브젝트가 파괴되었는지 반드시 확인합니다.
        if (matchingText == null || successText == null)
        {
            Debug.Log("씬이 전환되어 UI 오브젝트가 이미 파괴되었습니다. 핸들러를 중단합니다.");
            return;
        }   

        isMatching = false; // 매칭 성공 시 정지

        // matchPanel.SetActive(true);
        matchingText.gameObject.SetActive(false);
        timeText.gameObject.SetActive(false);
        successText.SetActive(true);
        matchCancelButton.SetActive(false);
    }

    // 매칭 종료 버튼 클릭시 - 매칭 UI 비활성화 및 네트워크 탐색 중지
    public void CancleMatchmaking()
    {
        // 호스트 예약 취소 
        CancelInvoke(nameof(StartAsHost));

        // 네트워크 탐색 중지
        networkDiscovery.StopDiscovery();

        // 호스트가 되었거나 연결 중일 때 중지
        if(NetworkServer.active || NetworkClient.active)
        {
            NetworkManager.singleton.StopHost();
        }

        // UI 닫기
        isMatching = false;
        if(matchPanel != null) matchPanel.SetActive(false);
        if(matchingText != null) matchingText.gameObject.SetActive(false);
        if(timeText != null) timeText.gameObject.SetActive(false);
        if(matchCancelButton != null) matchCancelButton.SetActive(false);
    }
}