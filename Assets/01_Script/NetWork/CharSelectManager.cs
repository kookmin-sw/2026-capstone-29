using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CharSelectManager : NetworkBehaviour
{
    public static CharSelectManager instance;

    [Header("캐릭터 데이터 (인덱스 순서 일치 필수)")]
    public CharacterData[] characters;
    public GameObject[] characterPrefabs;

    [Header("씬 설정")]
    public string gameSceneName = "GameScene";

    [SyncVar(hook = nameof(OnP1CursorChanged))]
    public int p1CursorIndex = 0;

    [SyncVar(hook = nameof(OnP2CursorChanged))]
    public int p2CursorIndex = 0;

    [SyncVar(hook = nameof(OnP1SelectionChanged))]
    public int p1SelectedIndex = -1;

    [SyncVar(hook = nameof(OnP2SelectionChanged))]
    public int p2SelectedIndex = -1;

    [SyncVar(hook = nameof(OnReadyCountChanged))]
    public int readyCount = 0;

    [SyncVar(hook = nameof(OnReadyToAssignChanged))]
    public bool isReadyToAssign = false;

    private readonly Dictionary<NetworkConnectionToClient, int> connIndexMap
        = new Dictionary<NetworkConnectionToClient, int>();

    // ── 생명주기 ──────────────────────────────────────────────────

    private void Awake()
    {
        Debug.Log($"[CharSelectManager] Awake - netId:{netId} isServer:{isServer} isClient:{isClient}");
    }

    // 씬 오브젝트이므로 OnStartServer는 서버/호스트가 씬을 로드한 직후 호출됨
    public override void OnStartServer()
    {
        base.OnStartServer();
        instance = this;
        isReadyToAssign = false;
        p1CursorIndex = 0; p2CursorIndex = 0;
        p1SelectedIndex = -1; p2SelectedIndex = -1;
        readyCount = 0;
        connIndexMap.Clear();
        Debug.Log($"[CharSelectManager] OnStartServer - netId:{netId}");
    }

    // 씬 오브젝트이므로 OnStartClient는 클라이언트가 씬을 로드한 직후 호출됨
    // → 이 시점에 netId는 반드시 유효한 값 (0이 아님)
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[CharSelectManager] OnStartClient - netId:{netId}");

        if (CharSelectUI.instance != null)
            CharSelectUI.instance.OnNetworkReady();
    }

    private void CheckLocalPlayerIndex()
    {
        if (!NetworkClient.active) return;

        // 클라이언트의 connectionId는 NetworkClient.connection.remoteTimeStamp 대신
        // 서버가 알고 있는 connId를 직접 비교할 수 없으므로
        // 호스트(서버)는 connId=0, 클라이언트는 그 외 값
        bool isHost = NetworkServer.active;

        if (isHost)
        {
            // 호스트는 항상 P1
            instance = this;
            CharSelectUI.instance?.SetLocalPlayerIndex(0);
        }
        else
        {
            // 순수 클라이언트는 항상 P2
            instance = this;
            CharSelectUI.instance?.SetLocalPlayerIndex(1);
        }
    }

    void OnReadyToAssignChanged(bool _, bool v)
    {
        if (!v) return;
        // SyncVar가 도착한 시점 = 오브젝트 완전 초기화 완료
        instance = this;
        Debug.Log($"[CharSel][Client] instance 등록 - netId:{netId}");

        bool isHost = NetworkServer.active;
        int index = isHost ? 0 : 1;
        CharSelectUI.instance?.SetLocalPlayerIndex(index);
    }
    // ── MatchManager에서 씬 전환 완료 후 호출 ────────────────────

    [Server]
    public void RegisterConnection(NetworkConnectionToClient conn)
    {
        int index = connIndexMap.Count; // 0번째 = P1, 1번째 = P2
        connIndexMap[conn] = index;
        Debug.Log($"[CharSel] P{index + 1} 등록 - connId:{conn.connectionId}");

        // 두 conn 모두 등록 완료 시 SyncVar trigger
        if (connIndexMap.Count >= 2)
            isReadyToAssign = true;
    }

    [TargetRpc]
    private void TargetSetPlayerIndex(NetworkConnectionToClient target, int index)
    {
        StartCoroutine(RegisterInstanceWhenReady(index));
    }

    private IEnumerator RegisterInstanceWhenReady(int index)
    {
        // netId가 유효해질 때까지 대기
        while (netId == 0)
            yield return null;

        instance = this;
        Debug.Log($"[CharSel][Client] instance 등록 완료 - netId:{netId} index:{index}");
        CharSelectUI.instance?.SetLocalPlayerIndex(index);
    }

    // ── Commands ──────────────────────────────────────────────────

    [Server]
    public void ServerMoveCursor(NetworkConnectionToClient conn, int direction)
    {
        int pi = GetPlayerIndex(conn);
        if (pi < 0) return;
        int current = (pi == 0) ? p1CursorIndex : p2CursorIndex;
        int next = Mathf.Clamp(current + (direction == 1 ? 1 : -1), 0, characters.Length - 1);
        if (pi == 0) p1CursorIndex = next;
        else         p2CursorIndex = next;
    }

    [Server]
    public void ServerConfirmSelection(NetworkConnectionToClient conn)
    {
        int pi = GetPlayerIndex(conn);
        if (pi < 0) return;
        if (pi == 0 && p1SelectedIndex >= 0) return;
        if (pi == 1 && p2SelectedIndex >= 0) return;
        int cursor = (pi == 0) ? p1CursorIndex : p2CursorIndex;
        if (pi == 0) p1SelectedIndex = cursor;
        else         p2SelectedIndex = cursor;
        readyCount++;
        if (readyCount >= 2)
        {
            RpcShowCountdown();
            StartCoroutine(StartGameRoutine());
        }
    }

    [Server]
    public void ServerCancelSelection(NetworkConnectionToClient conn)
    {
        int pi = GetPlayerIndex(conn);
        if (pi < 0) return;
        bool wasReady = (pi == 0) ? p1SelectedIndex >= 0 : p2SelectedIndex >= 0;
        if (!wasReady) return;
        if (pi == 0) p1SelectedIndex = -1;
        else         p2SelectedIndex = -1;
        readyCount = Mathf.Max(0, readyCount - 1);
    }

    [Server]
    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(3.8f);
        if (NetworkManager.singleton is MatchManager mm)
        {
            mm.p1CharacterIndex = p1SelectedIndex;
            mm.p2CharacterIndex = p2SelectedIndex;
            mm.characterPrefabs = characterPrefabs;
            mm.ServerChangeScene(gameSceneName);
        }
    }

    [ClientRpc]
    private void RpcShowCountdown()
    {
        CharSelectUI.instance?.ShowCountdown();
    }

    // ── SyncVar Hooks ─────────────────────────────────────────────

    void OnP1CursorChanged(int _, int v)    => CharSelectUI.instance?.UpdateCursor(0, v, p1SelectedIndex >= 0);
    void OnP2CursorChanged(int _, int v)    => CharSelectUI.instance?.UpdateCursor(1, v, p2SelectedIndex >= 0);
    void OnP1SelectionChanged(int _, int v) => CharSelectUI.instance?.UpdateSelection(0, v);
    void OnP2SelectionChanged(int _, int v) => CharSelectUI.instance?.UpdateSelection(1, v);
    void OnReadyCountChanged(int _, int v)  => CharSelectUI.instance?.UpdateReadyState(v);

    private int GetPlayerIndex(NetworkConnectionToClient conn)
    {
        if (conn == null) return -1;
        return connIndexMap.TryGetValue(conn, out int idx) ? idx : -1;
    }
}