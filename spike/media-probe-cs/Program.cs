using Windows.Media.Control;

var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
var session = manager.GetCurrentSession();

if (session is null)
{
    Console.WriteLine("NO_SESSION");
    return;
}

var props = await session.TryGetMediaPropertiesAsync();
Console.WriteLine($"{props.Title}\t{props.Artist}\t{props.AlbumTitle}");
