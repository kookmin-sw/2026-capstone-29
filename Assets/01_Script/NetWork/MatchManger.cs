using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MatchManager : NetworkManager
{
    [Header("Match Settings")]
    public string gameSceneName = "TPSTestScene"; 
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

    public override void OnStopClient()
    {
        // 게임 신에서 호스트의 의해 연결이 끊킨경우 씬이동 방지
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!NetworkServer.active && currentScene == gameSceneName)
        {
            isMatchStarted = false;
            // base 호출 안 함 → Mirror의 자동 오프라인 씬 이동 방지
            return;
        }

        base.OnStopClient();
        isMatchStarted = false; // 추가: 클라이언트로만 접속했다가 나올 때도 초기화
    }

    // 서버가 끊킨 경우 클라이언트가 게임 신에 유지되도록
    public override void OnClientDisconnect()
    {

        // 순수 클라이언트(서버가 아닌 쪽)가 연결 끊겼을 때
        if (!NetworkServer.active)
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == gameSceneName) // 게임 신인 경우에만 
            {
                // 게임오버 UI 강제 표시 (서버 연결 없이도 버튼 접근 가능하게)
                if (NetworkGameManger.instance != null)
                {
                    NetworkGameManger.instance.ForceShowDisconnectUI();
                }
            }
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
        Destroy(gameObject); // 게임 신 매니저 파괴

        yield return null;

        UnityEngine.SceneManagement.SceneManager.LoadScene("TitleMirror");
    }
}