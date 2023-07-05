using System;
using Proyecto26;
using RSG;
using UnityEngine;

[Serializable]
class ServerResponse
{
    public string ip;
    public ushort port;
}

public class MatchMaking
{
    public string baseUrl = "https://game-coordinator-fxy2zritya-uc.a.run.app/";
    public bool useBackendInEditor = true;

    public IPromise<ServerTransferData> CreateServer()
    {
        if (ShouldMockConnection())
        {
            return new Promise<ServerTransferData>((resolve, reject) =>
            {
                resolve(new ServerTransferData()
                {
                    address = "localhost",
                    port = 7770,
                });
            });
        }

        return RestClient.Post<ServerResponse>($"{baseUrl}/custom-servers/allocate", "{}").Then(response => new ServerTransferData()
        {
            address = response.ip,
            port = response.port,
        });
    }

    public IPromise<ServerTransferData> FindServerFromJoinCode(string joinCode)
    {
        if (ShouldMockConnection())
        {
            return new Promise<ServerTransferData>((resolve, reject) =>
            {
                resolve(new ServerTransferData()
                {
                    address = "localhost",
                    port = 7770,
                });
            });
        }

        return RestClient.Get<ServerResponse>($"{baseUrl}/custom-servers/gameId/bedwars/code/" + joinCode).Then(response => new ServerTransferData()
        {
            address = response.ip,
            port = response.port,
        });
    }

    private bool ShouldMockConnection()
    {
        return RunCore.IsEditor() && !useBackendInEditor;
    }
}