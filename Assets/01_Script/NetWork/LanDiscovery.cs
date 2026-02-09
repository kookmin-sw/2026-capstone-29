using Mirror;
using Mirror.Discovery;
using System;
using UnityEngine;

// 브로드캐스트 시 전달할 정보
public struct DiscoveryResponse : NetworkMessage
{
    public System.Uri uri;
    public long serverId;
    public int currentPlayers;
    public int maxPlayers;
}

public class LanDiscovery : NetworkDiscoveryBase<ServerRequest, DiscoveryResponse>
{
    public static long ServerId { get; private set; }

    [Tooltip("최대 플레이어 수")]
    public int maxPlayers = 2;

    public override void Start()
    {
        ServerId = RandomLong();
        base.Start();
    }

    // Host가 브로드캐스트 요청을 받았을 때 응답 생성
    protected override DiscoveryResponse ProcessRequest(ServerRequest request, System.Net.IPEndPoint endpoint)
    {
        return new DiscoveryResponse
        {
            uri = transport.ServerUri(),
            serverId = ServerId,
            currentPlayers = NetworkServer.connections.Count,
            maxPlayers = maxPlayers
        };
    }

    // Client가 응답을 받았을 때 처리
    protected override ServerRequest GetRequest()
    {
        return new ServerRequest();
    }

    protected override void ProcessResponse(DiscoveryResponse response, System.Net.IPEndPoint endpoint)
    {
        // URI에 실제 IP 반영
        UriBuilder realUri = new UriBuilder(response.uri)
        {
            Host = endpoint.Address.ToString()
        };
        response.uri = realUri.Uri;

        OnServerFound?.Invoke(response);
    }

    public event System.Action<DiscoveryResponse> OnServerFound;

}