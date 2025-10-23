using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using ProfanityFilter;
using Tutorly.Shared;

namespace Tutorly.Shared
{

    public class ChatBotService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _blobConnectionString;
        private readonly string _blobContainerName;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly Dictionary<string, float[]> _documentEmbeddings = new();
        private readonly Dictionary<string, string> _documentContent = new();
        private readonly IEmbeddingApiService? _embeddingService;
        private readonly Dictionary<string, string> _faqDatabase = new();
        private readonly string _embeddingsCachePath = "embeddings_cache.json";
        private readonly Dictionary<string, float[]> _queryEmbeddingCache = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        public ChatBotService(
            HttpClient httpClient,
            IConfiguration configuration,
            string blobConnectionString,
            string blobContainerName,
            IEmbeddingApiService? embeddingService = null)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _embeddingService = embeddingService;
            
            _blobConnectionString = blobConnectionString ?? _configuration["Azure: ConnString"];
            _blobContainerName = blobContainerName ?? _configuration["Azure: BlobContainerNameChat"];
            
            // Get HuggingFace API key and model
            _apiKey = _configuration["HuggingFace:ApiKey"] ?? "demo-key";
            _model = "meta-llama/Llama-3.1-8B"; // Force the correct model
            
            if (!string.IsNullOrEmpty(_apiKey) && _apiKey != "demo-key")
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        // Force refresh of embeddings cache (useful for admin operations)
        public async Task ForceRefreshAsync()
        {
            try
            {
                Console.WriteLine("Forcing cache refresh...");
                
                // Clear existing cache
                _documentEmbeddings.Clear();
                _documentContent.Clear();
                _queryEmbeddingCache.Clear();
                _lastCacheUpdate = DateTime.MinValue;
                
                // Delete cache file if it exists
                if (File.Exists(_embeddingsCachePath))
                {
                    File.Delete(_embeddingsCachePath);
                    Console.WriteLine("Deleted existing cache file");
                }
                
                // Reinitialize
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during force refresh: {ex.Message}");
            }
        }

        // Initialize chatbot service by loading embeddings from documents from chatbot-kb folder
        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("ChatBotService: Starting initialization...");
                
                // Initialize the embedding API service
                if (_embeddingService != null)
                {
                    Console.WriteLine("ChatBotService: Initializing embedding service...");
                    await _embeddingService.InitializeAsync();
                    
                    // Wait for embedding API to be ready
                    var maxRetries = 10;
                    var retryCount = 0;
                    while (retryCount < maxRetries)
                    {
                        try
                        {
                            // Test if embedding API is responding
                            await _embeddingService.GetEmbeddingAsync("test");
                            Console.WriteLine("ChatBotService: Embedding API is ready");
                            break;
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            Console.WriteLine($"ChatBotService: Embedding API not ready yet (attempt {retryCount}/{maxRetries}): {ex.Message}");
                            if (retryCount >= maxRetries)
                            {
                                Console.WriteLine("ChatBotService: Embedding API failed to start, using fallback");
                                break;
                            }
                            await Task.Delay(2000); // Wait 2 seconds before retry
                        }
                    }
                }

                // Try to load cached embeddings first, but check if blob storage has newer files
                var cacheLoaded = await LoadEmbeddingsCacheAsync();
                if (cacheLoaded && await IsCacheUpToDateAsync())
                {
                    Console.WriteLine("Using cached embeddings - no need to regenerate");
                    await LoadFaqDatabaseAsync();
                    _isInitialized = true;
                    Console.WriteLine("ChatBotService: Initialization completed successfully with cached data");
                    return;
                }
                else if (cacheLoaded)
                {
                    Console.WriteLine("Cache is outdated - blob storage has newer files, regenerating...");
                }

                // Check if Azure Blob Storage is configured
                if (string.IsNullOrEmpty(_blobConnectionString) || string.IsNullOrEmpty(_blobContainerName))
                {
                    Console.WriteLine("Azure Blob Storage not configured - using demo data");
                    await LoadDemoDataAsync();
                    await LoadFaqDatabaseAsync();
                    await SaveEmbeddingsCacheAsync(); // Save demo data cache
                    _isInitialized = true;
                    Console.WriteLine("ChatBotService: Initialization completed successfully with demo data");
                    return;
                }
                
                Console.WriteLine($"Connecting to Azure Blob Storage...");
                Console.WriteLine($"Container: {_blobContainerName}");
                
                // load demo data as backup
                await LoadDemoDataAsync();
                
