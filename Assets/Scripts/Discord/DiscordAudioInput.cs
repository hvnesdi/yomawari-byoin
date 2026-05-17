using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Receives speaking events from the discord_bot WebSocket server.
public class DiscordAudioInput : IAudioInput
{
    public event Action<float> OnVolumeChanged;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private float pendingVolume = -1f;

    public void Start()
    {
        cts = new CancellationTokenSource();
        ws = new ClientWebSocket();
        ConnectAsync();
    }

    private async void ConnectAsync()
    {
        try
        {
            await ws.ConnectAsync(new Uri("ws://localhost:8765"), cts.Token);
            _ = ReceiveLoopAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiscordAudioInput] WebSocket connect failed: {e.Message}");
        }
    }

    private async System.Threading.Tasks.Task ReceiveLoopAsync()
    {
        var buffer = new byte[1024];
        while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
        {
            try
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ParseMessage(json);
            }
            catch { break; }
        }
    }

    private void ParseMessage(string json)
    {
        // Minimal parse: {"type":"voice_state","speaking":true}
        bool speaking = json.Contains("\"speaking\":true");
        pendingVolume = speaking ? 0.9f : 0f;
    }

    public void Update()
    {
        if (pendingVolume >= 0f)
        {
            OnVolumeChanged?.Invoke(pendingVolume);
            pendingVolume = -1f;
        }
    }

    public void Dispose()
    {
        cts?.Cancel();
        ws?.Dispose();
    }
}
