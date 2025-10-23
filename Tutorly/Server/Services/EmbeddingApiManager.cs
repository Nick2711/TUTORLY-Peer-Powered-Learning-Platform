using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    /// <summary>
    /// Hosted service that manages the lifecycle of the Python embedding API
    /// </summary>
    public class EmbeddingApiManager : BackgroundService
    {
        private readonly IEmbeddingApiService _embeddingService;
        private readonly ILogger<EmbeddingApiManager> _logger;

        public EmbeddingApiManager(
            IEmbeddingApiService embeddingService,
            ILogger<EmbeddingApiManager> logger)
        {
            _embeddingService = embeddingService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("EmbeddingApiManager starting...");
                
                // Only start the embedding API if it's not already running
                await _embeddingService.InitializeAsync();
                
                _logger.LogInformation("EmbeddingApiManager started successfully");
                
                // Keep the service running until cancellation is requested
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Check if the embedding API is still running every 30 seconds
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    
                    try
                    {
                        // Test if the API is still responding
                        await _embeddingService.GetEmbeddingAsync("health check");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Embedding API health check failed, attempting to restart...");
                        
                        // Try to restart the API
                        try
                        {
                            await _embeddingService.StopAsync();
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Wait longer for cleanup
                            await _embeddingService.InitializeAsync(); // Use InitializeAsync instead of StartAsync
                            _logger.LogInformation("Embedding API restarted successfully");
                        }
                        catch (Exception restartEx)
                        {
                            _logger.LogError(restartEx, "Failed to restart embedding API");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("EmbeddingApiManager is stopping due to cancellation request");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingApiManager encountered an error");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("EmbeddingApiManager stopping...");
                
                // Stop the embedding API
                await _embeddingService.StopAsync();
                
                _logger.LogInformation("EmbeddingApiManager stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping EmbeddingApiManager");
            }
            finally
            {
                await base.StopAsync(cancellationToken);
            }
        }
    }
}
