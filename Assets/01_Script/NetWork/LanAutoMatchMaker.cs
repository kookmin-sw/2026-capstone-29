using Mirror;
using UnityEngine;

public class LanAutoMatchmaker : MonoBehaviour
{
    public NetworkManager networkManager;
    public LanDiscovery discovery;
    public float searchTimeout = 3f;

    private float searchTimer;
    private bool isSearching;
    private bool matched;

    
    void OnEnable()
    {
        // 클라이언트가 서버에 연결되면 자동 Ready
        NetworkClient.RegisterHandler<SceneMessage>(OnClientSceneChanged, false);
    }

    void OnDisable()
    {
        NetworkClient.UnregisterHandler<SceneMessage>();
    }

    void OnClientSceneChanged(SceneMessage msg)
    {
        // 씬 로드 완료 후 자동으로 Ready 전송
        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();
        }

        // Ready 후 자동으로 플레이어 스폰 요청
        if (NetworkClient.localPlayer == null)
        {
            NetworkClient.AddPlayer();
        }
    }
    public void FindMatch()
    {
        matched = false;
        isSearching = true;
        searchTimer = searchTimeout;

        // 서버 발견 시 콜백 등록
        discovery.OnServerFound += OnDiscoveredServer;

        // LAN에서 Host 탐색 시작
        discovery.StartDiscovery();

        Debug.Log("LAN에서 호스트 탐색 중...");
    }

    void Update()
    {
        if (!isSearching) return;

        searchTimer -= Time.deltaTime;

        // 시간 내 호스트를 못 찾으면 → 내가 호스트
        if (searchTimer <= 0f && !matched)
        {
            isSearching = false;
            discovery.StopDiscovery();
            BecomeHost();
        }
    }

    void OnDiscoveredServer(DiscoveryResponse response)
    {
        if (matched) return;

        // 자리가 있는 방만 접속
        if (response.currentPlayers >= response.maxPlayers) return;

        matched = true;
        isSearching = false;
        discovery.StopDiscovery();
        discovery.OnServerFound -= OnDiscoveredServer;

        // 자동으로 해당 Host에 접속
        networkManager.StartClient(response.uri);
        Debug.Log($"호스트 발견! 접속 중: {response.uri}");

        // 씬 로드 완료 후 자동으로 Ready 전송
        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();
        }
    }

    void BecomeHost()
    {
        discovery.OnServerFound -= OnDiscoveredServer;

        networkManager.StartHost();
        discovery.AdvertiseServer(); // 다른 클라이언트에게 내 존재 알림

        Debug.Log("호스트를 찾지 못해 새 호스트로 시작합니다.");
    }
}
