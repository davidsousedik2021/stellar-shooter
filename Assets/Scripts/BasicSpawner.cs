using UnityEngine;
using Fusion;
using Fusion.Sockets;
using TMPro;
using System.Collections.Generic;
using System;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;
    public NetworkPrefabRef playerPrefab;
    public Transform[] spawnPoints;
    public TMP_Text regionText;
    private int _spawnIndex = 0;

    async void Start()
    {
        if (regionText != null)
            regionText.text = "Region: connecting...";

        _runner = GetComponent<NetworkRunner>();
        if (_runner == null) _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = "KovbojRoom",
            Scene = SceneRef.FromIndex(0),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (regionText != null)
        {
            regionText.text = result.Ok
                ? $"Region: {_runner.SessionInfo.Region}"
                : $"Connection failed: {result.ShutdownReason}";
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player != runner.LocalPlayer) return;

        Vector3 spawnPos = GetNextSpawnPoint();
        NetworkObject obj = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);

        if (obj.TryGetComponent<NetworkTransform>(out var nt))
            nt.Teleport(spawnPos);
    }

    public Vector3 GetNextSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Vector3 pos = spawnPoints[_spawnIndex % spawnPoints.Length].position;
            _spawnIndex++;
            return pos;
        }
        return new Vector3(
            UnityEngine.Random.Range(-8f, 8f),
            2f,
            UnityEngine.Random.Range(-8f, 8f));
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason disconnectReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
