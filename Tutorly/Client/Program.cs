using System;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Tutorly.Client;
using Tutorly.Client.Pages.Auth;
using Tutorly.Client.Services;
using Tutorly.Shared;


var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Auth state for <CascadingAuthenticationState>
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ServerAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ServerAuthStateProvider>());
builder.Services.AddScoped<TopicService>();
builder.Services.AddScoped<ModuleService>();
builder.Services.AddScoped<ResourceService>();
builder.Services.AddScoped<TutorApplicationService>();

// HTTP to your Server project (same origin by default)
builder.Services.AddHttpClient("Tutorly.ServerAPI", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

// Default HttpClient for DI
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Tutorly.ServerAPI"));

// JWT-aware HttpClient with proper HttpClient injection
builder.Services.AddScoped<JwtHttpClient>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Tutorly.ServerAPI");
    var jsRuntime = sp.GetRequiredService<IJSRuntime>();
    return new JwtHttpClient(httpClient, jsRuntime);
});

// Real-time messaging service
builder.Services.AddScoped<MessagingHubService>();

// Real-time study room services
builder.Services.AddScoped<StudyRoomWebRTCService>();
builder.Services.AddScoped<StudyRoomHubService>();
builder.Services.AddScoped<MeteredVideoService>();

// Call state management
builder.Services.AddScoped<CallStateService>();

await builder.Build().RunAsync();
