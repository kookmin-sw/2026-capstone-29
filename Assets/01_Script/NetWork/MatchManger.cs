using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MatchManager : NetworkManager
{
    [Header("Match Settings")]
    public string gameSceneName = "GameScene"; 
    public float sceneChangeDelay = 1.5f; // 매칭 성공 후 대기 시간
    private bool isMatchStarted = false;

    // 1. 서버에 새로운 클라이언트가 '연결'되었을 때 호출 (캐릭터 생성 전)
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);

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
        Debug.Log("게임 씬으로 이동합니다.");
        ServerChangeScene(gameSceneName);
    }

    // 2. 게임 씬으로 완전히 전환된 후 서버에서 실행됨
    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);

        // 게임 씬일 때만 실행
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == gameSceneName)
        {
            if (conn.identity != null) return;

            Transform startPos = GetStartPosition();
            Vector3 pos = startPos ? startPos.position : Vector3.zero;
            Quaternion rot = startPos ? startPos.rotation : Quaternion.identity;

            GameObject player = Instantiate(playerPrefab, pos, rot);
            NetworkServer.AddPlayerForConnection(conn, player);
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
}