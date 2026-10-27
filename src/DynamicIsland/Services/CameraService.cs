using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace DynamicIsland.Services;

public enum CameraState
{
    Idle,
    Starting,
    Live,
    NoCamera,
    AccessDenied,
    Busy,
    Error
}

public sealed record CameraStatus(CameraState State, string? DeviceName = null, string? Message = null);

public sealed record CameraFrame(byte[] Pixels, int Width, int Height, int Stride);

/// <summary>
/// WinRT MediaCapture preview for a small WPF surface. Frames are copied to
/// BGRA8 buffers on the capture thread and rendered by the view on the UI thread.
/// </summary>
public sealed class CameraService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private IReadOnlyList<MediaFrameSourceGroup> _groups = Array.Empty<MediaFrameSourceGroup>();
    private int _deviceIndex;
    private long _lastFrameTicks;
    private int _framePending;
    private volatile bool _running;
    private byte[]? _sourcePixels;
    private byte[]? _previewPixels;
    private Windows.Storage.Streams.Buffer? _transferBuffer;
    private bool _disposed;

    public CameraState State { get; private set; } = CameraState.Idle;
    public string? DeviceName { get; private set; }
    public int DeviceCount => _groups.Count;
    public bool IsLive => State == CameraState.Live;

    public event Action<CameraStatus>? StatusChanged;
    public event Action<CameraFrame>? FrameReady;

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed || _capture is not null)
            {
                return;
            }

            SetState(CameraState.Starting);

            _groups = (await MediaFrameSourceGroup.FindAllAsync())
                .Where(group => group.SourceInfos.Any(info => info.SourceKind == MediaFrameSourceKind.Color))
                .ToList();

            if (_groups.Count == 0)
            {
                SetState(CameraState.NoCamera, null, "No camera found");
                return;
            }

            if (_deviceIndex >= _groups.Count)
            {
                _deviceIndex = 0;
            }

            await StartGroupAsync(_groups[_deviceIndex]);
        }
        catch (Exception ex)
        {
            await CleanupAsync();
            HandleStartException(ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CleanupAsync();
            if (!_disposed)
            {
                SetState(CameraState.Idle);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SwitchCameraAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_groups.Count < 2)
            {
                return;
            }

            await CleanupAsync();
            _deviceIndex = (_deviceIndex + 1) % _groups.Count;
            SetState(CameraState.Starting);

            await StartGroupAsync(_groups[_deviceIndex]);
        }
        catch (Exception ex)
        {
            await CleanupAsync();
            HandleStartException(ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The view calls this after drawing a frame so the next one can be produced.</summary>
    public void MarkFrameRendered()
    {
        Interlocked.Exchange(ref _framePending, 0);
    }

    /// <summary>Captures a full-resolution photo while the preview is live.</summary>
    public async Task<string?> CapturePhotoAsync(string directory)
    {
        var capture = _capture;
        if (capture is null || !_running)
        {
            return null;
        }

        try
        {
            var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(directory);
            var file = await folder.CreateFileAsync(
                $"camera-{DateTime.Now:yyyyMMdd-HHmmss}.jpg",
                Windows.Storage.CreationCollisionOption.GenerateUniqueName);

            await capture.CapturePhotoToStorageFileAsync(
                Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg(),
                file);

            return file.Path;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"camera photo failed: {ex}");
            return null;
        }
    }

    private async Task StartGroupAsync(MediaFrameSourceGroup group)
    {
        var capture = new MediaCapture();
        _capture = capture;

        var settings = new MediaCaptureInitializationSettings
        {
            SourceGroup = group,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly
        };

        try
        {
            await capture.InitializeAsync(settings);
        }
        catch (Exception) when (settings.SharingMode == MediaCaptureSharingMode.SharedReadOnly)
        {
            // Some drivers do not support shared mode. Recreate the capture
            // before retrying with exclusive control.
            try
            {
                capture.Dispose();
            }
            catch
            {
                // Ignore a failed half-initialized capture.
            }

            capture = new MediaCapture();
            _capture = capture;
            settings.SharingMode = MediaCaptureSharingMode.ExclusiveControl;
            await capture.InitializeAsync(settings);
        }

        var source = capture.FrameSources.Values
            .FirstOrDefault(item => item.Info.SourceKind == MediaFrameSourceKind.Color);

        if (source is null)
        {
            throw new InvalidOperationException("No color camera source.");
        }

        var format = source.SupportedFormats
            .Where(item => item.VideoFormat is not null)
            .OrderBy(item =>
                Math.Abs((int)item.VideoFormat.Width - 640) +
                Math.Abs((int)item.VideoFormat.Height - 360))
            .FirstOrDefault();

        DebugLog.Write(
            "camera formats: " +
            string.Join(
                ", ",
                source.SupportedFormats
                    .Where(item => item.VideoFormat is not null)
                    .Select(item => $"{item.VideoFormat.Width}x{item.VideoFormat.Height}")
                    .Distinct()));

        DebugLog.Write(
            $"camera format chosen: {format?.VideoFormat?.Width}x{format?.VideoFormat?.Height}");

        if (format is not null)
        {
            try
            {
                await source.SetFormatAsync(format);
            }
            catch (Exception ex)
            {
                DebugLog.Write($"camera set format failed: {ex.Message}");
            }
        }

        MediaFrameReader reader;
        try
        {
            reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
        }
        catch
        {
            reader = await capture.CreateFrameReaderAsync(source);
        }

        _reader = reader;
        reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
        reader.FrameArrived += OnFrameArrived;

        _lastFrameTicks = 0;
        Interlocked.Exchange(ref _framePending, 0);

        await reader.StartAsync();

        _running = true;
        DeviceName = string.IsNullOrWhiteSpace(group.DisplayName) ? "Camera" : group.DisplayName;
        var resolution = format?.VideoFormat;
        var detail = resolution is null
            ? null
            : $"{resolution.Width} \u00D7 {resolution.Height}";

        SetState(CameraState.Live, DeviceName, detail);
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_disposed || !_running || State != CameraState.Live || Volatile.Read(ref _framePending) == 1)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (_lastFrameTicks != 0 && now - _lastFrameTicks < Stopwatch.Frequency / 15)
        {
            return;
        }

        _lastFrameTicks = now;

        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            var videoFrame = frame?.VideoMediaFrame;
            var bitmap = videoFrame?.SoftwareBitmap;
            if (bitmap is null)
            {
                return;
            }

            SoftwareBitmap? converted = null;
            try
            {
                if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    converted = SoftwareBitmap.Convert(
                        bitmap,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                    bitmap = converted;
                }

                var sourceWidth = bitmap.PixelWidth;
                var sourceHeight = bitmap.PixelHeight;
                var sourceStride = sourceWidth * 4;
                var sourceSize = sourceStride * sourceHeight;

                var sourcePixels = _sourcePixels;
                if (sourcePixels is null || sourcePixels.Length != sourceSize)
                {
                    sourcePixels = new byte[sourceSize];
                    _sourcePixels = sourcePixels;
                }

                var transferBuffer = _transferBuffer;
                if (transferBuffer is null || transferBuffer.Capacity < (uint)sourceSize)
                {
                    transferBuffer = new Windows.Storage.Streams.Buffer((uint)sourceSize);
                    _transferBuffer = transferBuffer;
                }

                bitmap.CopyToBuffer(transferBuffer);
                using var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(transferBuffer);
                dataReader.ReadBytes(sourcePixels);

                var targetWidth = Math.Min(640, sourceWidth);
                var targetHeight = Math.Max(
                    1,
                    (int)Math.Round(targetWidth * (double)sourceHeight / sourceWidth));
                var targetStride = targetWidth * 4;
                var targetSize = targetStride * targetHeight;

                var previewPixels = _previewPixels;
                if (previewPixels is null || previewPixels.Length != targetSize)
                {
                    previewPixels = new byte[targetSize];
                    _previewPixels = previewPixels;
                }

                Downscale(
                    sourcePixels,
                    sourceWidth,
                    sourceHeight,
                    sourceStride,
                    previewPixels,
                    targetWidth,
                    targetHeight,
                    targetStride);

                if (Interlocked.Exchange(ref _framePending, 1) == 1)
                {
                    return;
                }

                var handler = FrameReady;
                if (handler is null)
                {
                    Interlocked.Exchange(ref _framePending, 0);
                    return;
                }

                try
                {
                    handler(new CameraFrame(
                        previewPixels,
                        targetWidth,
                        targetHeight,
                        targetStride));
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"camera frame handler failed: {ex}");
                    Interlocked.Exchange(ref _framePending, 0);
                }
            }
            finally
            {
                converted?.Dispose();
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"camera frame failed: {ex}");
        }
    }

    private static void Downscale(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int sourceStride,
        byte[] target,
        int targetWidth,
        int targetHeight,
        int targetStride)
    {
        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
        {
            Buffer.BlockCopy(source, 0, target, 0, Math.Min(source.Length, target.Length));
            return;
        }

        var x0s = new int[targetWidth];
        var x1s = new int[targetWidth];
        var xfs = new float[targetWidth];
        var scaleX = (float)sourceWidth / targetWidth;

        for (var x = 0; x < targetWidth; x++)
        {
            var sx = (x + 0.5f) * scaleX - 0.5f;
            var x0 = (int)MathF.Floor(sx);
            if (x0 < 0)
            {
                x0 = 0;
                sx = 0;
            }

            x0s[x] = x0;
            x1s[x] = Math.Min(x0 + 1, sourceWidth - 1);
            xfs[x] = sx - x0;
        }

        var scaleY = (float)sourceHeight / targetHeight;

        for (var y = 0; y < targetHeight; y++)
        {
            var sy = (y + 0.5f) * scaleY - 0.5f;
            var y0 = (int)MathF.Floor(sy);
            if (y0 < 0)
            {
                y0 = 0;
                sy = 0;
            }

            var y1 = Math.Min(y0 + 1, sourceHeight - 1);
            var fy = sy - y0;
            var row0 = y0 * sourceStride;
            var row1 = y1 * sourceStride;
            var destRow = y * targetStride;

            for (var x = 0; x < targetWidth; x++)
            {
                var x0 = x0s[x];
                var x1 = x1s[x];
                var fx = xfs[x];

                var i00 = row0 + x0 * 4;
                var i01 = row0 + x1 * 4;
                var i10 = row1 + x0 * 4;
                var i11 = row1 + x1 * 4;

                var w00 = (1 - fx) * (1 - fy);
                var w01 = fx * (1 - fy);
                var w10 = (1 - fx) * fy;
                var w11 = fx * fy;

                var di = destRow + x * 4;

                target[di] = (byte)(
                    source[i00] * w00 +
                    source[i01] * w01 +
                    source[i10] * w10 +
                    source[i11] * w11);
                target[di + 1] = (byte)(
                    source[i00 + 1] * w00 +
                    source[i01 + 1] * w01 +
                    source[i10 + 1] * w10 +
                    source[i11 + 1] * w11);
                target[di + 2] = (byte)(
                    source[i00 + 2] * w00 +
                    source[i01 + 2] * w01 +
                    source[i10 + 2] * w10 +
                    source[i11 + 2] * w11);
                target[di + 3] = 255;
            }
        }
    }

    private async Task CleanupAsync()
    {
        _running = false;
        Interlocked.Exchange(ref _framePending, 0);

        var reader = _reader;
        _reader = null;

        if (reader is not null)
        {
            DebugLog.Write("camera stop: reader");
            reader.FrameArrived -= OnFrameArrived;
            try
            {
                await reader.StopAsync();
            }
            catch
            {
                // The reader may already be stopped.
            }

            reader.Dispose();
        }

        var capture = _capture;
        _capture = null;

        try
        {
            capture?.Dispose();
        }
        catch
        {
            // Disposal should never take down the app.
        }

        DeviceName = null;
        _sourcePixels = null;
        _previewPixels = null;
        _transferBuffer = null;
        DebugLog.Write("camera stop: done");
    }

    private void HandleStartException(Exception ex)
    {
        DebugLog.Write($"camera start failed: {ex}");

        if (ex is UnauthorizedAccessException)
        {
            SetState(CameraState.AccessDenied, null, "Camera access blocked");
            return;
        }

        if (ex is COMException com)
        {
            var code = (uint)com.HResult;
            if (code == 0x80070005)
            {
                SetState(CameraState.AccessDenied, null, "Camera access blocked");
                return;
            }

            if (code is 0x8007001F or 0x800700AA or 0x8007000E)
            {
                SetState(CameraState.Busy, null, "Camera is in use by another app");
                return;
            }
        }

        SetState(CameraState.Error, null, "Camera unavailable");
    }

    private void SetState(CameraState state, string? deviceName = null, string? message = null)
    {
        State = state;
        if (deviceName is not null)
        {
            DeviceName = deviceName;
        }

        StatusChanged?.Invoke(new CameraStatus(state, DeviceName, message));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Exchange(ref _framePending, 0);

        var reader = _reader;
        _reader = null;
        if (reader is not null)
        {
            reader.FrameArrived -= OnFrameArrived;
            try
            {
                reader.Dispose();
            }
            catch
            {
                // Ignore shutdown errors.
            }
        }

        try
        {
            _capture?.Dispose();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        _capture = null;
    }
}
