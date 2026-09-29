namespace TripPlanner.Api.Features.FavoriteDestinations;

public sealed partial class FavoriteDestinationImportWorker(
    IServiceScopeFactory scopeFactory,
    FavoriteDestinationImportSignal signal,
    ILogger<FavoriteDestinationImportWorker> logger) : BackgroundService
{
    // Picks up imports queued on other replicas and ones whose processing lease expired.
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = false;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<FavoriteDestinationImportProcessor>();
                claimed = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogPassFailed(exception.GetType().Name);
            }

            if (!claimed)
            {
                await signal.WaitAsync(IdlePollInterval, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Favorite import worker pass failed. Error type: {ErrorType}")]
    private partial void LogPassFailed(string errorType);
}
