using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public class ChatBotInitializationService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ChatBotInitializationService> _logger;

        public ChatBotInitializationService(IServiceProvider serviceProvider, ILogger<ChatBotInitializationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ChatBot Initialization Service starting...");

            // Start initialization in background without blocking application startup
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait a bit for other services to initialize
                    await Task.Delay(2000, cancellationToken);

                    using var scope = _serviceProvider.CreateScope();
                    var chatBotService = scope.ServiceProvider.GetRequiredService<ChatBotService>();

                    _logger.LogInformation("Starting ChatBot service initialization...");
                    await chatBotService.InitializeAsync();
                    _logger.LogInformation("ChatBot service initialized successfully during application startup");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize ChatBot service during application startup");
                }
            }, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ChatBot Initialization Service stopping...");
            return Task.CompletedTask;
        }
    }
}
