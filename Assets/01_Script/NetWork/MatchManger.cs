using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MatchManager : NetworkManager
{
    [Header("Match Settings")]
    public string gameSceneName = "GameScene";
    public string charSelectSceneName = "CharSelectScene"; 
    public float sceneChangeDelay = 1.5f; // 매칭 성공 후 대기 시간
    private bool isMatchStarted = false;

    [Header("Prefabs")]
    public GameObject charSelectManagerPrefab; // Inspector에서 CharSelectManager 프리팹 연결

    private bool charSelectManagerSpawned = false;

    private readonly List<NetworkConnectionToClient> readyConnsInCharSelect = new List<NetworkConnectionToClient>();

    // 캐릭터 선택 결과 - 캐릭터 신 매니저가 채움
    [HideInInspector] public int p1CharacterIndex = 0; // 플레이어가 선택한 캐릭터 인덱스
    [HideInInspector] public int p2CharacterIndex = 0;
    [HideInInspector] public GameObject[] characterPrefabs; // 캐릭터 프리팹 배열

    // 접속 순서에 따른 플레이어 번호 부여 - (P1 = 0, P2 = 1)
    private readonly List<NetworkConnectionToClient> connectionOrder = new List<NetworkConnectionToClient>();
    public int GetConnectionOrder(NetworkConnectionToClient conn)
    {
        int idx = connectionOrder.IndexOf(conn);
        Debug.Log($"[MatchManager] GetConnectionOrder - connId:{conn.connectionId} idx:{idx} 전체목록:{connectionOrder.Count}");
        return idx >= 0 ? idx : 0;
    }

    // 2명 접속 감지 시 캐릭터 선택창으로 이동
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);

        if (!connectionOrder.Contains(conn))
            connectionOrder.Add(conn);

        // 현재 연결된 총 인원(호스트 포함)이 2명이고 아직 시작 안 했다면
        if (NetworkServer.connections.Count == 2 && !isMatchStarted)
        {
            isMatchStarted = true;
            Debug.Log("2명 연결 확인! 매칭 성공 메시지를 보냅니다.");
            
            // 모든 클라이언트에게 매칭 성공 알림 (UI 표시용)
            NetworkServer.SendToAll(new MatchSuccessMessage());
            
            // 씬 전환 코루틴 시작
            StartCoroutine(ChangeSceneRoutine());
        }
    }

    IEnumerator ChangeSceneRoutine()
    {
        yield return new WaitForSeconds(sceneChangeDelay);
        Debug.Log("캐릭터 선택 신으로 이동합니다.");
        ServerChangeScene(charSelectSceneName);
    }

    // 서버 준비시 작동하는 함수 - 현재 신마다 동작하는 것이 다름
    public override void OnServerReady(NetworkConnectionToClient conn)
    {  
        base.OnServerReady(conn);

        // 현재 활성 씬
        string sceneName = SceneManager.GetActiveScene().name;

        // 캐릭터 선택 신 - 연결 순서만 등록하기
        if(sceneName == charSelectSceneName)
        {
            StartCoroutine(RegisterWhenReady(conn)); // 캐릭터 선택 신 매니저 등록 코루틴
            return;
        }

        // 게임 신에서 
        if(sceneName == gameSceneName)
        {
            if (conn.identity != null) return;

            // 캐릭터 프리팹 미등록 방지
            if (characterPrefabs == null || characterPrefabs.Length == 0)
            {
                Debug.LogError("[MatchManager] characterPrefabs가 비어있습니다!");
                return;
            }

            int playerIndex = GetConnectionOrder(conn);
            int charIdx = (playerIndex == 0) ? p1CharacterIndex : p2CharacterIndex;
            charIdx = Mathf.Clamp(charIdx, 0, characterPrefabs.Length - 1);

        
            Transform startPos = GetStartPosition();
            Vector3 pos = startPos  != null ? startPos .position : Vector3.zero;
            Quaternion rot = startPos  != null ? startPos .rotation : Quaternion.identity;

            GameObject player = Instantiate(characterPrefabs[charIdx], pos, rot);
            NetworkServer.AddPlayerForConnection(conn, player);

            Debug.Log($"[MatchManager] P{playerIndex + 1} 스폰 완료 → 캐릭터 인덱스 {charIdx}");
        }
    }

    // 서버가 중단될 때 플래그 초기화
    public override void OnStopServer()
    {
        base.OnStopServer();
        isMatchStarted = false;
    }

    // 클라이언트가 종료될 때 플래그 초기화
    public override void OnStopClient()
    {
        base.OnStopClient();
        isMatchStarted = false;
    }

    public override void OnClientDisconnect()
    {
        // 순수 클라이언트가 게임 씬에서 연결 끊겼을 때만 처리
        if (!NetworkServer.active)
        {
            if (NetworkGameManger.instance != null)
                NetworkGameManger.instance.ForceShowDisconnectUI();
        }
        else
        {
            base.OnClientDisconnect(); // 호스트 측은 기본 처리
        }
    }

    public void ReturnToTitle()
    {
        StartCoroutine(ReturnToTitleRoutine());
    }

    private IEnumerator ReturnToTitleRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        StopHost();
        
        DestroyImmediate(gameObject); // 게임 신 매니저 파괴
        // yield return null;

        UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
    }

    // 캐릭터 선택창 매니저 등록 대기
    private IEnumerator RegisterWhenReady(NetworkConnectionToClient conn)
    {
        // conn 수집 (중복 방지)
        if (!readyConnsInCharSelect.Contains(conn))
            readyConnsInCharSelect.Add(conn);

        // ★ 두 플레이어가 모두 Ready될 때까지 대기
        float timeout = 10f;
        while (readyConnsInCharSelect.Count < 2 && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // ★ 첫 번째로 여기 도달한 코루틴만 스폰 실행
        if (!charSelectManagerSpawned)
        {
            charSelectManagerSpawned = true;
            var go = Instantiate(charSelectManagerPrefab);
            NetworkServer.Spawn(go); // 이 시점엔 두 클라이언트 모두 Ready → SpawnMessage 정상 수신

            // ★ 스폰 후 netId 부여 대기
            var netId = go.GetComponent<NetworkIdentity>();
            float spawnTimeout = 5f;
            while (netId.netId == 0 && spawnTimeout > 0f)
            {
                spawnTimeout -= Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[MatchManager] CharSelectManager 스폰 완료 - netId:{go.GetComponent<NetworkIdentity>().netId}");
        }

        // CharSelectManager 활성화 대기
        timeout = 5f;
        while ((CharSelectManager.instance == null ||
            !CharSelectManager.instance.isActiveAndEnabled ||
            CharSelectManager.instance.netId == 0) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (CharSelectManager.instance != null)
        {
            Debug.Log($"[MatchManager] RegisterConnection 호출 - connId:{conn.connectionId}");
            CharSelectManager.instance.RegisterConnection(conn);
        }
        else
            Debug.LogError("[MatchManager] CharSelectManager를 찾지 못했습니다! (timeout)");
    }

    // OnServerSceneChanged에서 초기화
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        charSelectManagerSpawned = false;
        readyConnsInCharSelect.Clear(); // ★ 추가
    }

    
}