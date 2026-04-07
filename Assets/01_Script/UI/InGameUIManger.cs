using System;
using System.Collections;
using System.Collections.Generic;
using kcp2k;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

public class InGameUIManger : MonoBehaviour
{
    [SerializeField] private GameObject settingPanel; // 게임 설정 패널
    
    [Header("UI Setting")]
    public GameObject KeySettingPanel; // 키세팅 패널
    public GameObject ExitPanel; // 나가기 패널

    private StarterAssetsInputs inputs;
    private PlayerInput playerInput;

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
