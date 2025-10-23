using System.Diagnostics;
using System.Net.Sockets;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    /// <summary>
    /// Service to automatically start and manage the Python embedding API
    /// </summary>
    public class EmbeddingApiService : IEmbeddingApiService, IDisposable
    {
        private Process? _pythonProcess;
        private readonly ILogger<EmbeddingApiService> _logger;
        private readonly IConfiguration _configuration;
        private bool _disposed = false;
        private static readonly object _lockObject = new object();
        private static bool _isStarting = false;

        public EmbeddingApiService(ILogger<EmbeddingApiService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task StartAsync()
        {
            lock (_lockObject)
            {
                if (_isStarting)
                {
                    _logger.LogInformation("Python embedding API is already starting, skipping...");
                    return;
                }
                _isStarting = true;
            }

            try
            {
                _logger.LogInformation("Starting Python embedding API...");

                // Check if we already have a running process
                if (_pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _logger.LogInformation("Python embedding API is already running");
                    return;
                }

                // Check if port 8000 is already in use and kill existing process
                if (await IsPortInUseAsync(8000))
                {
                    _logger.LogWarning("Port 8000 is already in use. Attempting to kill existing process...");
                    await KillProcessOnPortAsync(8000);
                    await Task.Delay(3000); // Wait longer for port to be released
                    
                    // Check again after killing processes
                    if (await IsPortInUseAsync(8000))
                    {
                        _logger.LogError("Port 8000 is still in use after attempting to kill processes. Cannot start embedding API.");
                        return;
                    }
                }

                // Check if Python is available
                var pythonPath = await FindPythonPathAsync();
                if (string.IsNullOrEmpty(pythonPath))
                {
                    _logger.LogError("Python not found. Please install Python and ensure it's in your PATH.");
                    return;
                }

                // Check if embedding_api.py exists (it's in the Shared directory)
                var embeddingApiPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "", "Shared", "embedding_api.py");
                if (!File.Exists(embeddingApiPath))
                {
                    _logger.LogError($"embedding_api.py not found at: {embeddingApiPath}");
                    return;
                }

                // Start the Python process
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{embeddingApiPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "", "Shared")
                };

                _pythonProcess = new Process { StartInfo = startInfo };
                _pythonProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogInformation($"Embedding API: {e.Data}");
                };
                _pythonProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogError($"Embedding API Error: {e.Data}");
                };

                _pythonProcess.Start();
                
                // Only call BeginOutputReadLine if the process started successfully
                if (!_pythonProcess.HasExited)
                {
                    _pythonProcess.BeginOutputReadLine();
                    _pythonProcess.BeginErrorReadLine();
                }

                // Wait a moment for the API to start
                await Task.Delay(2000);

                // Test if the API is responding
                if (await TestEmbeddingApiAsync())
                {
                    _logger.LogInformation("✅ Embedding API started successfully on http://localhost:8000");
                }
                else
                {
                    _logger.LogWarning("⚠️ Embedding API may not be responding correctly");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start embedding API");
            }
            finally
            {
                lock (_lockObject)
                {
                    _isStarting = false;
                }
            }
        }

        public async Task StopAsync()
        {
            try
            {
                if (_pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _logger.LogInformation("Stopping embedding API...");
                    
                    // Try graceful shutdown first
                    try
                    {
                        _pythonProcess.CloseMainWindow();
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await _pythonProcess.WaitForExitAsync(cts.Token);
                    }
                    catch
                    {
                        // If graceful shutdown fails, force kill
                        _logger.LogWarning("Graceful shutdown failed, forcing termination...");
                        _pythonProcess.Kill();
                        await _pythonProcess.WaitForExitAsync();
                    }
                    
                    _pythonProcess.Dispose();
                    _pythonProcess = null;
                    _logger.LogInformation("Embedding API stopped successfully");
                }
                else if (_pythonProcess != null)
                {
                    _logger.LogInformation("Embedding API process was already stopped");
                    _pythonProcess.Dispose();
                    _pythonProcess = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping embedding API");
                
                // Force cleanup even if there was an error
                try
                {
                    _pythonProcess?.Dispose();
                    _pythonProcess = null;
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        public async Task InitializeAsync()
        {
            // Start the embedding API if it's not already running
            if (_pythonProcess == null || _pythonProcess.HasExited)
            {
                await StartAsync();
            }
            else
            {
                _logger.LogInformation("Embedding API is already running, skipping initialization");
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                var response = await httpClient.PostAsJsonAsync("http://localhost:8000/embed", new { text });
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<float>>>();
                if (result == null || !result.ContainsKey("embedding"))
                    throw new Exception("Embedding API returned invalid data.");
                    
                return result["embedding"].ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting embedding from API");
                throw;
            }
        }

        private async Task<string?> FindPythonPathAsync()
        {
            var possiblePaths = new[]
            {
                "python",
                "python3",
                "py",
                @"C:\Python39\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python312\python.exe"
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation($"Found Python at: {path}");
                        return path;
                    }
                }
                catch
                {
                    // Continue to next path
                }
            }

            return null;
        }

        private async Task<bool> TestEmbeddingApiAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync("http://localhost:8000/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsPortInUseAsync(int port)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync("localhost", port);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task KillProcessOnPortAsync(int port)
        {
            try
            {
                // Use PowerShell to find and kill processes using the port
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"Get-NetTCPConnection -LocalPort {port} -ErrorAction SilentlyContinue | ForEach-Object {{ $_.OwningProcess }} | Sort-Object -Unique | ForEach-Object {{ Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully killed processes using port {Port}", port);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to kill processes using port {Port}. Error: {Error}", port, error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error killing process on port {Port}", port);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    try
                    {
                        if (_pythonProcess != null && !_pythonProcess.HasExited)
                        {
                            _logger.LogInformation("Disposing EmbeddingApiService - stopping Python process");
                            _pythonProcess.Kill();
                            _pythonProcess.WaitForExit(5000); // Wait up to 5 seconds
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during disposal of Python process");
                    }
                    finally
                    {
                        _pythonProcess?.Dispose();
                        _pythonProcess = null;
                    }
                }
                _disposed = true;
            }
        }

        ~EmbeddingApiService()
        {
            Dispose(false);
        }
    }
}
