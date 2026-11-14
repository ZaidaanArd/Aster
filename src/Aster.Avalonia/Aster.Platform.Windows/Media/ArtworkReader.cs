using Windows.Storage.Streams;

namespace Aster.Platform.Windows.Media;

public static class ArtworkReader
{
    public const uint MaxBytes = 8 * 1024 * 1024;

    public static async Task<byte[]?> ReadAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0)
            {
                return null;
            }

            if (stream.Size > MaxBytes)
            {
                return null;
            }

            var size = (uint)stream.Size;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync(size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}
