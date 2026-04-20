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
    public float sceneChangeDelay = 1.5f;
    private bool isMatchStarted = false;

    [Header("Prefabs")]
    public GameObject charSelectManagerPrefab; // 다시 추가

    [HideInInspector] public int p1CharacterIndex = 0;
    [HideInInspector] public int p2CharacterIndex = 0;
    [HideInInspector] public GameObject[] characterPrefabs;

    private readonly List<NetworkConnectionToClient> connectionOrder
        = new List<NetworkConnectionToClient>();

    // CharSelectScene에서 Ready된 conn 수집
    private readonly List<NetworkConnectionToClient> readyConns
        = new List<NetworkConnectionToClient>();
    private bool charSelectManagerSpawned = false;

    public int GetConnectionOrder(NetworkConnectionToClient conn)
    {
        int idx = connectionOrder.IndexOf(conn);
        return idx >= 0 ? idx : 0;
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        if (!connectionOrder.Contains(conn))
            connectionOrder.Add(conn);

        if (NetworkServer.connections.Count == 2 && !isMatchStarted)
        {
            isMatchStarted = true;
            NetworkServer.SendToAll(new MatchSuccessMessage());
            StartCoroutine(ChangeSceneRoutine());
        }
    }

    IEnumerator ChangeSceneRoutine()
    {
        yield return new WaitForSeconds(sceneChangeDelay);
        ServerChangeScene(charSelectSceneName);
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        if (sceneName == charSelectSceneName)
        {
            readyConns.Clear();
            charSelectManagerSpawned = false;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<CharSelectInputMessage>(OnCharSelectInput);
    }

    private void OnCharSelectInput(NetworkConnectionToClient conn, CharSelectInputMessage msg)
    {
        var mgr = CharSelectManager.instance;
        if (mgr == null) return;

        switch (msg.action)
        {
            case 0: mgr.ServerMoveCursor(conn, 0); break;  // 왼쪽
            case 1: mgr.ServerMoveCursor(conn, 1); break;  // 오른쪽
            case 2: mgr.ServerConfirmSelection(conn); break;
            case 3: mgr.ServerCancelSelection(conn); break;
        }
    }

    // ★ 핵심: OnServerReady에서 두 클라이언트 모두 Ready 완료 후 Spawn
    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);

        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == charSelectSceneName)
        {
            if (!readyConns.Contains(conn))
                readyConns.Add(conn);

            Debug.Log($"[MatchManager] CharSelect Ready: {readyConns.Count}/2 (connId:{conn.connectionId})");

            // 두 클라이언트 모두 Ready됐을 때 딱 한 번 Spawn
            if (readyConns.Count >= 2 && !charSelectManagerSpawned)
            {
                charSelectManagerSpawned = true;
                StartCoroutine(SpawnCharSelectManager());
            }
            return;
        }

        if (sceneName == gameSceneName)
        {
            if (conn.identity != null) return;
            if (characterPrefabs == null || characterPrefabs.Length == 0)
            {
                Debug.LogError("[MatchManager] characterPrefabs 비어있음!");
                return;
            }

            int playerIndex = GetConnectionOrder(conn);
            int charIdx = (playerIndex == 0) ? p1CharacterIndex : p2CharacterIndex;
            charIdx = Mathf.Clamp(charIdx, 0, characterPrefabs.Length - 1);

            Transform startPos = GetStartPosition();
            Vector3 pos = startPos != null ? startPos.position : Vector3.zero;
            Quaternion rot = startPos != null ? startPos.rotation : Quaternion.identity;

            GameObject player = Instantiate(characterPrefabs[charIdx], pos, rot);
            NetworkServer.AddPlayerForConnection(conn, player);
            Debug.Log($"[MatchManager] P{playerIndex + 1} 스폰 완료 → 캐릭터 {charIdx}");
        }
    }

    private IEnumerator SpawnCharSelectManager()
    {
        // 두 클라이언트 모두 Ready이므로 즉시 Spawn해도 안전
        // 단, 같은 프레임 내 충돌 방지를 위해 1프레임 대기
        yield return null;

        var go = Instantiate(charSelectManagerPrefab);
        NetworkServer.Spawn(go);

        // netId 부여 대기
        var netIdentity = go.GetComponent<NetworkIdentity>();
        float timeout = 5f;
        while (netIdentity.netId == 0 && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[MatchManager] CharSelectManager Spawn 완료 - netId:{netIdentity.netId}");

        // netId가 유효해진 후 RegisterConnection
        var mgr = go.GetComponent<CharSelectManager>();
        foreach (var conn in readyConns)
        {
            Debug.Log($"[MatchManager] RegisterConnection - connId:{conn.connectionId}");
            mgr.RegisterConnection(conn);
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        isMatchStarted = false;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        isMatchStarted = false;
    }

    public override void OnClientDisconnect()
    {
        if (!NetworkServer.active)
        {
            if (NetworkGameManger.instance != null)
                NetworkGameManger.instance.ForceShowDisconnectUI();
        }
        else
        {
            base.OnClientDisconnect();
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
        DestroyImmediate(gameObject);
        SceneManager.LoadScene("TitleScene");
    }
}