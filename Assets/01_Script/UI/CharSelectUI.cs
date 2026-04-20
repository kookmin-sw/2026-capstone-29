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
    private bool isNetworkReady = false; 

    private void Awake()
    {
        instance = this;
        Debug.Log("[CharSelectUI] Awake");
    }

    private void Start()
    {
        Debug.Log("[CharSelectUI] Start - CharSelectManager 대기 시작");
        StartCoroutine(WaitAndInit());
    }

    // CharSelectManager.OnStartClient에서 호출됨
    public void OnNetworkReady()
    {
        Debug.Log($"[CharSelectUI] OnNetworkReady - netId:{CharSelectManager.instance?.netId}");
        isNetworkReady = true;
        
        // InitUI가 아직 안 됐으면 여기서 처리
        if (!isInitialized && CharSelectManager.instance != null)
            InitUI(CharSelectManager.instance);
    }

    private IEnumerator WaitAndInit()
    {
        float timeout = 5f;
        while (!isNetworkReady && !isInitialized && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!isInitialized && CharSelectManager.instance == null)
        {
            Debug.LogError("[CharSelectUI] CharSelectManager 초기화 실패");
            yield break;
        }

        Debug.Log($"[CharSelectUI] WaitAndInit 통과");

        if (!isInitialized)
            InitUI(CharSelectManager.instance);

        if (localPlayerIndex < 0)
            ResolveLocalPlayerIndex();
        else
            Debug.Log($"[CharSelectUI] localPlayerIndex 이미 설정됨({localPlayerIndex}), 스킵");
    }

    private void ResolveLocalPlayerIndex()
    {
        // 호스트 = 서버이면서 클라이언트 → 항상 P1
        if (NetworkServer.active)
        {
            localPlayerIndex = 0;
            Debug.Log("[CharSelectUI] 호스트 → localPlayerIndex = 0");
        }
        // 순수 클라이언트 → 항상 P2
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

    // CharSelectManager.OnStartClient()에서 직접 호출
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

    // 썸네일 레이아웃 계산 후 커서 초기화 (2프레임 대기)
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

    public void SetLocalPlayerIndex(int index)
    {
        localPlayerIndex = index;
        isNetworkReady = true;  // ← 이 줄 추가
        Debug.Log($"[CharSelectUI] localPlayerIndex = {index}");

        // ★ 추가: index가 설정될 때 InitUI가 아직 안 끝났으면 여기서 시도
        if (!isInitialized && CharSelectManager.instance != null)
        {
            Debug.Log("[CharSelectUI] SetLocalPlayerIndex → 아직 미초기화, InitUI 재시도");
            InitUI(CharSelectManager.instance);
        }
    }

    private void Update() { HandleInput(); }

    private void HandleInput()
    {
        // 임시 진단 로그 (매 프레임 출력되므로 나중에 삭제)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            var mgr2 = CharSelectManager.instance;
            Debug.Log($"[Input진단] isInit={isInitialized} mgr={mgr2 != null} " +
                    $"localIdx={localPlayerIndex} netId={mgr2?.netId}");
        }

        if (!isInitialized) return;
        if (!isNetworkReady) return;
        var mgr = CharSelectManager.instance;
        if (mgr == null) return;
        if (localPlayerIndex < 0) return;
        // if (mgr.netId == 0) return;

        if (!isLocalConfirmed)
        {
            bool canMove = Time.time - lastInputTime > inputCooldown;
            bool moveLeft  = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)
                          || (canMove && (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)));
            bool moveRight = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)
                          || (canMove && (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)));

            if (moveLeft)
            {
                mgr.CmdMoveCursor(0);
                lastInputTime = Time.time;
                int cur = (localPlayerIndex == 0) ? mgr.p1CursorIndex : mgr.p2CursorIndex;
                RefreshLocalInfo(Mathf.Max(0, cur - 1));
            }
            else if (moveRight)
            {
                mgr.CmdMoveCursor(1);
                lastInputTime = Time.time;
                int cur = (localPlayerIndex == 0) ? mgr.p1CursorIndex : mgr.p2CursorIndex;
                RefreshLocalInfo(Mathf.Min(characters.Length - 1, cur + 1));
            }
        }

        if (!isLocalConfirmed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            mgr.CmdConfirmSelection();
            isLocalConfirmed = true;
        }

        if (isLocalConfirmed && Input.GetKeyDown(KeyCode.Escape))
        {
            mgr.CmdCancelSelection();
            isLocalConfirmed = false;
        }
    }

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
        bool isReady = selectedIndex >= 0;
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
            var go = Instantiate(thumbnailSlotPrefab, thumbnailContainer);
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