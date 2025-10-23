using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tutorly.Shared;
using Microsoft.Extensions.Options;
using Tutorly.Server.Helpers;
using SupabaseClient = Supabase.Client;

namespace Tutorly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatBotController : ControllerBase
    {
        private readonly ChatBotService _chatBotService;
        private readonly ILogger<ChatBotController> _logger;
        private readonly SupabaseClient _supabase;

        public ChatBotController(ChatBotService chatBotService, ILogger<ChatBotController> logger, SupabaseClient supabase)
        {
            _chatBotService = chatBotService;
            _logger = logger;
            _supabase = supabase;
        }

        // Force refresh of chatbot cache (admin only)
        [HttpPost("refresh-cache")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> RefreshCache()
        {
            try
            {
                _logger.LogInformation("Admin requested cache refresh...");
                
                // Force refresh in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _chatBotService.ForceRefreshAsync();
                        _logger.LogInformation("Cache refresh completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during cache refresh");
                    }
                });
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Cache refresh initiated. The system will process all documents including new ones from blob storage.",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating cache refresh");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to initiate cache refresh",
                    Data = null
                });
            }
        }

        // Initialize  chatbot service
        [HttpPost("initialize")]
        public ActionResult<ApiResponse<object>> InitializeChatBot()
        {
            try
            {
                _logger.LogInformation("Initializing chatbot service...");
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _chatBotService.InitializeAsync();
                        _logger.LogInformation("ChatBot service initialized successfully in background");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to initialize chatbot service in background");
                    }
                });
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "ChatBot service initialization started"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start chatbot service initialization");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to start chatbot service initialization"
                });
            }
        }

        // send message to chatbot and get  a context aware response
        [HttpPost("chat")]
        public async Task<ActionResult<ApiResponse<ChatBotResponse>>> SendMessage([FromBody] ChatBotRequest request)
        {
            try
            {
                _logger.LogInformation("ChatBot SendMessage endpoint called");
                
                if (request == null)
                {
                    _logger.LogWarning("Request is null");
                    return BadRequest(new ApiResponse<ChatBotResponse>
                    {
                        Success = false,
                        Message = "Request body is required"
                    });
                }

                // Validate JWT token using Supabase
                var userId = await ValidateTokenAndGetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User not authenticated");
                    return Unauthorized(new ApiResponse<ChatBotResponse> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                _logger.LogInformation($"User ID: {userId}");
                
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    _logger.LogWarning("Message is empty");
                    return BadRequest(new ApiResponse<ChatBotResponse>
                    {
                        Success = false,
                        Message = "Message cannot be empty"
                    });
                }

                _logger.LogInformation($"User {userId} sent message: {request.Message.Substring(0, Math.Min(50, request.Message.Length))}...");

                // Check if chatbot service is initialized
                _logger.LogInformation($"ChatBot service initialization status: {_chatBotService.IsInitialized}");
                if (!_chatBotService.IsInitialized)
                {
                    _logger.LogInformation("ChatBot service is not yet initialized - returning initialization message");
                    return Ok(new ApiResponse<ChatBotResponse>
                    {
                        Success = true,
                        Message = "ChatBot is initializing and will be ready shortly",
                        Data = new ChatBotResponse
                        {
                            Success = true,
                            Response = "Tutorly is currently initializing...\n\n" +
                                      "I am loading all the study materials and preparing to help you with your questions. " +
                                      "This will only take a moment!\n\n" +
                                      "Please wait about 30-60 seconds and then try asking your question again.\n\n" +
                                      "Once I am ready, I will be able to help you with:\n" +
                                      "📚 Module information and study guides\n" +
                                      "🎓 Course materials and resources\n" +
                                      "👨‍🏫 Tutor booking assistance\n" +
                                      "❓ General academic questions\n\n" +
                                      "Thank you for your patience! 😊",
                            ShouldEscalate = false,
                            ConfidenceScore = 1.0f,
                            Query = request.Message
                        }
                    });
                }

                var response = await _chatBotService.GenerateResponseAsync(request.Message, request.MaxDocuments ?? 3);
                _logger.LogInformation($"ChatBot service response - Success: {response.Success}, ShouldEscalate: {response.ShouldEscalate}, Confidence: {response.ConfidenceScore:F2}");

                if (response.Success == false)
                {
                    _logger.LogWarning($"ChatBot service failed: {response.ErrorMessage}");
                    return BadRequest(new ApiResponse<ChatBotResponse>
                    {
                        Success = false,
                        Message = response.ErrorMessage,
                        Data = response
                    });
                }

                // Handle smart escalation if needed
                if (response.ShouldEscalate)
                {
                    _logger.LogInformation($"Smart escalation triggered - Reason: {response.EscalationReason}");
                    
                    // Automatically escalate to appropriate tutors
                    await HandleSmartEscalationAsync(request.Message, userId, response);
                }

                _logger.LogInformation("ChatBot response generated successfully");
                return Ok(new ApiResponse<ChatBotResponse>
                {
                    Success = true,
                    Message = "Response generated successfully",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot message");
                return StatusCode(500, new ApiResponse<ChatBotResponse>
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("welcome")]
        public ActionResult<ApiResponse<string>> GetWelcomeMessage()
        {
            try
            {
                var welcomeMessage = "Hello! I am Tutorly, your AI learning assistant. I am here to help you with any queries regarding:\n \n" +
                                   "📚 Application Features - Learn how to navigate and use Tutorly \n" +
                                   "🎓 Modules & Courses - Get information about available subjects and modules \n" +
                                   "👨‍🏫 Tutor Bookings - Help with scheduling and finding tutors \n" +
                                   "📖 Study Materials - Access resources and study guides \n" +
                                   "❓ General Questions - Answer any academic or platform-related questions\n \n" +
                                   "💡Frequently Asked Questions:\n" +
                                   "• How do I book a tutor session?\n" +
                                   "• How do I access my modules?\n" +
                                   "• How do I join a study room?\n" +
                                   "• How do I post in the forum?\n" +
                                   "• What subjects are available for tutoring?\n" +
                                   "• How do I find study materials?\n \n" +
                                   "Feel free to ask me anything!";

                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Message = "Welcome message retrieved successfully",
                    Data = welcomeMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting welcome message");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "Failed to get welcome message"
                });
            }
        }

        // Escalate a query to tutors or admin
        [HttpPost("escalate")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<EscalationResponseDto>>> EscalateQuery([FromBody] EscalationRequestDto request)
        {
            try
            {
                _logger.LogInformation("ChatBot EscalateQuery endpoint called");
                
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User not authenticated");
                    return Unauthorized(new ApiResponse<EscalationResponseDto> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    _logger.LogWarning("Query is empty");
                    return BadRequest(new ApiResponse<EscalationResponseDto>
                    {
                        Success = false,
                        Message = "Query cannot be empty"
                    });
                }

                _logger.LogInformation($"User {userId} escalating query: {request.Query.Substring(0, Math.Min(50, request.Query.Length))}...");

                // Determine escalation type based on module information
                var escalationType = DetermineEscalationType(request);
                
                var response = await ProcessEscalationAsync(request, userId, escalationType);
                
                if (!response.Success)
                {
                    _logger.LogWarning($"Escalation failed: {response.Message}");
                    return BadRequest(new ApiResponse<EscalationResponseDto>
                    {
                        Success = false,
                        Message = response.Message,
                        Data = response
                    });
                }

                _logger.LogInformation("Escalation processed successfully");
                return Ok(new ApiResponse<EscalationResponseDto>
                {
                    Success = true,
                    Message = "Escalation processed successfully",
                    Data = response
                    //Message = 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing escalation");
                return StatusCode(500, new ApiResponse<EscalationResponseDto>
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        // Get chatbot status and health check
        [HttpGet("status")]
        public ActionResult<ApiResponse<object>> GetStatus()
        {
            try
            {
                var isReady = _chatBotService.IsInitialized;
                var status = isReady ? "Ready" : "Initializing";
                var message = isReady ? "ChatBot service is ready to help!" : "ChatBot service is still initializing";

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = message,
                    Data = new { 
                        Status = status, 
                        IsReady = isReady,
                        Timestamp = DateTime.UtcNow,
                        EstimatedWaitTime = isReady ? "0 seconds" : "30-60 seconds"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking chatbot status");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Service unavailable"
                });
            }
        }

        private EscalationType DetermineEscalationType(EscalationRequestDto request)
        {
            // If module information is provided, escalate to module tutors
            if (request.ModuleId.HasValue || !string.IsNullOrWhiteSpace(request.ModuleName))
            {
                return EscalationType.ModuleTutor;
            }

            // Check if the query contains module-related keywords
            var moduleKeywords = new[] { "module", "course", "subject", "assignment", "homework", "exam", "quiz", "test" , "summative", "lecture" };
            var queryLower = request.Query.ToLower();
            
            if (moduleKeywords.Any(keyword => queryLower.Contains(keyword)))
            {
                return EscalationType.ModuleTutor;
            }

            return EscalationType.Admin; // default
        }

        private async Task<EscalationResponseDto> ProcessEscalationAsync(EscalationRequestDto request, string userId, EscalationType escalationType)
        {
            try
            {
                if (escalationType == EscalationType.ModuleTutor)
                {
                    return await ProcessModuleTutorEscalationAsync(request, userId);
                }
                else
                {
                    return await ProcessAdminEscalationAsync(request, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing escalation");
                return new EscalationResponseDto
                {
                    Success = false,
                    Message = "Failed to process escalation",
                    EscalationType = escalationType
                };
            }
        }

        private async Task<EscalationResponseDto> ProcessModuleTutorEscalationAsync(EscalationRequestDto request, string userId)
        {
            try
            {
                _logger.LogInformation($"Processing module tutor escalation for user {userId}");
                
                // Try to determine module from query or context
                var moduleId = await DetermineModuleFromQueryAsync(request.Query, userId);
                
                if (moduleId.HasValue)
                {
                    // Find tutors for the specific module
                    var tutors = await GetTutorsForModuleAsync(moduleId.Value);
                    
                    if (tutors.Any())
                    {
                        // Create conversation with the first available tutor
                        var tutor = tutors.First();
                        var conversation = await CreateEscalationConversationAsync(userId, tutor.UserId, request.Query);
                        
                        if (conversation.Success)
                        {
                            return new EscalationResponseDto
                            {
                                Success = true,
                                Message = $"Your query has been escalated to {tutor.FullName}, a tutor for this module. They will respond shortly.",
                                EscalationType = EscalationType.ModuleTutor,
                                AvailableTutors = tutors,
                                Conversation = conversation.Data
                            };
                        }
                    }
                }
                
                // Fallback to admin escalation if no module tutors found
                return await ProcessAdminEscalationAsync(request, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing module tutor escalation");
                return new EscalationResponseDto
                {
                    Success = false,
                    Message = "Failed to escalate to module tutors. Escalating to administrators instead.",
                    EscalationType = EscalationType.Admin
                };
            }
        }

        private async Task<EscalationResponseDto> ProcessAdminEscalationAsync(EscalationRequestDto request, string userId)
        {
            // TODO: Implement admin escalation logic
            // This would involve:
            // 1. Finding available admins
            // 2. Creating a conversation with admins
            // 3. Sending the escalated query as initial message
            
            return new EscalationResponseDto
            {
                Success = true,
                Message = "Your query has been escalated to administrators. They will respond shortly.",
                EscalationType = EscalationType.Admin
            };
        }

        private async Task<string?> ValidateTokenAndGetUserId()
        {
            try
            {
                // Get the Authorization header
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    _logger.LogWarning("No valid Authorization header found");
                    return null;
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                _logger.LogInformation($"Validating token: {token.Substring(0, Math.Min(20, token.Length))}...");

                // Use Supabase client to validate the token
                var user = await _supabase.Auth.GetUser(token);
                if (user == null)
                {
                    _logger.LogWarning("Token validation failed - user is null");
                    return null;
                }

                _logger.LogInformation($"Token validated successfully for user: {user.Id}");
                return user.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return null;
            }
        }

        private string? GetCurrentUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                        User.FindFirst("sub")?.Value;
            
            _logger.LogInformation($"GetCurrentUserId: Found user ID: {userId}");
            _logger.LogInformation($"GetCurrentUserId: User authenticated: {User.Identity?.IsAuthenticated}");
            _logger.LogInformation($"GetCurrentUserId: User name: {User.Identity?.Name}");
            
            return userId;
        }

        // Handle smart escalation automatically triggered by low confidence responses
        private async Task HandleSmartEscalationAsync(string query, string userId, ChatBotResponse response)
        {
            try
            {
                _logger.LogInformation($"Handling smart escalation for user {userId}");
                
                var escalationRequest = new EscalationRequestDto
                {
                    Query = query,
                    EscalationType = EscalationType.ModuleTutor,
                    AdditionalContext = $"Confidence: {response.ConfidenceScore:F2}, FAQ Suggestions: {string.Join(", ", response.FaqSuggestions)}"
                };

                var escalationResponse = await ProcessEscalationAsync(escalationRequest, userId, EscalationType.ModuleTutor);
                
                if (escalationResponse.Success)
                {
                    _logger.LogInformation($"Smart escalation successful for user {userId}");
                }
                else
                {
                    _logger.LogWarning($"Smart escalation failed for user {userId}: {escalationResponse.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling smart escalation for user {userId}");
            }
        }

        // Determine module from query content
        private async Task<int?> DetermineModuleFromQueryAsync(string query, string userId)
        {
            try
            {
                // This is a simplified implementation
                // In a real system, you might use NLP to extract module information
                // or check user's enrolled modules
                
                var moduleKeywords = new Dictionary<string, int>
                {
                    ["math"] = 1,
                    ["mathematics"] = 1,
                    ["calculus"] = 1,
                    ["algebra"] = 1,
                    ["programming"] = 2,
                    ["python"] = 2,
                    ["java"] = 2,
                    ["c#"] = 2,
                    ["C#"] = 2
                };

                var queryLower = query.ToLower();
                foreach (var keyword in moduleKeywords)
                {
                    if (queryLower.Contains(keyword.Key))
                    {
                        return keyword.Value;
                    }
                }

                return null; // Could not determine module
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining module from query");
                return null;
            }
        }

        // Get tutors for a specific module
        private async Task<List<TutorDto>> GetTutorsForModuleAsync(int moduleId)
        {
            try
            {
                // This would typically call the ModuleController's GetModuleTutors endpoint
                // For now, return mock data
                return new List<TutorDto>
                {
                   
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tutors for module {moduleId}");
                return new List<TutorDto>();
            }
        }

        // Create escalation conversation
        private async Task<ApiResponse<ConversationDto>> CreateEscalationConversationAsync(string studentUserId, string tutorUserId, string query)
        {
            try
            {
                // This would use the MessagingService to create a conversation
                // For now, return a mock response
                return new ApiResponse<ConversationDto>
                {
                    Success = true,
                    Data = new ConversationDto
                    {
                        ConversationId = 1,
                        ConversationType = ConversationType.Direct,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating escalation conversation");
                return new ApiResponse<ConversationDto>
                {
                    Success = false,
                    Message = "Failed to create conversation"
                };
            }
        }
    }

}
