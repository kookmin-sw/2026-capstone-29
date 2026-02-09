using Mirror;
using UnityEngine;
using System.Collections;
public class AutoReadyRoomManager : NetworkRoomManager
{
    public override void OnRoomClientConnect()
    {
        base.OnRoomClientConnect();
    }

    // Room에 입장했을 때 자동으로 Ready
    public override void OnRoomClientEnter()
    {
        base.OnRoomClientEnter();
        StartCoroutine(AutoReady());
    }

    IEnumerator AutoReady()
    {
        yield return new WaitForSeconds(0.5f);

        // 로컬 RoomPlayer를 찾아서 자동 Ready
        foreach (var player in roomSlots)
        {
            if (player != null && player.isLocalPlayer && !player.readyToBegin)
            {
                player.CmdChangeReadyState(true);
                Debug.Log("자동으로 Ready 상태로 전환");
                break;
            }
        }
    }

    // 모든 플레이어가 Ready되면 자동으로 게임 시작
    public override void OnRoomServerPlayersReady()
    {
        // 원하는 조건 추가 가능 (예: 최소 인원 체크)
        base.OnRoomServerPlayersReady(); // 이게 GameScene으로 전환시킴
    }
}