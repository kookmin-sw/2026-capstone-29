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

    // ── 연출용 레퍼런스 ──────────────────────────────────────────
    [Header("매치 스타트 연출")]
    [Tooltip("P1 초상화의 RectTransform (슬라이드 애니메이션 대상)")]
    public RectTransform p1PortraitRect;
    [Tooltip("P2 초상화의 RectTransform (슬라이드 애니메이션 대상)")]
    public RectTransform p2PortraitRect;
    [Tooltip("VS 텍스트 오브젝트 (평소엔 비활성)")]
    public GameObject vsPanel;
    [Tooltip("캐릭터 이름 표시 패널 등 선택 UI 루트 — 연출 시 숨길 오브젝트들")]
    public GameObject[] selectionOnlyObjects;   // 썸네일 컨테이너, 캐릭터 설명 등
    [Tooltip("슬라이드 이동 거리 (픽셀). 화면 해상도에 맞게 조정)")]
    public float slideOffscreenX = 900f;
    [Tooltip("슬라이드 후 P1 초상화의 최종 X 위치 (anchoredPosition). 중앙 기준 음수 = 왼쪽)")]
    public float p1SlideTargetX = -300f;
    [Tooltip("슬라이드 후 P2 초상화의 최종 X 위치 (anchoredPosition). 중앙 기준 양수 = 오른쪽)")]
    public float p2SlideTargetX = 300f;
    [Tooltip("초상화 슬라이드 소요 시간 (초)")]
    public float slideDuration = 0.45f;
    [Tooltip("VS 팝업 연출 소요 시간 (초)")]
    public float vsPopDuration = 0.3f;

    [Header("로딩 패널")]
    public GameObject loadingPanel;


    [Header("입력 쿨다운")]
    public float inputCooldown = 0.18f;

    private RectTransform[] thumbnailSlots;
    private CharacterData[] characters;
    private float lastInputTime;
    private bool isLocalConfirmed = false;
    private int localPlayerIndex = -1;
    private bool isInitialized = false;
    private bool isNetworkReady = false;

    // ── 연출 중에는 입력 차단 플래그 ────────────────────────────
    private bool isCinematicPlaying = false;

    private void Awake()
    {
        instance = this;
        Debug.Log("[CharSelectUI] Awake");
    }

    private void Start()
    {
        Debug.Log("[CharSelectUI] Start - CharSelectManager 대기 시작");
    }

    // ── 네트워크 콜백 ─────────────────────────────────────────────

    public void OnNetworkReady()
    {
        Debug.Log($"[CharSelectUI] OnNetworkReady - netId:{CharSelectManager.instance?.netId}");
        isNetworkReady = true;
        if (!isInitialized && CharSelectManager.instance != null)
            InitUI(CharSelectManager.instance);
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

    public void SetLocalPlayerIndex(int index)
    {
        localPlayerIndex = index;
        isNetworkReady = true;
        Debug.Log($"[CharSelectUI] SetLocalPlayerIndex = {index}");
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
        if (vsPanel) vsPanel.SetActive(false);
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

    // ── Update ────────────────────────────────────────────────────

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
        if (!isNetworkReady)
        {
            if (NetworkServer.active || NetworkClient.isConnected)
                isNetworkReady = true;
            else
                return;
        }
        Debug.Log("[CharSelectUI] TryInit 통과");
        InitUI(mgr);
        if (localPlayerIndex < 0)
            ResolveLocalPlayerIndex();
    }

    private void HandleInput()
    {
        if (!isInitialized) return;
        if (localPlayerIndex < 0) return;
        if (isCinematicPlaying) return;     // 연출 중 입력 차단

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

        // 연출 중에는 뱃지를 절대 다시 켜지 않음
        // (클라이언트에서 SyncVar 훅이 ShowCountdown보다 늦게 도착해 뱃지를 되살리는 문제 방지)
        if (readyBadge != null && !isCinematicPlaying)
            readyBadge.SetActive(isReady);

        if (isReady && characters != null && selectedIndex < characters.Length)
            if (portrait != null && characters[selectedIndex].portrait != null)
                portrait.sprite = characters[selectedIndex].portrait;
    }

    public void UpdateReadyState(int readyCount)
    {
        Debug.Log($"[CharSelectUI] 준비 완료: {readyCount}/2");
    }

    // ── 매치 스타트 연출 진입점 (CharSelectManager.RpcShowCountdown에서 호출) ──

    /// <summary>
    /// 두 플레이어 모두 레디 시 호출. 연출 → 카운트다운 순으로 진행됩니다.
    /// </summary>
    public void ShowCountdown()
    {
        StartCoroutine(MatchStartCinematic());
    }

    // ── 연출 코루틴 ───────────────────────────────────────────────

    private IEnumerator MatchStartCinematic()
    {
        isCinematicPlaying = true;

        // ── Step 1. 선택 UI 요소 즉시 숨기기 ──────────────────────
        HideSelectionUI();

        // 살짝 텀을 두어 숨김이 자연스럽게 느껴지도록
        yield return new WaitForSeconds(0.1f);

        // ── Step 2. 초상화를 화면 양쪽 바깥으로 초기 배치 ─────────
        // P1은 왼쪽 바깥, P2는 오른쪽 바깥에서 시작
        float p1OrigY = p1PortraitRect != null ? p1PortraitRect.anchoredPosition.y : 0f;
        float p2OrigY = p2PortraitRect != null ? p2PortraitRect.anchoredPosition.y : 0f;

        Vector2 p1From = new Vector2(-slideOffscreenX, p1OrigY);
        Vector2 p2From = new Vector2( slideOffscreenX, p2OrigY);
        Vector2 p1To   = new Vector2(p1SlideTargetX,  p1OrigY);
        Vector2 p2To   = new Vector2(p2SlideTargetX,  p2OrigY);

        if (p1PortraitRect != null) p1PortraitRect.anchoredPosition = p1From;
        if (p2PortraitRect != null) p2PortraitRect.anchoredPosition = p2From;

        // ── Step 3. 초상화 슬라이드인 ─────────────────────────────
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            // EaseOutCubic
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / slideDuration), 3f);

            if (p1PortraitRect != null) p1PortraitRect.anchoredPosition = Vector2.Lerp(p1From, p1To, t);
            if (p2PortraitRect != null) p2PortraitRect.anchoredPosition = Vector2.Lerp(p2From, p2To, t);

            yield return null;
        }

        // 최종 위치 확정
        if (p1PortraitRect != null) p1PortraitRect.anchoredPosition = p1To;
        if (p2PortraitRect != null) p2PortraitRect.anchoredPosition = p2To;

        // ── Step 4. VS 팝업 ────────────────────────────────────────
        if (vsPanel != null)
        {
            vsPanel.SetActive(true);

            // 스케일 0 → 1 팝업 효과
            var vsRect = vsPanel.GetComponent<RectTransform>();
            if (vsRect != null)
            {
                elapsed = 0f;
                while (elapsed < vsPopDuration)
                {
                    elapsed += Time.deltaTime;
                    // EaseOutBack
                    float t  = Mathf.Clamp01(elapsed / vsPopDuration);
                    float s  = EaseOutBack(t);
                    vsRect.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
                vsRect.localScale = Vector3.one;
            }
        }

        yield return new WaitForSeconds(0.4f);

        // ── Step 5. 로딩 패널 등장
        yield return new WaitForSeconds(1.5f); // 잠시 대기

        // countdownPanel 대신 loadingPanel 활성화
        if (loadingPanel != null) loadingPanel.SetActive(true);

    }

    // EaseOutBack 수식 (overshooting 팝업 느낌)
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>
    /// 썸네일 컨테이너, 커서 인디케이터, 캐릭터 설명 등 선택 단계 전용 UI를 숨깁니다.
    /// </summary>
    private void HideSelectionUI()
    {
        // 썸네일 컨테이너
        if (thumbnailContainer != null)
            thumbnailContainer.gameObject.SetActive(false);

        // 커서 인디케이터
        if (p1CursorIndicator != null) p1CursorIndicator.gameObject.SetActive(false);
        if (p2CursorIndicator != null) p2CursorIndicator.gameObject.SetActive(false);

        // 레디 뱃지
        if (p1ReadyBadge != null) p1ReadyBadge.SetActive(false);
        if (p2ReadyBadge != null) p2ReadyBadge.SetActive(false);

        // 캐릭터 이름 / 설명 텍스트
        if (characterNameText != null) characterNameText.gameObject.SetActive(false);
        if (characterDescText  != null) characterDescText.gameObject.SetActive(false);

        // Inspector에서 추가로 지정한 오브젝트들
        if (selectionOnlyObjects != null)
            foreach (var go in selectionOnlyObjects)
                if (go != null) go.SetActive(false);

        Debug.Log("[CharSelectUI] 선택 UI 비활성화 완료");
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

            // 카운트 숫자마다 팝 스케일 연출
            if (countdownPanel != null)
                StartCoroutine(PopScale(countdownPanel.GetComponent<RectTransform>()));

            yield return new WaitForSeconds(step == "FIGHT!" ? 0.8f : 1f);
        }
    }

    /// <summary>
    /// 대상 RectTransform을 순간적으로 1.3배 → 1.0배로 줄이는 팝 효과
    /// </summary>
    private IEnumerator PopScale(RectTransform target, float duration = 0.15f)
    {
        if (target == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.Lerp(1.3f, 1f, t);
            target.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        target.localScale = Vector3.one;
    }
}