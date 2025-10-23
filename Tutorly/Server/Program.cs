using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

using Tutorly.Server.Helpers;          // SupabaseSettings, CookieOptionsFactory (your helper)
using Tutorly.Server.Controller;
using Supabase;                        // Supabase.Client
using Tutorly.Server.Handler;
using Tutorly.Server.Controllers;
using Tutorly.Server.Hubs;
using Tutorly.Server.Services;
using Tutorly.Shared;
using Microsoft.AspNetCore.SignalR;


var builder = WebApplication.CreateBuilder(args);

// Load user secrets in development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// MVC / Razor
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddScoped<TopicService>();


// ---- Config ----
builder.Services.Configure<SupabaseSettings>(builder.Configuration.GetSection("Supabase"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));  // Assuming you have this section
builder.Services.Configure<AzureBlobStoreOptions>(builder.Configuration.GetSection("AzureBlobStorage"));

// Debug Azure Blob Storage configuration
var azureConfig = builder.Configuration.GetSection("AzureBlobStorage");
Console.WriteLine($"DEBUG: Program.cs - AzureBlobStorage section exists: {azureConfig.Exists()}");
Console.WriteLine($"DEBUG: Program.cs - ConnectionString: {(!string.IsNullOrEmpty(azureConfig["ConnectionString"]) ? "SET" : "EMPTY")}");
Console.WriteLine($"DEBUG: Program.cs - BlobContainerName: {azureConfig["BlobContainerName"]}");

// Keep your factory registration if other parts of your app use it
builder.Services.AddSingleton<ISupabaseClientFactory, SupabaseClientFactory>();
// Also register the concrete type so controllers/handlers can depend on it directly
builder.Services.AddSingleton<SupabaseClientFactory>(sp => (SupabaseClientFactory)sp.GetRequiredService<ISupabaseClientFactory>());

builder.Services.AddSingleton<Supabase.Client>(sp =>
{
    var cfg = sp.GetRequiredService<IOptions<SupabaseSettings>>().Value;
    var url = cfg.Url;                 // e.g. https://xyzcompany.supabase.co
    var key = string.IsNullOrWhiteSpace(cfg.ServiceRoleKey) ? cfg.AnonKey : cfg.ServiceRoleKey;

    var options = new Supabase.SupabaseOptions
    {
        AutoConnectRealtime = false,
        AutoRefreshToken = false,
    };
    return new Supabase.Client(url!, key!, options);
});

// ---- JWT Authentication ----
var supabaseSettings = builder.Configuration.GetSection("Supabase").Get<SupabaseSettings>();

// Use a more flexible JWT validation approach that works with any Supabase project
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Set to true in production
        options.SaveToken = true;

        // Use Supabase's JWT secret if available, otherwise use a default approach
        var jwtSecret = supabaseSettings?.JwtSecret;
        if (!string.IsNullOrEmpty(jwtSecret))
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, // Don't validate issuer - Supabase tokens can have different issuers
                ValidateAudience = false, // Don't validate audience - Supabase tokens can have different audiences
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.FromMinutes(5) // Allow some clock skew for token validation
            };
        }
        else
        {
            // If no JWT secret is configured, use a more permissive validation
            // This allows the token to pass through and be validated by Supabase client
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false, // Don't validate lifetime here - let Supabase handle it
                ValidateIssuerSigningKey = false, // Don't validate signature here - let Supabase handle it
                RequireExpirationTime = false,
                RequireSignedTokens = false
            };
        }

        // Token validation events
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Authentication failed: {context.Exception?.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"JWT Token validated successfully for user: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                // Extract JWT token from query string for SignalR WebSocket connections
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/studyroomHub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---- CORS (must exactly match how you run the client) ----
var clientOrigin = builder.Configuration["Auth:ClientOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("client", p => p
        .WithOrigins(clientOrigin)         // e.g. http://localhost:5173 (HTTP in dev) or https://...
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// (Optional) make SameSite defaults predictable in dev; real flags are set where you append cookies.
builder.Services.Configure<CookiePolicyOptions>(opts =>
{
    opts.MinimumSameSitePolicy = SameSiteMode.Lax;
    opts.HttpOnly = HttpOnlyPolicy.Always;
});

// ---- Add logging ----
builder.Services.AddLogging();

// ---- Add SMTP settings if using Gmail/Email ----
builder.Services.AddSingleton<SmtpSettings>(sp =>
{
    return sp.GetRequiredService<IOptions<SmtpSettings>>().Value;
});
builder.Services.AddSingleton<ISupabaseClientFactory, SupabaseClientFactory>();

// Register HttpClientFactory for services that need it
builder.Services.AddHttpClient();

// Register Supabase Auth Service
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();

builder.Services.AddHttpClient<TopicService>();
// Register server-side module handler (uses Supabase directly)
builder.Services.AddScoped<ModuleHandler>();
// Topic subscription and topic controller services
builder.Services.AddScoped<TopicSubscriptionHandler>();
builder.Services.AddScoped<TopicController>();

builder.Services.AddScoped<AzureBlobStoreHandler>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureBlobStoreOptions>>().Value;
    return new AzureBlobStoreHandler(options);
});
builder.Services.AddScoped<ResourceService>();

// Register UnifiedAuthService (REQUIRED for Login/Register)
builder.Services.AddScoped<IUnifiedAuthService, UnifiedAuthService>();

// Register Content Filter Service
builder.Services.AddScoped<IContentFilterService, ContentFilterService>();

// Register Forum Services
builder.Services.AddScoped<IForumService, ForumService>();
builder.Services.AddScoped<IForumNotificationService, ForumNotificationService>();

// Register Module Tutor Services
builder.Services.AddScoped<IModuleTutorService, ModuleTutorService>();

// Register Tutor Application Services
builder.Services.AddScoped<ITutorApplicationService, TutorApplicationService>();

// Register Email Notification Service
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();

// Register Messaging Services
builder.Services.AddScoped<IMessagingService, MessagingService>();

// Register Study Room Services
builder.Services.AddScoped<IStudyRoomService, StudyRoomService>();
builder.Services.AddScoped<MeteredRoomService>();
builder.Services.AddHttpClient<MeteredRoomService>();

// Register Booking Services
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Register Rating Services
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IPdfExportService, PdfExportService>();

// Register Background Services
builder.Services.AddHostedService<SessionActivationBackgroundService>();
builder.Services.AddHostedService<ChatBotInitializationService>();

// Register custom user ID provider for SignalR
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// Register SignalR with timeout configuration
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});

// Register Embedding API Service
builder.Services.AddSingleton<IEmbeddingApiService, EmbeddingApiService>();
builder.Services.AddHostedService<EmbeddingApiManager>();

// Register ChatBot Service as Singleton to maintain initialization state
builder.Services.AddSingleton<ChatBotService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var embeddingService = sp.GetRequiredService<IEmbeddingApiService>();
    var blobConnectionString = configuration["Azure: ConnString"];
    var blobContainerName = configuration["Azure: BlobContainerNameChat"];

    return new ChatBotService(httpClient, configuration, blobConnectionString, blobContainerName, embeddingService);
});

// Register Admin Services
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAdminModuleService, AdminModuleService>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();


// ---- Build app ----
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// If you’re testing on HTTP locally, keep this. On HTTPS, it will redirect accordingly.
app.UseHttpsRedirection();

app.UseCookiePolicy();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("client");

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<ForumHub>("/forumHub");
app.MapHub<MessagingHub>("/messagingHub");
app.MapHub<StudyRoomHub>("/studyroomHub");
app.MapFallbackToFile("index.html");

app.Run();

