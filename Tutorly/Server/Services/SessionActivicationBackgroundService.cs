using Tutorly.Server.Services;

namespace Tutorly.Server.Services;

public class SessionActivationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionActivationBackgroundService> _logger;

    public SessionActivationBackgroundService(IServiceProvider serviceProvider, ILogger<SessionActivationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session Activation Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionManagementService>();

                var result = await sessionService.CheckAndActivateScheduledSessionsAsync();

                if (result.Success)
                {
                    _logger.LogInformation("Session activation check completed: {Message}", result.Message);
                }
                else
                {
                    _logger.LogWarning("Session activation check failed: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during session activation check");
            }

            // Wait 5 minutes before next check
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("Session Activation Background Service stopped");
    }
}
