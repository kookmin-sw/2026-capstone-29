using Mirror;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CharSelectUI : MonoBehaviour
{
    public static CharSelectUI instance;

    [Header("P1 패널")]
    public Image p1Portrait;
    public GameObject p1ReadyBadge;

    [Header("P2 패널")]
    public Image p2Portrait;
    public GameObject p2ReadyBadge;

    [Header("썸네일 행")]
    public Transform thumbnailContainer;
    public GameObject thumbnailSlotPrefab;

    [Header("커서 인디케이터")]
    public RectTransform p1CursorIndicator;
    public RectTransform p2CursorIndicator;

    [Header("캐릭터 정보 텍스트")]
    public Text characterNameText;
    public Text characterDescText;

    [Header("카운트다운")]
    public GameObject countdownPanel;
    public Text countdownText;

    [Header("입력 쿨다운")]
    public float inputCooldown = 0.18f;

    private RectTransform[] thumbnailSlots;
    private CharacterData[] characters;
    private float lastInputTime;
    private bool isLocalConfirmed = false;
    private int localPlayerIndex = -1;
    private bool isInitialized = false;

    // OnNetworkReady 또는 SetLocalPlayerIndex 중 하나라도 호출되면 true
    private bool isNetworkReady = false;

    private void Awake()
    {
        instance = this;
        Debug.Log("[CharSelectUI] Awake");
    }

    private void Start()
    {
        Debug.Log("[CharSelectUI] Start - CharSelectManager 대기 시작");
    }

    // CharSelectManager.OnStartClient에서 호출
    public void OnNetworkReady()
    {
        Debug.Log($"[CharSelectUI] OnNetworkReady - netId:{CharSelectManager.instance?.netId}");
        isNetworkReady = true;

        if (!isInitialized && CharSelectManager.instance != null)
            InitUI(CharSelectManager.instance);

        // WaitAndInit이 이미 진행 중일 수 있으므로 localPlayerIndex도 여기서 처리
        if (localPlayerIndex < 0)
            ResolveLocalPlayerIndex();

    }

    private void ResolveLocalPlayerIndex()
    {
        if (NetworkServer.active)
        {
            localPlayerIndex = 0;
            Debug.Log("[CharSelectUI] 호스트 → localPlayerIndex = 0");
        }
        else if (NetworkClient.active)
        {
            localPlayerIndex = 1;
            Debug.Log("[CharSelectUI] 클라이언트 → localPlayerIndex = 1");
        }
        else
        {
            Debug.LogError("[CharSelectUI] 네트워크 연결 상태 이상!");
        }
    }

    // CharSelectManager.TargetSetPlayerIndex에서 호출
    public void SetLocalPlayerIndex(int index)
    {
        localPlayerIndex = index;
        isNetworkReady = true;
        Debug.Log($"[CharSelectUI] SetLocalPlayerIndex = {index}, " +
                $"instance={CharSelectManager.instance != null}, " +
                $"isInit={isInitialized}, " +
                $"characters={(CharSelectManager.instance?.characters?.Length ?? -1)}");

        if (!isInitialized && CharSelectManager.instance != null)
        {
            Debug.Log("[CharSelectUI] SetLocalPlayerIndex → InitUI 재시도");
            InitUI(CharSelectManager.instance);
        }
    }

    // ── UI 초기화 ─────────────────────────────────────────────────

    public void InitUI(CharSelectManager mgr)
    {
        if (isInitialized) return;
        isInitialized = true;

        Debug.Log("[CharSelectUI] InitUI 호출됨");

        characters = mgr.characters;
        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("[CharSelectUI] characters 배열이 비어있음!");
            return;
        }

        BuildThumbnails();
        StartCoroutine(InitCursorsNextFrame(mgr));

        if (p1ReadyBadge) p1ReadyBadge.SetActive(false);
        if (p2ReadyBadge) p2ReadyBadge.SetActive(false);
        if (countdownPanel) countdownPanel.SetActive(false);
    }

    private IEnumerator InitCursorsNextFrame(CharSelectManager mgr)
    {
        yield return null;
        yield return null;
        UpdateCursor(0, mgr.p1CursorIndex, mgr.p1SelectedIndex >= 0);
        UpdateCursor(1, mgr.p2CursorIndex, mgr.p2SelectedIndex >= 0);
        UpdateSelection(0, mgr.p1SelectedIndex);
        UpdateSelection(1, mgr.p2SelectedIndex);
        RefreshLocalInfo(0);
        Debug.Log("[CharSelectUI] 커서 초기화 완료");
    }

    // ── 입력 처리 ─────────────────────────────────────────────────

    private void Update() 
    { 
        if (!isInitialized)
        {
            TryInit();
            return;
        }    

        HandleInput(); 
        
    }

    private void TryInit()
    {
        var mgr = CharSelectManager.instance;
        if (mgr == null) return;

        // SyncVar Hook에서 instance가 등록된 이후에만 통과
        // instance가 null이 아니면 netId도 유효한 상태
        if (!isNetworkReady)
        {
            if (NetworkServer.active || NetworkClient.isConnected)
                isNetworkReady = true;
            else
                return;
        }

        Debug.Log($"[CharSelectUI] TryInit 통과");
        InitUI(mgr);

        if (localPlayerIndex < 0)
            ResolveLocalPlayerIndex();
    }

    private void HandleInput()
    {
        if (!isInitialized) return;
        if (localPlayerIndex < 0) return;
        // netId 체크 완전 제거 — 메시지 방식은 netId 불필요

        if (!isLocalConfirmed)
        {
            bool canMove = Time.time - lastInputTime > inputCooldown;
            bool moveLeft  = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)
                        || (canMove && (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)));
            bool moveRight = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)
                        || (canMove && (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)));

            if (moveLeft)
            {
                NetworkClient.Send(new CharSelectInputMessage { action = 0 });
                lastInputTime = Time.time;
            }
            else if (moveRight)
            {
                NetworkClient.Send(new CharSelectInputMessage { action = 1 });
                lastInputTime = Time.time;
            }
        }

        if (!isLocalConfirmed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            NetworkClient.Send(new CharSelectInputMessage { action = 2 });
            isLocalConfirmed = true;
        }

        if (isLocalConfirmed && Input.GetKeyDown(KeyCode.Escape))
        {
            NetworkClient.Send(new CharSelectInputMessage { action = 3 });
            isLocalConfirmed = false;
        }
        }
        // ── UI 업데이트 ───────────────────────────────────────────────

        public void UpdateCursor(int playerIndex, int cursorIndex, bool isLocked)
        {
            if (characters == null || cursorIndex < 0 || cursorIndex >= characters.Length) return;
            var indicator = (playerIndex == 0) ? p1CursorIndicator : p2CursorIndicator;
            MoveCursorIndicator(indicator, cursorIndex);
            if (!isLocked)
            {
                var portrait = (playerIndex == 0) ? p1Portrait : p2Portrait;
                if (portrait != null && characters[cursorIndex].portrait != null)
                    portrait.sprite = characters[cursorIndex].portrait;
            }
    }

    public void UpdateSelection(int playerIndex, int selectedIndex)
    {
        bool isReady   = selectedIndex >= 0;
        var readyBadge = (playerIndex == 0) ? p1ReadyBadge : p2ReadyBadge;
        var portrait   = (playerIndex == 0) ? p1Portrait   : p2Portrait;
        if (readyBadge != null) readyBadge.SetActive(isReady);
        if (isReady && characters != null && selectedIndex < characters.Length)
            if (portrait != null && characters[selectedIndex].portrait != null)
                portrait.sprite = characters[selectedIndex].portrait;
    }

    public void UpdateReadyState(int readyCount)
    {
        Debug.Log($"[CharSelectUI] 준비 완료: {readyCount}/2");
    }

    public void ShowCountdown()
    {
        if (countdownPanel != null) countdownPanel.SetActive(true);
        StartCoroutine(CountdownRoutine());
    }

    // ── 썸네일 빌드 ───────────────────────────────────────────────

    private void BuildThumbnails()
    {
        if (thumbnailContainer == null || thumbnailSlotPrefab == null)
        {
            Debug.LogError("[CharSelectUI] thumbnailContainer 또는 thumbnailSlotPrefab이 null!");
            return;
        }
        thumbnailSlots = new RectTransform[characters.Length];
        for (int i = 0; i < characters.Length; i++)
        {
            var go  = Instantiate(thumbnailSlotPrefab, thumbnailContainer);
            go.name = $"Slot_{i}";
            var img = go.GetComponentInChildren<Image>();
            if (img != null && characters[i].thumbnail != null)
                img.sprite = characters[i].thumbnail;
            thumbnailSlots[i] = go.GetComponent<RectTransform>();
        }
        Debug.Log($"[CharSelectUI] 썸네일 {characters.Length}개 생성 완료");
    }

    private void MoveCursorIndicator(RectTransform indicator, int index)
    {
        if (indicator == null || thumbnailSlots == null) return;
        if (index < 0 || index >= thumbnailSlots.Length) return;
        Canvas canvas = indicator.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            canvas.worldCamera, thumbnailSlots[index].position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(), screenPoint,
            canvas.worldCamera, out Vector2 localPoint);
        indicator.anchoredPosition = localPoint;
    }

    private void RefreshLocalInfo(int index)
    {
        if (characters == null || index < 0 || index >= characters.Length) return;
        if (characterNameText != null) characterNameText.text = characters[index].characterName;
        if (characterDescText  != null) characterDescText.text  = characters[index].description;
    }

    private IEnumerator CountdownRoutine()
    {
        string[] steps = { "3", "2", "1", "FIGHT!" };
        foreach (var step in steps)
        {
            if (countdownText != null) countdownText.text = step;
            yield return new WaitForSeconds(step == "FIGHT!" ? 0.8f : 1f);
        }
    }
}