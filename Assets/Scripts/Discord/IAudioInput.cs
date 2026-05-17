using System;

public interface IAudioInput : IDisposable
{
    event Action<float> OnVolumeChanged;
    void Start();
    void Update();
}
