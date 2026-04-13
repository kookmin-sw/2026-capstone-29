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

    [Header("HealthBar Setting")]
    public Image p1CurrentBar;
    public Image p1DelayBar;
    public Image p2CurrentBar;
    public Image p2DelayBar;

    [Header("HealthBar Anim")]
    public float delayTime = 0.5f; // 닳기 시작까지 대기 시간
    public float drainSpeed = 1.0f; // 닳는 속도

    public Coroutine p1DrainCoroutine;
    public Coroutine p2DrainCoroutine;

    private StarterAssetsInputs inputs;
    private PlayerInput playerInput;

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
