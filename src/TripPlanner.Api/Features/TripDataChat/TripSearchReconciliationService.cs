namespace TripPlanner.Api.Features.TripDataChat;

public sealed partial class TripSearchReconciliationService(
    IServiceScopeFactory scopeFactory,
    ILogger<TripSearchReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var indexer = scope.ServiceProvider.GetRequiredService<ITripSearchIndexer>();
                await indexer.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                LogReconciliationPassFailed();
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trip chat reconciliation pass failed; content was omitted.")]
    private partial void LogReconciliationPassFailed();
}