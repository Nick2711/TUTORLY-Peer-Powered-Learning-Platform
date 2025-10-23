using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tutorly.Shared;
using Tutorly.Tests.Infrastructure; // UnitTestBase + CustomWebApplicationFactory
using Xunit;

namespace Tutorly.Tests.Backend;

/// <summary>
/// Fetch-only tests for Module endpoints.
/// NOTE: UnitTestBase has a parameterless ctor; don't pass the factory to base.
/// </summary>
public class ModuleTests : UnitTestBase, IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _http;

    // Do NOT call ": base(factory)". UnitTestBase doesn't take args.
    public ModuleTests(CustomWebApplicationFactory factory)
    {
        _http = factory.CreateClient();
    }

    [Fact]
    public async Task GetModules_List_ReturnsOk_AndContainsSeedData()
    {
        // Act
        var resp = await _http.GetAsync("/api/module");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var modules = await resp.Content.ReadFromJsonAsync<List<Module>>();
        modules.Should().NotBeNull();
        modules!.Should().NotBeEmpty();
        // Adjust the expected codes/names to whatever your seed data provides
        modules.Select(m => m.ModuleCode).Should().Contain(new[] { "CS101", "CS102", "MTH101" });
    }

    [Fact]
    public async Task GetModuleByName_Found_ReturnsOk_AndCorrectModule()
    {
        // Arrange (adjust to a known module name in your seed)
        var name = "Data Structures";

        // Act
        var resp = await _http.GetAsync($"/api/module/byname?name={Uri.EscapeDataString(name)}");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var module = await resp.Content.ReadFromJsonAsync<Module>();
        module.Should().NotBeNull();
        module!.ModuleName.Should().Be(name);
        module.ModuleCode.Should().Be("CS102"); // update if your seed differs
    }

    [Fact]
    public async Task GetModuleByName_NotFound_Returns404()
    {
        // Arrange
        var missing = "Does Not Exist";

        // Act
        var resp = await _http.GetAsync($"/api/module/byname?name={Uri.EscapeDataString(missing)}");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
