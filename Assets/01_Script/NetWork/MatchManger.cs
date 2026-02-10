using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MatchManager : NetworkManager
{
    [Header("Match Settings")]
    public string gameSceneName = "TPSTestScene"; 
    public float sceneChangeDelay = 3.0f; // 매칭 성공 후 대기 시간
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
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        // 게임 씬에 도착했을 때만 캐릭터를 소환
        if (sceneName == gameSceneName)
        {
            Debug.Log("게임 씬 도착: 플레이어 캐릭터 소환을 시작합니다.");
            
            foreach (var conn in NetworkServer.connections.Values)
            {
                // 이미 플레이어가 할당되어 있지 않은 연결에 대해서만 생성
                if (conn.identity == null)
                {
                    GameObject player = Instantiate(playerPrefab);
                    // 이 함수가 실행되어야 클라이언트 화면에 캐릭터가 나타나고 권한이 부여됨
                    NetworkServer.AddPlayerForConnection(conn, player);
                }
            }
        }
    }

    // 서버가 중단될 때 플래그 초기화
    public override void OnStopServer()
    {
        base.OnStopServer();
        isMatchStarted = false;
    }
}