                // Load FAQ database
                await LoadFaqDatabaseAsync();

                var containerClient = new BlobContainerClient(_blobConnectionString, _blobContainerName);
                
                int processedCount = 0;
                int skippedCount = 0;

                await foreach (var blobItem in containerClient.GetBlobsAsync())
                {
                    try
                    {
                        Console.WriteLine($"Found blob: {blobItem.Name}");
                        
                        if (blobItem.Name.EndsWith("/"))
                            continue;

                        var blobClient = containerClient.GetBlobClient(blobItem.Name);
                        
                        string content;
                        using (var stream = await blobClient.OpenReadAsync())
                        using (var reader = new StreamReader(stream))
                        {
                            content = await reader.ReadToEndAsync();
                        }
                        if (blobItem.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                Console.WriteLine($"Extracting text from PDF: {blobItem.Name}");
                                content = await ExtractTextFromPdfAsync(blobClient);
                                
                                if (string.IsNullOrWhiteSpace(content))
                                {
                                    Console.WriteLine($"No text extracted from PDF: {blobItem.Name}");
                                    skippedCount++;
                                    continue;
                                }
                                
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error extracting text from PDF {blobItem.Name}: {ex.Message}");
                                skippedCount++;
                                continue;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            Console.WriteLine($"Skipping empty file: {blobItem.Name}");
                            skippedCount++;
                            continue;
                        }

                        Console.WriteLine($"Processing file: {blobItem.Name} (Content length: {content.Length} chars)");
                        Console.WriteLine($"Content preview: {content.Substring(0, Math.Min(200, content.Length))}...");
                        
                        // Special logging for PDF files
                        if (blobItem.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"PDF file '{blobItem.Name}' processed successfully with {content.Length} characters");
                        }

                        // Generate embedding
                        var embedding = await GetEmbeddingAsync(content);
                        
                        _documentEmbeddings[blobItem.Name] = embedding;
                        _documentContent[blobItem.Name] = content;
                        
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                    }
                }

                Console.WriteLine($"Document loading completed. Processed: {processedCount}, Skipped: {skippedCount}");
                Console.WriteLine($"Total documents in knowledge base: {_documentEmbeddings.Count}");

                // Save embeddings cache after processing
                await SaveEmbeddingsCacheAsync();
                _isInitialized = true;
                Console.WriteLine("ChatBotService: Initialization completed successfully with blob storage data");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading documents from Azure Blob Storage: {ex.Message}");
                Console.WriteLine("Falling back to demo data only");
                await LoadDemoDataAsync();
                await LoadFaqDatabaseAsync();
                await SaveEmbeddingsCacheAsync(); // Save demo data cache
                _isInitialized = true;
                Console.WriteLine("ChatBotService: Initialization completed successfully with fallback demo data");
            }
        }

        // demo data
        private async Task LoadDemoDataAsync()
        {
            try
            {
                var demoContent = new Dictionary<string, string>
                {
                    ["tutorly-intro.txt"] = "Tutorly is a comprehensive peer-powered learning platform designed to connect students with tutors and provide AI-powered educational support. The platform facilitates knowledge sharing through peer tutoring, interactive forums, and intelligent chatbot assistance.",
                    
                    ["tutorly-features.txt"] = "Tutorly offers multiple learning features: AI-powered chatbot for instant help with questions, peer tutoring system connecting students with knowledgeable peers, interactive discussion forums for collaborative learning, calendar scheduling for tutoring sessions, progress tracking to monitor learning goals, and resource sharing for educational materials.",
                    
                    ["tutorly-subjects.txt"] = "Tutorly supports the following academic modules: Mathematics 1, Mathematics 2, Statistics, Programming 1, Programming 2, Data Structures, Algorithms, Databases, Web Programming, Operating Systems, and Networks. These modules cover fundamental computer science and mathematics topics for university students.",
                    
                    ["tutorly-modules.txt"] = "The platform organizes learning through modules and courses. Students can enroll in specific modules, track their progress, access course materials, participate in discussions, and receive personalized tutoring. Modules are structured to provide comprehensive coverage of topics with clear learning objectives.",
                    
                    ["tutorly-help.txt"] = "Getting help on Tutorly is easy: Use the AI chatbot for quick questions and explanations, browse available tutors by subject and availability, post questions in relevant forum sections, schedule one-on-one tutoring sessions, access shared resources and study materials, and track your learning progress through the dashboard.",
                    
                    ["tutorly-ai-assistant.txt"] = "The AI assistant on Tutorly can help with: Answering subject-specific questions, explaining complex concepts in simple terms, providing study tips and learning strategies, helping with homework and assignments, suggesting relevant resources, and offering personalized learning recommendations based on your progress and interests."
                };

                foreach (var kvp in demoContent)
                {
                    try
                    {
                        var embedding = await GetEmbeddingAsync(kvp.Value);
                        _documentEmbeddings[kvp.Key] = embedding;
                        _documentContent[kvp.Key] = kvp.Value;
                    }
                    catch (Exception ex)
                    { //skip
                    }
                }

            }
            catch (Exception ex)
            {
            }
        }

        // Load FAQ database for smart escalation
        private async Task LoadFaqDatabaseAsync()
        {
            try
            {
                _faqDatabase["How do I book a tutor?"] = "To book a tutor, go to the Modules page, select your module, and click on 'Book Tutor' next to any available tutor. You can schedule sessions based on their availability. The system will show you available tutors for your specific module.";
                
                _faqDatabase["How do I join a study room?"] = "You can join study rooms by going to the Study Rooms page and either creating a new room or joining an existing one using the room code.";
                
                _faqDatabase["How do I post in the forum?"] = "Navigate to the Forum section, select the appropriate community, and click 'Create Post' to ask questions or share information with other students.";
                
                _faqDatabase["How do I access my modules?"] = "Your enrolled modules are displayed on the My Modules page. Click on any module to view details, tutors, resources, and forum discussions.";
                
                _faqDatabase["How do I update my profile?"] = "Go to your profile settings where you can update your personal information, academic details, and preferences.";
                
                _faqDatabase["What subjects are available for tutoring?"] = "Tutorly supports all modules taught at Belgium Campus ITVersity. If there is a tutor, it is on our system!";
                
                _faqDatabase["How do I contact support?"] = "You can contact support through the chatbot, escalate queries to registered tutors, or reach out through the help section in your profile.";
                
                _faqDatabase["How do I rate a tutor?"] = "After a tutoring session, you can rate your tutor by going to your 'my modules' page and providing a rating.";
                
                _faqDatabase["How do I find study materials?"] = "Study materials are available in each module's resource section. You can also access shared materials from tutors and other students in the forum.";
                
                _faqDatabase["How do I schedule recurring sessions?"] = "When booking a tutor, you can select the 'Recurring' option to schedule multiple sessions at regular intervals.";
                
                Console.WriteLine($"Loaded {_faqDatabase.Count} FAQ entries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading FAQ database: {ex.Message}");
            }
        }

        int user_warnings = 0;  
        

        // Generate a response using RAG 
        public async Task<ChatBotResponse> GenerateResponseAsync(string userQuery, int maxDocuments = 3)
        {
            try
            {
                Console.WriteLine($"GenerateResponseAsync called with query: {userQuery.Substring(0, Math.Min(50, userQuery.Length))}...");
                
                //Check for profanity and escalation triggers BEFORE any AI processing
                if (ContainsProfanity(userQuery)) 
                {
                    Console.WriteLine("Profanity detected in user query");
                    user_warnings += 1;

                    if (user_warnings >= 3)
                    {
                        return new ChatBotResponse
                        {
                            Success = true,
                            Response = $"You have {user_warnings} profanity warnings. Your query has been escalated to a tutor.",
                            ShouldEscalate = true,
                            EscalationReason = "Excessive profanity usage",
                            Query = userQuery
                        };
                    }
                    return new ChatBotResponse
                    {
                        Success = true,
                        Response = $"Please refrain from using profanity, this is a bannable offence. Current warnings: {user_warnings}",
                        Query = userQuery
                    };
                }

                // escalation check - first priority
                var escalationTriggers = new[] { "report", "cost", "hacked", "ridiculous", "frustrated", "seriously", "stupid", "terrible", "come on" };
                var shouldEscalateByTrigger = escalationTriggers.Any(trigger => userQuery.ToLower().Contains(trigger));
                
                if (shouldEscalateByTrigger)
                {
                    Console.WriteLine("Escalation trigger words detected");
                    var triggerFaqSuggestions = await GetFaqSuggestionsAsync(userQuery);
                    return new ChatBotResponse
                    {
                        Success = true,
                        Response = BuildEscalationResponse("", triggerFaqSuggestions, 0.0f),
                        ShouldEscalate = true,
                        EscalationReason = "Query contains escalation trigger words",
                        FaqSuggestions = triggerFaqSuggestions,
                        ConfidenceScore = 0.0f,
                        Query = userQuery
                    };
                }
                
                Console.WriteLine($"Document embeddings count: {_documentEmbeddings.Count}");
                
                if (_documentEmbeddings.Count == 0)
                {
                    Console.WriteLine("No document embeddings found - service may not be properly initialized");
                    return new ChatBotResponse
                    {
                        Success = false,
                        ErrorMessage = "ChatBot service is not properly initialized. Please contact support.",
                        SourceDocuments = new List<string>(),
                        Query = userQuery
                    };
                }

                Console.WriteLine("Retrieving relevant documents...");
                var relevantDocuments = await RetrieveTopDocumentsAsync(userQuery, maxDocuments);
                Console.WriteLine($"Found {relevantDocuments.Count} relevant documents");
                
                // Debug: Log document names for troubleshooting
                if (relevantDocuments.Count > 0)
                {
                    Console.WriteLine($"Retrieved documents: {string.Join(", ", relevantDocuments)}");
                    
                    // Log content preview of retrieved documents
                    foreach (var docName in relevantDocuments)
                    {
                        if (_documentContent.TryGetValue(docName, out var docContent))
                        {
                            Console.WriteLine($"Document '{docName}' content preview: {docContent.Substring(0, Math.Min(100, docContent.Length))}...");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No relevant documents found - this might cause low confidence");
                }
                
                var documentContents = GetDocumentContents(relevantDocuments);
                
                // Calculate confidence score based on document relevance
                var confidenceScore = CalculateConfidenceScore(userQuery, relevantDocuments);
                Console.WriteLine($"Confidence score: {confidenceScore:F2}");
                
                var faqSuggestions = await GetFaqSuggestionsAsync(userQuery);
                
                var enhancedPrompt = BuildEnhancedPrompt(userQuery, documentContents);

                Console.WriteLine("Generating AI response...");
                var aiResponse = await GenerateWithLlamaAsync(enhancedPrompt);
                Console.WriteLine($"AI response generated: {aiResponse.Substring(0, Math.Min(100, aiResponse.Length))}...");
                
                aiResponse = CleanResponseToSingleAnswer(aiResponse);
                
                // Determine if escalation is needed based on confidence and response content
                var shouldEscalate = confidenceScore < 0.1f || 
                                   (confidenceScore < 0.15f && faqSuggestions.Count == 0) ||
                                   aiResponse.Contains("I don't know") ||
                                   aiResponse.Contains("I cannot") ||
                                   aiResponse.Contains("I'm not sure") ||
                                   aiResponse.Contains("search filters") ||
                                   aiResponse.Contains("experience level") ||
                                   aiResponse.Contains("teaching method");


                // Check AI response for profanity
                if (ContainsProfanity(aiResponse))
                {
                    Console.WriteLine("Profanity detected in AI response, filtering...");
                    aiResponse = CensorProfanity(aiResponse);
                }

                // If escalation is needed, modify the response
                if (shouldEscalate)
                {
                    aiResponse = BuildEscalationResponse(aiResponse, faqSuggestions, confidenceScore);
                }

                return new ChatBotResponse
                {
                    Success = true,
                    Response = aiResponse,
                    SourceDocuments = documentContents,
                    Query = userQuery,
                    ShouldEscalate = shouldEscalate,
                    FaqSuggestions = faqSuggestions,
                    ConfidenceScore = confidenceScore,
                    EscalationReason = shouldEscalate ? GetEscalationReason(confidenceScore, faqSuggestions.Count) : null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateResponseAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ChatBotResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to generate response: {ex.Message}",
                    Query = userQuery
                };
            }
        }

        #region RAG Functionality

        // Get embedding for text  
        private async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (_embeddingService != null)
            {
                try
                {
                    return await _embeddingService.GetEmbeddingAsync(text);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting embedding from service: {ex.Message}");
                    // Fallback to direct HTTP call
                }
            }
            
            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/embed", new { text });
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<float>>>(); //key: string, value: list<float>
                if (result == null || !result.ContainsKey("embedding"))
                    throw new Exception("Embedding API returned invalid data.");
                return result["embedding"].ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting embedding from HTTP API: {ex.Message}");
                // Return a zero vector as fallback
                return new float[384]; // all-MiniLM-L6-v2 produces 384-dimensional embeddings
            }
        }

        // Calculate cosine similarity between two vectors - user query and document content (embedded)
        private float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0f, normA = 0f, normB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            return dot / ((float)(Math.Sqrt(normA) * Math.Sqrt(normB)));
        }

        // Retrieve most relevant documents for RAG
        private async Task<List<string>> RetrieveTopDocumentsAsync(string query, int k = 3)
        {
            // Check if we already have this query's embedding cached
            var queryVec = GetCachedQueryEmbedding(query);
            if (queryVec == null)
            {
                // Only call the API if we don't have it cached
                queryVec = await GetEmbeddingAsync(query);
                _queryEmbeddingCache[query] = queryVec;
                Console.WriteLine($"Cached query embedding for: {query.Substring(0, Math.Min(20, query.Length))}...");
            }
            else
            {
                Console.WriteLine($"Using cached query embedding for: {query.Substring(0, Math.Min(20, query.Length))}...");
            }
            
            var similarities = _documentEmbeddings
                .Select(doc => new { 
                    Name = doc.Key, 
                    Similarity = CosineSimilarity(doc.Value, queryVec) 
                }) //call cosineSimilarity check for each doc and compares to query vector
                .OrderByDescending(x => x.Similarity)
                .ToList(); //order by to get top 3 responses
            
            const float similarityThreshold = 0.02f; // Lowered threshold for better document retrieval
            
            var relevantDocs = similarities
                .Where(x => x.Similarity >= similarityThreshold)
                .Take(k)
                .Select(x => x.Name) //transform object into doc names
                .ToList();
            
            return relevantDocs;
        }

        // Get the actual content of documents from stored content
        private List<string> GetDocumentContents(List<string> documentNames)
        {
            var contents = new List<string>();

            foreach (var documentName in documentNames)
            {
                if (_documentContent.TryGetValue(documentName, out var content))
                {
                    contents.Add(content);
                }
            }

            return contents;
        }

        #endregion

        #region HuggingFace AI Integration

        private async Task<string> GenerateWithLlamaAsync(string prompt)
        {
            try
            {
                // If no HuggingFace API key, return error message
                if (string.IsNullOrEmpty(_apiKey) || _apiKey == "demo-key")
                {
                    return "I'm sorry, but the AI service is currently unavailable. Please check your HuggingFace API key configuration.";
                }

                var huggingFaceResponse = await CallHuggingFaceAPI(prompt);
                
                if (huggingFaceResponse.Contains("unavailable") || huggingFaceResponse.Contains("error"))
                {
                    Console.WriteLine("unavailable - error");
                }
                
                return huggingFaceResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateWithLlamaAsync: {ex.Message}");
                return "I'm sorry, but the AI service encountered an error. Please try again later.";
            }
        }



        private async Task<string> CallHuggingFaceAPI(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    prompt = prompt,
                    model = _model,
                    max_tokens = 200,
                    temperature = 0.7,
                    top_p = 0.9
                };
                
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var apiUrl = "https://router.huggingface.co/featherless-ai/v1/completions";

                Console.WriteLine($"Calling HuggingFace Router API: {apiUrl}");
                Console.WriteLine($"Model: {_model}");
                Console.WriteLine($"Prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");

                var response = await _httpClient.PostAsync(apiUrl, content);

                Console.WriteLine($"HuggingFace API Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode == false)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"HuggingFace API Error ({response.StatusCode}): {errorContent}");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"Model '{_model}' not found. Please check if the model exists on HuggingFace.");
                        return "I'm sorry, but the AI service is currently unavailable. The model was not found.";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine("HuggingFace API key is invalid or missing.");
                        return "I'm sorry, but the AI service is currently unavailable. Please check your API key configuration.";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Console.WriteLine("HuggingFace API rate limit exceeded.");
                        return "I'm sorry, but the AI service is currently unavailable due to rate limiting. Please try again later.";
                    }
                    
                    return "I'm sorry, but the AI service is currently unavailable. Please try again later.";
                }

                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"HuggingFace Router API Response Body: {body.Substring(0, Math.Min(500, body.Length))}...");

                using var doc = JsonDocument.Parse(body);
                
                // Handle Router API response format
                if (doc.RootElement.TryGetProperty("choices", out var choicesElement) && 
                    choicesElement.ValueKind == JsonValueKind.Array && 
                    choicesElement.GetArrayLength() > 0)
                {
                    var firstChoice = choicesElement[0];
                    if (firstChoice.TryGetProperty("text", out var textElement))
                    {
                        var generatedText = textElement.GetString();
                        if (!string.IsNullOrEmpty(generatedText))
                        {
                            // Clean the response - remove the original prompt if it's included
                            var cleanText = generatedText.StartsWith(prompt) ? generatedText.Substring(prompt.Length).Trim() : generatedText;
                            return string.IsNullOrEmpty(cleanText) ? generatedText : cleanText;
                        }
                    }
                }

                Console.WriteLine("Unexpected response format from HuggingFace Router API");
                return "I'm sorry, but the AI service returned an unexpected response format. Please try again later.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HuggingFace API Exception: {ex.Message}");
                return "I'm sorry, but the AI service encountered an error. Please try again later.";
            }
        }


        #endregion

        #region Profanity Filtering

        private bool ContainsProfanity(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return false;

                var filter = new ProfanityFilter.ProfanityFilter();
                var profanities = filter.DetectAllProfanities(text);
                return profanities.Count > 0; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking profanity: {ex.Message}");
                return false;
            }
        }

        private string CensorProfanity(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return text;

                var filter = new ProfanityFilter.ProfanityFilter();
                return filter.CensorString(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error censoring profanity: {ex.Message}");
                return text;
            }
        }

        #endregion

        #region Query Embedding Cache

        // Get cached query embedding to avoid duplicate API calls
        private float[]? GetCachedQueryEmbedding(string query)
        {
            return _queryEmbeddingCache.TryGetValue(query, out var embedding) ? embedding : null;
        }

        #endregion

        #region Smart Escalation Methods

        // Calculate confidence score based on document similarity
        private float CalculateConfidenceScore(string query, List<string> relevantDocuments)
        {
            if (relevantDocuments.Count == 0)
            {
                Console.WriteLine("No relevant documents for confidence calculation");
                return 0.0f;
            }

            // Use the query embedding that was already calculated in RetrieveTopDocumentsAsync
            var queryVec = GetCachedQueryEmbedding(query);
            if (queryVec == null)
            {
                Console.WriteLine("Query embedding not found in cache, using fallback");
                return 0.5f; 
            }

            var maxSimilarity = 0.0f;
            Console.WriteLine($"Calculating confidence for {relevantDocuments.Count} documents");
            
            foreach (var docName in relevantDocuments)
            {
                if (_documentEmbeddings.TryGetValue(docName, out var docVec))
                {
                    var similarity = CosineSimilarity(queryVec, docVec);
                    Console.WriteLine($"Document '{docName}' similarity: {similarity:F4}");
                    maxSimilarity = Math.Max(maxSimilarity, similarity);
                }
                else
                {
                    Console.WriteLine($"Document '{docName}' not found in embeddings cache");
                }
            }

            Console.WriteLine($"Max similarity found: {maxSimilarity:F4}");
            
            // Convert similarity to confidence score (0-1 range)
            var confidence = Math.Max(0.0f, Math.Min(1.0f, maxSimilarity));
            Console.WriteLine($"Final confidence score: {confidence:F4}");
            
            return confidence;
        }

        // Get FAQ suggestions based on query similarity
        private async Task<List<string>> GetFaqSuggestionsAsync(string query)
        {
            var suggestions = new List<string>();
            
            try
            {
                // Use cached query embedding if available
                var queryVec = GetCachedQueryEmbedding(query);
                if (queryVec == null)
                {
                    queryVec = await GetEmbeddingAsync(query);
                    _queryEmbeddingCache[query] = queryVec;
                }

                var faqSimilarities = new List<(string question, float similarity)>();

                foreach (var faq in _faqDatabase.Keys)
                {
                    var similarity = CalculateTextSimilarity(query.ToLower(), faq.ToLower());
                    
                    if (similarity > 0.3f) // Threshold for FAQ relevance
                    {
                        faqSimilarities.Add((faq, similarity));
                    }
                }

                // Sort by similarity and take top 3
                suggestions = faqSimilarities
                    .OrderByDescending(x => x.similarity)
                    .Take(3)
                    .Select(x => x.question)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting FAQ suggestions: {ex.Message}");
            }

            return suggestions;
        }

        // text similarity for FAQ matching
        private float CalculateTextSimilarity(string query, string faq)
        {
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var faqWords = faq.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var commonWords = queryWords.Intersect(faqWords, StringComparer.OrdinalIgnoreCase).Count();
            var totalWords = Math.Max(queryWords.Length, faqWords.Length);
            
            return totalWords > 0 ? (float)commonWords / totalWords : 0f;
        }

        // Build escalation response with FAQ suggestions
        private string BuildEscalationResponse(string originalResponse, List<string> faqSuggestions, float confidenceScore)
        {
            var response = new StringBuilder();
            
            // Add original response if it's not empty
            if (!string.IsNullOrWhiteSpace(originalResponse) && !originalResponse.Contains("I don't know"))
            {
                response.AppendLine(originalResponse);
                response.AppendLine();
            }

            // Add escalation message
            response.AppendLine("I am not entirely confident in my response. Let me help you get better assistance:");
            response.AppendLine();

            // Add FAQ suggestions if available
            if (faqSuggestions.Count > 0)
            {
                response.AppendLine("📚 Related FAQ Questions:");
                for (int i = 0; i < faqSuggestions.Count; i++)
                {
                    response.AppendLine($"{i + 1}. {faqSuggestions[i]}");
                }
                response.AppendLine();
            }

            // Add escalation message
            response.AppendLine("I am escalating your question to a human tutor who can provide more detailed assistance.");
            response.AppendLine("You will be notified when a tutor responds to your query.");

            return response.ToString();
        }

        // Get escalation reason for logging
        private string GetEscalationReason(float confidenceScore, int faqCount)
        {
            if (confidenceScore < 0.3f)
                return "Low confidence score";
            if (confidenceScore < 0.5f && faqCount == 0)
                return "Medium confidence with no FAQ matches";
            return "AI response indicates uncertainty";
        }

        #endregion

        #region Persistent Embeddings Storage

        // Save embeddings to cache file
        private async Task SaveEmbeddingsCacheAsync()
        {
            try
            {
                var cacheData = new
                {
                    DocumentEmbeddings = _documentEmbeddings.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value.ToList()
                    ),
                    DocumentContent = _documentContent,
                    Timestamp = DateTime.UtcNow
                };

                var json = System.Text.Json.JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_embeddingsCachePath, json);
                _lastCacheUpdate = DateTime.UtcNow;
                Console.WriteLine($"Embeddings cache saved to {_embeddingsCachePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving embeddings cache: {ex.Message}");
            }
        }

        // Check if cache is up to date by comparing with blob storage
        private async Task<bool> IsCacheUpToDateAsync()
        {
            try
            {
                // If no blob storage configured, cache is always up to date
                if (string.IsNullOrEmpty(_blobConnectionString) || string.IsNullOrEmpty(_blobContainerName))
                {
                    return true;
                }

                var containerClient = new BlobContainerClient(_blobConnectionString, _blobContainerName);
                var latestBlobTime = DateTime.MinValue;

                await foreach (var blobItem in containerClient.GetBlobsAsync())
                {
                    if (blobItem.Properties.LastModified.HasValue)
                    {
                        var blobTime = blobItem.Properties.LastModified.Value.DateTime;
                        if (blobTime > latestBlobTime)
                        {
                            latestBlobTime = blobTime;
                        }
                    }
                }

                // If cache is newer than the latest blob, it's up to date
                return _lastCacheUpdate > latestBlobTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking cache freshness: {ex.Message}");
                return false; // If we can't check, assume cache is outdated
            }
        }

        // Load embeddings from cache file
        private async Task<bool> LoadEmbeddingsCacheAsync()
        {
            try
            {
                if (!File.Exists(_embeddingsCachePath))
                {
                    Console.WriteLine("No embeddings cache file found");
                    return false;
                }

                var json = await File.ReadAllTextAsync(_embeddingsCachePath);
                var cacheData = System.Text.Json.JsonSerializer.Deserialize<EmbeddingsCacheData>(json);

                if (cacheData?.DocumentEmbeddings != null && cacheData.DocumentContent != null)
                {
                    _documentEmbeddings.Clear();
                    _documentContent.Clear();

                    foreach (var kvp in cacheData.DocumentEmbeddings)
                    {
                        _documentEmbeddings[kvp.Key] = kvp.Value.ToArray();
                    }

                    foreach (var kvp in cacheData.DocumentContent)
                    {
                        _documentContent[kvp.Key] = kvp.Value;
                    }

                    Console.WriteLine($"Loaded {_documentEmbeddings.Count} cached embeddings from {_embeddingsCachePath}");
                    _lastCacheUpdate = cacheData.Timestamp;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading embeddings cache: {ex.Message}");
            }

            return false;
        }

        // Cache data structure
        private class EmbeddingsCacheData
        {
            public Dictionary<string, List<float>>? DocumentEmbeddings { get; set; }
            public Dictionary<string, string>? DocumentContent { get; set; }
            public DateTime Timestamp { get; set; }
        }

        #endregion

        private string CleanResponseToSingleAnswer(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            var cleaned = response
                .Replace("You are Tutorly, an AI tutoring assistant.", "")
                .Replace("Answer ONLY the specific question asked completely.", "")
                .Replace("Give a single, direct answer in 2-3 sentences maximum.", "")
                .Replace("Do NOT ask additional questions or provide extra information.", "")
                .Replace("Answer (single direct response only):", "")
                .Replace("Answer (be brief and direct):", "")
                .Trim();

            // encoding issues
            cleaned = cleaned
                .Replace("undefined", "'")
                .Replace("&apos;", "'")
                .Replace("&#39;", "'")
                .Replace("&quot;", "\"")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");

            // Split by common question patterns and take only the first answer
            var lines = cleaned.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var answerLines = new List<string>();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Stop if we hit a question patternn but allow numbered lists
                if (trimmedLine.StartsWith("Question:") || 
                    trimmedLine.StartsWith("Q:") ||
                    (trimmedLine.EndsWith("?") && !trimmedLine.StartsWith("1.") && !trimmedLine.StartsWith("2.") && !trimmedLine.StartsWith("3.") && !trimmedLine.StartsWith("4.") && !trimmedLine.StartsWith("5.")) ||
                    (trimmedLine.StartsWith("What") && !trimmedLine.StartsWith("1.")) ||
                    (trimmedLine.StartsWith("How") && !trimmedLine.StartsWith("1.")) ||
                    (trimmedLine.StartsWith("When") && !trimmedLine.StartsWith("1.")) ||
                    (trimmedLine.StartsWith("Where") && !trimmedLine.StartsWith("1.")) ||
                    (trimmedLine.StartsWith("Why") && !trimmedLine.StartsWith("1.")))
                {
                    break;
                }
                
                if (!string.IsNullOrWhiteSpace(trimmedLine) && 
                    !trimmedLine.StartsWith("Answer:") &&
                    !trimmedLine.StartsWith("Response:") &&
                    !trimmedLine.Contains("tutoring assistant"))
                {
                    answerLines.Add(trimmedLine);
                }
            }
            
            var answer = string.Join("\n", answerLines);
            
            if (answer.Contains("1.") || answer.Contains("Step") || answer.Contains("Here's how"))
            {
                return answer.Trim();
            }
            
            //limit to 2-3 sentences
            var sentences = answer.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (sentences.Length > 3)
            {
                answer = string.Join(". ", sentences.Take(3)) + ".";
            }
            
            return answer.Trim();
        }

        private string BuildEnhancedPrompt(string userQuery, List<string> documentContents)
        {
            var promptBuilder = new StringBuilder();
            
            promptBuilder.AppendLine("You are Tutorly, an AI tutoring assistant. Answer ONLY the specific question asked based on the provided context. Give a single, direct answer in 1-2 sentences maximum. Do NOT ask additional questions, provide extra information, or make assumptions about features not mentioned in the context.");
            promptBuilder.AppendLine();

            if (documentContents.Any()) //checks if list has elemetns
            {
                promptBuilder.AppendLine("Context:");
                for (int i = 0; i < documentContents.Count; i++)
                {
                    promptBuilder.AppendLine(documentContents[i]);
                    promptBuilder.AppendLine();
                }
            }

            promptBuilder.AppendLine("Question: " + userQuery);
            promptBuilder.AppendLine("Answer (based only on the context provided):");

            return promptBuilder.ToString();
        }

        private async Task<string> ExtractTextFromPdfAsync(BlobClient blobClient)
        {
            try
            {
                using var stream = await blobClient.OpenReadAsync();
                using var pdfReader = new PdfReader(stream);
                using var pdfDocument = new PdfDocument(pdfReader);
                
                var textBuilder = new StringBuilder();
                
                for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
                {
                    var page = pdfDocument.GetPage(pageNum);
                    var strategy = new SimpleTextExtractionStrategy();
                    var text = PdfTextExtractor.GetTextFromPage(page, strategy);
                    textBuilder.AppendLine(text);
                }
                
                return textBuilder.ToString().Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting PDF text: {ex.Message}");
                throw;
            }
        }
    }

    public class ChatBotResponse
    {
        public bool Success { get; set; }
        public string Response { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> SourceDocuments { get; set; } = new List<string>();
        public string Query { get; set; } = string.Empty;
        public bool ShouldEscalate { get; set; } = false;
        public List<string> FaqSuggestions { get; set; } = new List<string>();
        public float ConfidenceScore { get; set; } = 1.0f;
        public string? EscalationReason { get; set; }
    }

    public class ChatBotRequest
    {
        public string Message { get; set; } = string.Empty;
        public int? MaxDocuments { get; set; } = 3;
    }
}