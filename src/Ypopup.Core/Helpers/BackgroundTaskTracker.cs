using Ypopup.Core.Logging;

namespace Ypopup.Core.Helpers;

public static class BackgroundTaskTracker
{
    public static Task RunAsync(
        string operationName,
        Func<Task> taskFactory,
        Action<string, Exception>? onError = null)
    {
        return Task.Run(async () =>
        {
            try
            {
                await taskFactory().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.Error("BackgroundTaskTracker", $"[{operationName}] {ex.Message}");
                onError?.Invoke(operationName, ex);
            }
        });
    }
}