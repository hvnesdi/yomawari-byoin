using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Steamworks.NET types - conditionally compiled so project builds without the library
#if STEAMWORKS_NET
using Steamworks;
#endif

/// <summary>
/// Steam P2P NetworkManager using Steamworks.NET.
/// CLAUDE.md: 同期対象 = 位置・アニメーション・インタラクション・エンド確定状態
///            非同期（個別）= 幻覚レベル・フラグ・見えているもの
/// </summary>
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    public bool IsHost { get; private set; }
    public bool IsConnected { get; private set; }

    // Message type identifiers
    private const int Channel = 0;
    private enum MsgType : byte
    {
        PlayerPosition  = 1,
        PlayerAwakened  = 2,
        GameStart       = 3,
        EndingDecided   = 4,
        Interaction     = 5,
    }

#if STEAMWORKS_NET
    private List<CSteamID> remotePlayers = new();
    private Callback<P2PSessionRequest_t> p2pSessionRequest;
    private Callback<LobbyCreated_t> lobbyCreated;
    private Callback<LobbyEnter_t> lobbyEntered;
    private Callback<GameLobbyJoinRequested_t> joinRequested;
#endif

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
#if STEAMWORKS_NET
        if (!SteamManager.Initialized) { Debug.LogWarning("[Net] Steam not initialized"); return; }

        p2pSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        lobbyCreated      = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyEntered      = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        joinRequested     = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);

        string localID = SteamUser.GetSteamID().ToString();
        PlayerManager.Instance?.SetLocalPlayerID(localID);
#else
        Debug.Log("[Net] Steamworks.NET not available – running in offline mode");
#endif
    }

    void Update()
    {
#if STEAMWORKS_NET
        ReceiveMessages();
#endif
    }

    // ── Host ──────────────────────────────────────────────────────
    public void CreateLobby(int maxPlayers = 4)
    {
#if STEAMWORKS_NET
        IsHost = true;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
#else
        Debug.Log("[Net] CreateLobby (offline stub)");
        IsConnected = true;
        GameManager.Instance?.StartGame();
#endif
    }

    // ── Client ────────────────────────────────────────────────────
    public void JoinLobby(string lobbyIDStr)
    {
#if STEAMWORKS_NET
        if (ulong.TryParse(lobbyIDStr, out ulong id))
            SteamMatchmaking.JoinLobby(new CSteamID(id));
#endif
    }

    // ── Send helpers ──────────────────────────────────────────────
    public void BroadcastPosition(Vector3 pos)
    {
#if STEAMWORKS_NET
        var buf = BuildMsg(MsgType.PlayerPosition, pos.x, pos.y, pos.z);
        Broadcast(buf);
#endif
    }

    public void BroadcastAwakened(bool awakened)
    {
#if STEAMWORKS_NET
        var buf = BuildMsg(MsgType.PlayerAwakened, awakened ? 1f : 0f);
        Broadcast(buf);
#endif
    }

    public void BroadcastEnding(int endingIndex)
    {
#if STEAMWORKS_NET
        var buf = BuildMsg(MsgType.EndingDecided, (float)endingIndex);
        Broadcast(buf);
#endif
    }

    // ── Internal ──────────────────────────────────────────────────
#if STEAMWORKS_NET
    void Broadcast(byte[] buf)
    {
        foreach (var id in remotePlayers)
            SteamNetworking.SendP2PPacket(id, buf, (uint)buf.Length, EP2PSend.k_EP2PSendReliable, Channel);
    }

    byte[] BuildMsg(MsgType type, params float[] floats)
    {
        var localID = SteamUser.GetSteamID().m_SteamID;
        var bytes = new byte[1 + 8 + floats.Length * 4];
        bytes[0] = (byte)type;
        Buffer.BlockCopy(BitConverter.GetBytes(localID), 0, bytes, 1, 8);
        for (int i = 0; i < floats.Length; i++)
            Buffer.BlockCopy(BitConverter.GetBytes(floats[i]), 0, bytes, 9 + i * 4, 4);
        return bytes;
    }

    void ReceiveMessages()
    {
        while (SteamNetworking.IsP2PPacketAvailable(out uint size, Channel))
        {
            var buf = new byte[size];
            if (!SteamNetworking.ReadP2PPacket(buf, size, out _, out CSteamID sender, Channel)) continue;

            string senderID = sender.ToString();
            PlayerManager.Instance?.RegisterPlayer(senderID);

            var type   = (MsgType)buf[0];
            ulong rawID = BitConverter.ToUInt64(buf, 1);

            switch (type)
            {
                case MsgType.PlayerPosition:
                    float x = BitConverter.ToSingle(buf, 9);
                    float y = BitConverter.ToSingle(buf, 13);
                    float z = BitConverter.ToSingle(buf, 17);
                    PlayerManager.Instance?.UpdatePlayerPosition(senderID, new Vector3(x, y, z));
                    break;

                case MsgType.PlayerAwakened:
                    bool awk = BitConverter.ToSingle(buf, 9) > 0.5f;
                    PlayerManager.Instance?.SetPlayerAwakened(senderID, awk);
                    break;

                case MsgType.EndingDecided:
                    // Trigger shared ending screen if all players decided
                    if (PlayerManager.Instance?.AllPlayersHaveEnding() == true)
                        EndingSystem.Instance?.EvaluateAndTrigger();
                    break;
            }
        }
    }

    void OnP2PSessionRequest(P2PSessionRequest_t req)
    {
        SteamNetworking.AcceptP2PSessionWithUser(req.m_steamIDRemote);
        if (!remotePlayers.Contains(req.m_steamIDRemote))
            remotePlayers.Add(req.m_steamIDRemote);
        PlayerManager.Instance?.RegisterPlayer(req.m_steamIDRemote.ToString());
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (cb.m_eResult != EResult.k_EResultOK) { Debug.LogError("[Net] Lobby creation failed"); return; }
        IsConnected = true;
        Debug.Log($"[Net] Lobby created: {cb.m_ulSteamIDLobby}");
        GameManager.Instance?.StartGame();
    }

    void OnLobbyEntered(LobbyEnter_t cb)
    {
        IsConnected = true;
        var lobbyID = new CSteamID(cb.m_ulSteamIDLobby);
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
        for (int i = 0; i < count; i++)
        {
            var memberID = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
            if (memberID != SteamUser.GetSteamID() && !remotePlayers.Contains(memberID))
                remotePlayers.Add(memberID);
        }
        Debug.Log($"[Net] Joined lobby, {remotePlayers.Count} remote players");
    }

    void OnJoinRequested(GameLobbyJoinRequested_t cb)
        => SteamMatchmaking.JoinLobby(cb.m_steamIDLobby);
#endif
}
