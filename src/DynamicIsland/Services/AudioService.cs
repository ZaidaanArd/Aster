using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace DynamicIsland.Services;

public sealed class AudioService : IDisposable
{
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private AudioEndpointVolume? _volume;
    private bool _disposed;

    public int Volume { get; private set; } = 50;
    public bool IsMuted { get; private set; }
    public bool IsAvailable { get; private set; }

    public event Action<int, bool>? VolumeChanged;

    public AudioService()
    {
        TryInitialize();
    }

    private void TryInitialize()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _volume = _device.AudioEndpointVolume;
            _volume.OnVolumeNotification += OnVolumeNotification;
            Volume = (int)Math.Round(_volume.MasterVolumeLevelScalar * 100);
            IsMuted = _volume.Mute;
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Audio init failed: {ex.Message}");
        }
    }

    private void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        if (_disposed)
        {
            return;
        }

        Volume = (int)Math.Round(data.MasterVolume * 100);
        IsMuted = data.Muted;
        VolumeChanged?.Invoke(Volume, IsMuted);
    }

    public void Dispose()
    {
        _disposed = true;
        try
        {
            if (_volume is not null)
            {
                _volume.OnVolumeNotification -= OnVolumeNotification;
                _volume.Dispose();
            }

            _device?.Dispose();
            _enumerator?.Dispose();
        }
        catch
        {
            // Ignore COM teardown failures.
        }
    }
}
