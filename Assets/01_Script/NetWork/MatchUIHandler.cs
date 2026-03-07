using Mirror;
using Mirror.Discovery;
using UnityEngine;

public class MatchUIHandler : MonoBehaviour
{

    public NetworkDiscovery networkDiscovery;
    
    [Header("UI Panels")]
    public GameObject matchingPanel; // "매칭 중..." 패널
    public GameObject successPanel;  // "매칭 성공!" 패널

    private void Start()
    {
        // 클라이언트에서 매칭 성공 메시지를 받으면 실행할 함수 등록
        if (!NetworkClient.active)
        {
            NetworkClient.RegisterHandler<MatchSuccessMessage>(OnMatchSuccess);
        }
    }

    private void OnDestroy()
    {
        // [중요] 오브젝트가 파괴될 때(씬 전환 등) 핸들러 등록을 반드시 해제해야 합니다.
        // 그렇지 않으면 다음 씬에서도 유령 핸들러가 작동하여 오류를 일으킵니다.
        NetworkClient.UnregisterHandler<MatchSuccessMessage>();
    }

    // [매치 메이킹] 버튼에 연결
    public void ClickMatchmaking()
    {

        if (NetworkServer.active || NetworkClient.active) return;
        CancelInvoke(nameof(StartAsHost)); // 기존 Invoke 예약 취소

        if (matchingPanel != null) matchingPanel.SetActive(true);
        if (successPanel != null) successPanel.SetActive(false);

#if UNITY_EDITOR
        // 에디터는 무조건 Host
        Debug.Log("에디터 Host 모드");
        StartAsHost();

#elif UNITY_WEBGL
        // WebGL 빌드: URL로 Host/Client 구분
        string url = Application.absoluteURL;
        if (url.Contains("host=true"))
        {
            Debug.Log("WebGL Host 모드");
            StartAsHost();
        }
        else
        {
            Debug.Log("WebGL Client 모드 - localhost로 접속 시도");
            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
        }

#else
        // Windows 빌드: Discovery로 자동 매칭
        networkDiscovery.StartDiscovery();
        float randomDelay = 3.0f + Random.Range(0f, 1.0f);
        Invoke(nameof(StartAsHost), randomDelay);

#endif
    }

#if !UNITY_WEBGL
    // LAN 검색으로 방을 찾았을 때 실행
    public void OnServerFound(ServerResponse response)
    {
        // [조건 추가] 이미 내가 호스트(서버)이거나 클라이언트로 접속 중이라면 무시합니다.
        if (NetworkServer.active || NetworkClient.active)
        {
            return;
        }

        #if UNITY_EDITOR
            return;
        #endif

        // 위 조건이 통과되어야만 아래 코드가 실행됩니다.
        Debug.Log("외부 서버를 발견했습니다. 접속을 시도합니다.");
        CancelInvoke(nameof(StartAsHost));
        networkDiscovery.StopDiscovery();
        NetworkManager.singleton.StartClient(response.uri);
    }
#endif
    void StartAsHost()
    {
        if (NetworkServer.active || NetworkClient.active) return;

    #if !UNITY_WEBGL
        networkDiscovery.StopDiscovery();
        // Host는 Multiplex로 둘 다 Listen
        var multiplex = NetworkManager.singleton.GetComponent<MultiplexTransport>();
        if (multiplex != null)
            Transport.active = multiplex;
        else
            Debug.LogError("MultiplexTransport를 찾을 수 없습니다!");
    #endif

        NetworkManager.singleton.StartHost();
        Debug.Log("HOST 시작됨");

    #if !UNITY_WEBGL
        networkDiscovery.AdvertiseServer();
    #endif
    }
    // 서버가 보낸 메시지를 받았을 때 UI 변경
    void OnMatchSuccess(MatchSuccessMessage msg)
    {
        // [핵심 해결책] 씬 전환 중 오브젝트가 파괴되었는지 반드시 확인합니다.
        if (matchingPanel == null || successPanel == null)
        {
            Debug.Log("씬이 전환되어 UI 오브젝트가 이미 파괴되었습니다. 핸들러를 중단합니다.");
            return;
        }

        matchingPanel.SetActive(false);
        successPanel.SetActive(true);
    }
}