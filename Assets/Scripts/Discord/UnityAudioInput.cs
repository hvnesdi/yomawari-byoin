using System;
using UnityEngine;

public class UnityAudioInput : IAudioInput
{
    public event Action<float> OnVolumeChanged;

    private AudioClip micClip;
    private string micDevice;
    private float[] samples = new float[256];
    private float updateInterval = 0.1f;
    private float timer;

    public void Start()
    {
        if (Microphone.devices.Length == 0) return;
        micDevice = Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, 1, AudioSettings.outputSampleRate);
    }

    public void Update()
    {
        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;

        if (micClip == null) return;

        int pos = Microphone.GetPosition(micDevice);
        if (pos < samples.Length) return;

        micClip.GetData(samples, pos - samples.Length);
        float rms = 0f;
        foreach (float s in samples) rms += s * s;
        rms = Mathf.Sqrt(rms / samples.Length);

        OnVolumeChanged?.Invoke(rms);
    }

    public void Dispose()
    {
        if (!string.IsNullOrEmpty(micDevice))
            Microphone.End(micDevice);
    }
}
