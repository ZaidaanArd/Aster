using Aster.Core.Interfaces;
using Avalonia.Threading;

namespace Aster.Avalonia.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
