using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public struct CharSelectInputMessage : NetworkMessage
{
    public int action;    // 0=왼쪽, 1=오른쪽, 2=확정, 3=취소
    public int connId;    // 보내는 클라이언트의 connId (서버에서 식별용)
}