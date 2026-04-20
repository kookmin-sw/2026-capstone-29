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

    // 서버 전용
    private readonly Dictionary<NetworkConnectionToClient, int> connIndexMap
        = new Dictionary<NetworkConnectionToClient, int>();
    private int serverRegisteredCount = 0;

    private void Awake()
    {
        instance = this;
        Debug.Log($"[CharSelectManager] Awake - netId:{GetComponent<NetworkIdentity>().netId} isServer:{isServer} isClient:{isClient}");
    }

    // ── 서버 시작 시 ──────────────────────────────
    public override void OnStartServer()
    {
        base.OnStartServer();
        p1CursorIndex = 0; p2CursorIndex = 0;
        p1SelectedIndex = -1; p2SelectedIndex = -1;
        readyCount = 0;
        connIndexMap.Clear();
        serverRegisteredCount = 0;
    }

    // ── 클라이언트 시작 시 (씬 오브젝트 활성화 완료 후 호출됨) ──
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[CharSelectManager] OnStartClient - netId:{netId}");
        // WaitAndInit 폴링이 netId>0을 감지해서 자동으로 통과함
        // 추가로 UI에 직접 알림
        if (CharSelectUI.instance != null)
        {
            CharSelectUI.instance.OnNetworkReady();
        }
    }

    private IEnumerator NotifyUIWhenReady()
    {
        // OnStartClient 직후에도 netId가 0일 수 있으므로 한 프레임 대기
        yield return null;
        yield return null;
        
        Debug.Log($"[CharSelectManager] NotifyUIWhenReady - netId:{netId}");
        CharSelectUI.instance?.OnNetworkReady();
    }

    // 추가: 호스트 전용 index 직접 설정
    private IEnumerator DelayedSetHostIndex()
    {
        // RegisterConnection이 완료될 때까지 대기
        float timeout = 3f;
        while (timeout > 0f)
        {
            var localConn = NetworkServer.localConnection;
            if (localConn != null && connIndexMap.TryGetValue(localConn, out int idx))
            {
                Debug.Log($"[CharSel][Host] 호스트 playerIndex 직접 설정: {idx}");
                CharSelectUI.instance?.SetLocalPlayerIndex(idx);
                yield break;
            }
            timeout -= Time.deltaTime;
            yield return null;
        }
        Debug.LogError("[CharSel][Host] 호스트 index 설정 실패 (timeout)");
    }

    // MatchManager.OnServerReady에서 호출
    [Server]
    public void RegisterConnection(NetworkConnectionToClient conn)
    {
        // connectionOrder 기반 말고 serverRegisteredCount 순서로 부여
        // 단, 호스트(localConnection)는 항상 0번
        int index;
        if (NetworkServer.localConnection == conn)
            index = 0;
        else
            index = 1;  // 순수 클라이언트는 항상 1번

        connIndexMap[conn] = index;
        serverRegisteredCount++;
        Debug.Log($"[CharSel] P{index + 1} conn 등록 완료 (connId={conn.connectionId})");
        StartCoroutine(DelayedSetPlayerIndex(conn, index));
    }

    // 새로 추가할 코루틴
    private IEnumerator DelayedSetPlayerIndex(NetworkConnectionToClient conn, int index)
    {
        // netId가 0이 아닐 때까지 대기 (스폰 완전 완료 확인)
        float timeout = 5f;
        while (netId == 0 && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (netId == 0)
        {
            Debug.LogError("[CharSel] TargetRpc 발송 실패 - netId가 여전히 0");
            yield break;
        }

        // 추가 안전 딜레이
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[CharSel] TargetRpc 발송 - netId:{netId}, connId:{conn.connectionId}");
        TargetSetPlayerIndex(conn, index);
    }
    
    [TargetRpc]
    private void TargetSetPlayerIndex(NetworkConnectionToClient target, int index)
    {
        Debug.Log($"[CharSel][Client] 내 playerIndex = {index}");
        CharSelectUI.instance?.SetLocalPlayerIndex(index);
    }

    // ── Commands ──────────────────────────────────
    [Command(requiresAuthority = false)]
    public void CmdMoveCursor(int direction, NetworkConnectionToClient sender = null)
    {
        int pi = GetPlayerIndex(sender);
        Debug.Log($"[CharSel] CmdMoveCursor - pi:{pi} dir:{direction}");
        if (pi < 0) return;

        int current = (pi == 0) ? p1CursorIndex : p2CursorIndex;
        int next = Mathf.Clamp(current + (direction == 1 ? 1 : -1), 0, characters.Length - 1);
        if (pi == 0) p1CursorIndex = next;
        else p2CursorIndex = next;
    }

    [Command(requiresAuthority = false)]
    public void CmdConfirmSelection(NetworkConnectionToClient sender = null)
    {
        int pi = GetPlayerIndex(sender);
        if (pi < 0) return;
        if (pi == 0 && p1SelectedIndex >= 0) return;
        if (pi == 1 && p2SelectedIndex >= 0) return;

        int cursor = (pi == 0) ? p1CursorIndex : p2CursorIndex;
        if (pi == 0) p1SelectedIndex = cursor;
        else p2SelectedIndex = cursor;

        readyCount++;
        Debug.Log($"[CharSel] P{pi + 1} 확정 → readyCount={readyCount}");

        if (readyCount >= 2)
        {
            RpcShowCountdown();
            StartCoroutine(StartGameRoutine());
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdCancelSelection(NetworkConnectionToClient sender = null)
    {
        int pi = GetPlayerIndex(sender);
        if (pi < 0) return;

        bool wasReady = (pi == 0) ? p1SelectedIndex >= 0 : p2SelectedIndex >= 0;
        if (!wasReady) return;

        if (pi == 0) p1SelectedIndex = -1;
        else p2SelectedIndex = -1;
        readyCount = Mathf.Max(0, readyCount - 1);
    }

    [Server]
    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(3.8f); // 카운트다운 대기
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

    // ── SyncVar Hooks ─────────────────────────────
    void OnP1CursorChanged(int _, int v) => CharSelectUI.instance?.UpdateCursor(0, v, p1SelectedIndex >= 0);
    void OnP2CursorChanged(int _, int v) => CharSelectUI.instance?.UpdateCursor(1, v, p2SelectedIndex >= 0);
    void OnP1SelectionChanged(int _, int v) => CharSelectUI.instance?.UpdateSelection(0, v);
    void OnP2SelectionChanged(int _, int v) => CharSelectUI.instance?.UpdateSelection(1, v);
    void OnReadyCountChanged(int _, int v) => CharSelectUI.instance?.UpdateReadyState(v);

    private int GetPlayerIndex(NetworkConnectionToClient conn)
    {
        if (conn == null) return -1;
        return connIndexMap.TryGetValue(conn, out int idx) ? idx : -1;
    }
}