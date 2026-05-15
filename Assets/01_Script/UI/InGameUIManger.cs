using System;
using System.Collections;
using System.Collections.Generic;
using kcp2k;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InGameUIManger : MonoBehaviour
{
    [SerializeField] private GameObject settingPanel; // 게임 설정 패널
    
    [Header("UI Setting")]
    public GameObject KeySettingPanel; // 키세팅 패널
    public GameObject ExitPanel; // 나가기 패널

    [Header("Character Thumbnail")]
    public Image p1CharacterThumbnail;
    public Image p2CharacterThumbnail;
    public CharacterData[] characters;

    [Header("HealthBar Setting")]
    public Image p1CurrentBar;
    public Image p1DelayBar;
    public Image p2CurrentBar;
    public Image p2DelayBar;

    [Header("Lives UI Setting")]
    public Image[] p1LifeIcons;
    public Image[] p2LifeIcons;
    public Sprite lifeActiveSprite; // 남은 목숨
    public Sprite lifeDeadSprite; // 죽은 목숨

    [Header("Object UI")]
    public Image weaponItemIcon; // 무기 아이콘 - 계속 변경됨
    public Image weaponItemTimerFill; // 지속시간 fill
    public Text weaponItemCountText;
    public Sprite defaultWeaponSprite; // 기본 주무기 이미지
    public Image activeItemIcon;
    // public Sprite defaultActiveSprite; // 빈 슬롯 이미지

    [Header("Passive UI")]
    [SerializeField] private Transform passiveItemRoot;
    [SerializeField] private PassiveItemUISlot passiveItemSlotPrefab;

    [Header("Field UI")]
    [SerializeField] private Transform fieldItemRoot;
    [SerializeField] private FieldItemUISlot fieldItemSlotPrefab;

    private readonly Dictionary<int, PassiveItemUISlot> passiveItemSlots = new Dictionary<int, PassiveItemUISlot>();
    private readonly Dictionary<int, Coroutine> passiveTimerCoroutines = new Dictionary<int, Coroutine>();
    private readonly Dictionary<uint, FieldItemUISlot> fieldItemSlots = new();
    private readonly Dictionary<uint, Coroutine> fieldItemCoroutines = new();


    [Header("HealthBar Anim")]
    public float delayTime = 0.5f; // 닳기 시작까지 대기 시간
    public float drainSpeed = 1.0f; // 닳는 속도

    public Coroutine p1DrainCoroutine;
    public Coroutine p2DrainCoroutine;

    private StarterAssetsInputs inputs;
    private PlayerInput playerInput;
    private Coroutine weaponItemCoroutine;
    private Coroutine activeItemCoroutine;

    private void Awake()
    {
        SetActiveItemIconVisible(false);
    }

    // 게임 매니저에서 RegisterPlayer에서 호출 - p1, p2 구분하여 체력바 UI 등록
    public void RegisterHealthBar(int playerIndex, float initialHealth) 
    {
        float fill = initialHealth / 100f; 

        if(playerIndex == 1) 
        {
            if(p1CurrentBar != null) p1CurrentBar.fillAmount = fill;
            if(p1DelayBar != null) p1DelayBar.fillAmount = fill;
        }
        else 
        {
            if(p2CurrentBar != null) p2CurrentBar.fillAmount = fill;
            if(p2DelayBar != null) p2DelayBar.fillAmount = fill;
        }
    }

    // 각 플레이어 캐릭터 썸네일 등록
    public void SetCharacterThumbnail(int playerIndex, int characterIndex)
    {
        if (characters == null || characterIndex < 0 || characterIndex >= characters.Length) return;

        Sprite sprite = characters[characterIndex].inGameThumbnail;
        if (sprite == null) sprite = characters[characterIndex].thumbnail;

        Image target = playerIndex == 1 ? p1CharacterThumbnail : p2CharacterThumbnail;
        if (target != null) target.sprite = sprite;
    }

    // 각 플레이어 목숨 초기 등록
    public void RegisterLives(int playerIndex, int maxLives, int currentLives)
    {
        Image[] icons = playerIndex == 1 ? p1LifeIcons : p2LifeIcons;
        if(icons == null) return;
        UpdateLivesUI(icons, currentLives);
    }


    // 게임 매니저 OnHealhChanged에 연결해 이용 - 체력 변경
    public void OnP1HealthChanged(float newHealth)
    {
        float fill = newHealth / 100f;
        if (p1CurrentBar != null) p1CurrentBar.fillAmount = fill;

        if (p1DrainCoroutine != null) StopCoroutine(p1DrainCoroutine);
        p1DrainCoroutine = StartCoroutine(DrainDelayed(p1DelayBar, fill));
    }

    public void OnP2HealthChanged(float newHealth)
    {
        float fill = newHealth / 100f;
        if (p2CurrentBar) p2CurrentBar.fillAmount = fill;

        if (p2DrainCoroutine != null) StopCoroutine(p2DrainCoroutine);
        p2DrainCoroutine = StartCoroutine(DrainDelayed(p2DelayBar, fill));
    }

    // 목숨 변경 시 콜백 함수
    public void OnP1LivesChanged(int newLives)
    {
        UpdateLivesUI(p1LifeIcons, newLives);
    }

    public void OnP2LivesChanged(int newLives)
    {
        UpdateLivesUI(p2LifeIcons, newLives);
    }

    // 목숨 변경 시 UI 반영
    private void UpdateLivesUI(Image[] icons, int remainingLives)
    {
        if (icons == null) return;
        
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null) continue;
            // 인덱스가 남은 목숨 수보다 작으면 활성(살아있는 하트), 아니면 소진
            icons[i].sprite = i < remainingLives ? lifeActiveSprite : lifeDeadSprite;
        }
    }


    // 닳는 효과 코루틴
    private IEnumerator DrainDelayed(Image bar, float targetFill) 
    {
        yield return new WaitForSeconds(delayTime);

        while(bar != null && bar.fillAmount > targetFill) 
        {
            bar.fillAmount -= Time.deltaTime * drainSpeed;
            yield return null;
        }

        if(bar != null) bar.fillAmount = targetFill;
    }

    public void RegisterInput(StarterAssetsInputs input)
    {
        inputs = input;
        playerInput = input.GetComponent<PlayerInput>();
        Debug.Log("UIManager 등록 완료");
    }

    private void Update()
    {
        if(inputs == null) return;

        // ESC누른 경우 - 설정 UI 닫거나 열거나
        if(inputs.pause || (settingPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)))
        {
            Debug.Log("ESC 확인");
            bool isActive = settingPanel.activeSelf; // 활성화 여부
            settingPanel.SetActive(!isActive); // 누를 때마다 반대로 작동

            // 커서 처리
            inputs.cursorLocked = isActive;
            Cursor.lockState = isActive ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isActive;

            // 플레이어 입력 방지
            if(playerInput != null)
                playerInput.enabled = isActive;

            // esc로 닫을 때 다른 UI 모두 닫기
            if(isActive)
            {
                KeySettingPanel.SetActive(false);
                ExitPanel.SetActive(false);
            }

            inputs.pause = false; // 소비 처리
        }
    }

    // 아이템 무기를 획득했을 때 호출됨
    public void ShowWeaponItem(Sprite itemSprite, float duration)
    {
        if(weaponItemCoroutine != null)
            StopCoroutine(weaponItemCoroutine);

        SetWeaponItemCountVisible(false);

        if(weaponItemIcon != null && itemSprite != null)
            weaponItemIcon.sprite = itemSprite; 
        
        weaponItemCoroutine = StartCoroutine(WeaponItemTimerCoroutine(duration));
    }

    public void ShowWeaponItemCount(Sprite itemSprite, int count)
    {
        if (weaponItemCoroutine != null)
        {
            StopCoroutine(weaponItemCoroutine);
            weaponItemCoroutine = null;
        }

        if (weaponItemTimerFill != null)
            weaponItemTimerFill.fillAmount = 0f;

        if (weaponItemIcon != null && itemSprite != null)
            weaponItemIcon.sprite = itemSprite;

        UpdateWeaponItemCount(count);
    }

    public void UpdateWeaponItemCount(int count)
    {
        if (count <= 0)
        {
            HideWeaponItem();
            return;
        }

        EnsureWeaponItemCountText();
        if (weaponItemCountText == null) return;

        weaponItemCountText.text = count.ToString();
        SetWeaponItemCountVisible(true);
    }

    // 액티브 아이템 획득 시 호출
    public void ShowActiveItem(Sprite itemSprite)
    {
        if (activeItemIcon == null) return;

        if (itemSprite == null)
        {
            HideActiveItem();
            return;
        }

        activeItemIcon.sprite = itemSprite;
        SetActiveItemIconVisible(true);
    }

    // 패시브 아이템 획득 시 호출
    public void ShowPassiveItem(int uiId, Sprite sprite, PassiveUIType uiType, float duration)
    {
        if (passiveItemRoot == null || passiveItemSlotPrefab == null) return;

        if (passiveItemSlots.TryGetValue(uiId, out PassiveItemUISlot oldSlot) && oldSlot != null)
            Destroy(oldSlot.gameObject);

        PassiveItemUISlot slot = Instantiate(passiveItemSlotPrefab, passiveItemRoot);
        bool useTimer = uiType == PassiveUIType.TimedSpeed;

        slot.Initialize(sprite, useTimer);
        slot.transform.SetAsLastSibling();

        passiveItemSlots[uiId] = slot;

        if (passiveTimerCoroutines.TryGetValue(uiId, out Coroutine oldRoutine))
        {
            StopCoroutine(oldRoutine);
            passiveTimerCoroutines.Remove(uiId);
        }

        if (useTimer)
        {
            Coroutine routine = StartCoroutine(PassiveItemTimerCoroutine(uiId, duration));
            passiveTimerCoroutines[uiId] = routine;
        }
    }

    // 필드 아이템 획득 시 호출
    public void ShowFieldItem(uint uiId, Sprite sprite, float duration)
    {
        if (fieldItemRoot == null || fieldItemSlotPrefab == null) return;

        HideFieldItem(uiId);

        FieldItemUISlot slot = Instantiate(fieldItemSlotPrefab, fieldItemRoot);
        slot.Initialize(sprite);
        slot.transform.SetAsLastSibling();

        fieldItemSlots[uiId] = slot;
        fieldItemCoroutines[uiId] = StartCoroutine(FieldItemTimerCoroutine(uiId, duration));
    }


    public void UpdatePassiveItemTimer(int uiId, float normalized)
    {
        if (!passiveItemSlots.TryGetValue(uiId, out PassiveItemUISlot slot)) return;
        if (slot == null) return;

        slot.SetTimer(normalized);
    }

    // 액티브 아이템 사용 시 호출
    public void HideActiveItem()
    {
        if (activeItemIcon == null) return;
        activeItemIcon.sprite = null;
        SetActiveItemIconVisible(false);
    }

    private void SetActiveItemIconVisible(bool visible)
    {
        if (activeItemIcon != null)
            activeItemIcon.gameObject.SetActive(visible);
    }

    public void HideWeaponItem()
    {
        if (weaponItemCoroutine != null)
        {
            StopCoroutine(weaponItemCoroutine);
            weaponItemCoroutine = null;
        }

        if (weaponItemTimerFill != null)
            weaponItemTimerFill.fillAmount = 0f;

        SetWeaponItemCountVisible(false);

        if (weaponItemIcon != null && defaultWeaponSprite != null)
            weaponItemIcon.sprite = defaultWeaponSprite;
    }

    public void HidePassiveItem(int uiId)
    {
        if (!passiveItemSlots.TryGetValue(uiId, out PassiveItemUISlot slot)) return;

        if (slot != null)
            Destroy(slot.gameObject);

        passiveItemSlots.Remove(uiId);

        if (passiveTimerCoroutines.TryGetValue(uiId, out Coroutine routine))
        {
            StopCoroutine(routine);
            passiveTimerCoroutines.Remove(uiId);
        }
    }

    public void HideFieldItem(uint uiId)
    {
        if (fieldItemCoroutines.TryGetValue(uiId, out Coroutine routine))
        {
            StopCoroutine(routine);
            fieldItemCoroutines.Remove(uiId);
        }

        if (fieldItemSlots.TryGetValue(uiId, out FieldItemUISlot slot) && slot != null)
            Destroy(slot.gameObject);

        fieldItemSlots.Remove(uiId);
    }

    // 아이템 타이머 코루틴
    private IEnumerator WeaponItemTimerCoroutine(float duration)
    {
        float elapsed = 0f;

        // fill을 0에서 시작해서 1까지 채워나감
        if (weaponItemTimerFill != null)
            weaponItemTimerFill.fillAmount = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (weaponItemTimerFill != null)
                weaponItemTimerFill.fillAmount = elapsed / duration;
            yield return null;
        }

        // 지속시간 끝 → 원래 무기 아이콘으로 복귀
        if (weaponItemTimerFill != null)
            weaponItemTimerFill.fillAmount = 0f;

        if (weaponItemIcon != null && defaultWeaponSprite != null)
            weaponItemIcon.sprite = defaultWeaponSprite;


        weaponItemCoroutine = null;
    }

    private void SetWeaponItemCountVisible(bool visible)
    {
        if (weaponItemCountText != null)
            weaponItemCountText.gameObject.SetActive(visible);
    }

    private void EnsureWeaponItemCountText()
    {
        if (weaponItemCountText != null) return;
        if (weaponItemIcon == null) return;

        GameObject countObj = new GameObject("WeaponItemCountText", typeof(RectTransform));
        countObj.transform.SetParent(weaponItemIcon.transform, false);

        RectTransform rect = countObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.65f, 0f);
        rect.anchorMax = new Vector2(1f, 0.35f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        weaponItemCountText = countObj.AddComponent<Text>();
        weaponItemCountText.alignment = TextAnchor.MiddleCenter;
        weaponItemCountText.color = Color.white;
        weaponItemCountText.fontSize = 24;
        weaponItemCountText.raycastTarget = false;
        weaponItemCountText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        weaponItemCountText.gameObject.SetActive(false);
    }

    // 패시브 아이템 타이머 코루틴
    private IEnumerator PassiveItemTimerCoroutine(int uiId, float duration)
    {
        if (duration <= 0f) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalized = 1f - elapsed / duration;
            UpdatePassiveItemTimer(uiId, normalized);

            yield return null;
        }

        UpdatePassiveItemTimer(uiId, 0f);
    }

    private IEnumerator FieldItemTimerCoroutine(uint uiId, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = 1f - elapsed / duration;

            if (fieldItemSlots.TryGetValue(uiId, out FieldItemUISlot slot))
                slot.SetTimer(normalized);

            yield return null;
        }

        HideFieldItem(uiId);
    }

    // 키세팅 버튼 클릭 시 - ui 등장
    public void ClickKeySetting()
    {
        if(KeySettingPanel != null) 
            KeySettingPanel.SetActive(true);
    }

    // 키세팅 버튼 닫을 시 - ui 닫기
    public void CancleKeySetting()
    {
        if(KeySettingPanel != null) 
            KeySettingPanel.SetActive(false);
    }

    // 게임 나가기 버튼 클릭 시 - ui 등장
    public void ClickExit()
    {
        // if(settingPanel != null) settingPanel.SetActive(false);
        if(ExitPanel != null) ExitPanel.SetActive(true);
    }
    
    // 게임 나가기 취소시
    public void CancleExit()
    {
        // if(settingPanel != null) settingPanel.SetActive(true);
        if(ExitPanel != null) ExitPanel.SetActive(false);
    }
}
