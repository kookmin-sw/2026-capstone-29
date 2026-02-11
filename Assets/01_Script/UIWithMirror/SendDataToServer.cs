using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SendDataToServer : NetworkBehaviour // 플레이어에 부착되어 현재 플레이어가 속한 클라이언트가 호스트 여부인지 점검하여, 그에 맞춰 IngameData에 적절하게 전달해주는 함수들을 모아둠.
{
    private GameObject player;
    private bool isHost;
    private GameObject GM;
    private IngameData IGD;
    void Start()
    {
        isHost = isServer && isLocalPlayer;
        GM = GameObject.FindWithTag("GameManager");
        IGD = GM.GetComponent<IngameData>();
    }

    public void UpdateCurHealth(int curHealth)
    {
        IGD.CmdChangeCurHealth(isHost, curHealth);
    }

    public void UpdateScore()
    {
        IGD.CmdAddScore(!isHost); // 현재 Player에 부착된 PlayerHealth의 TakeDamage에서 호출되는 것을 상정하여, 체력이 0이 된 경우 상대방에게 점수를 올려주기 위한 조치.
    }
    
}
