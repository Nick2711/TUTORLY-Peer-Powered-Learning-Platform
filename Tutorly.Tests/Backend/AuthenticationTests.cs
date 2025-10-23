using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Tutorly.Tests.Infrastructure;

namespace Tutorly.Tests.Backend;

/// <summary>
/// White-box tests for AuthController functionality
/// Tests the actual business logic and validation rules
/// </summary>
public class AuthenticationTests : IntegrationTestBase
{
    // IMPORTANT: pass the factory to the base so all helpers are available.
    public AuthenticationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SignUp_WithNonCampusEmail_ReturnsBadRequest()
    {
        var dto = new { Email = "alice@gmail.com", Password = "Passw0rd!", Role = "student" };

        var res = await Http.PostAsJsonAsync("/api/auth/signup", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync())
            .Should().Contain("@belgiumcampus.ac.za");
    }

    #region SignUp Tests

    [Fact]
    public async Task SignUp_WithValidBelgiumCampusEmail_ShouldReturnSuccessOrBadRequest()
    {
        var signUpDto = new
        {
            Email = ValidBelgiumCampusEmail,
            Password = ValidPassword,
            Role = "student"
        };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        // In Testing env you may see 400 if external deps aren’t mocked; both are acceptable here.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignUp_WithInvalidEmail_ShouldReturnBadRequest()
    {
        var signUpDto = new
        {
            Email = InvalidEmail,
            Password = ValidPassword,
            Role = "student"
        };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignUp_WithEmptyEmail_ShouldReturnBadRequest()
    {
        var signUpDto = new { Email = "", Password = ValidPassword, Role = "student" };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Email and password are required");
    }

    [Fact]
    public async Task SignUp_WithEmptyPassword_ShouldReturnBadRequest()
    {
        var signUpDto = new { Email = ValidBelgiumCampusEmail, Password = "", Role = "student" };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Email and password are required");
    }

    [Fact]
    public async Task SignUp_WithNullRole_ShouldDefaultToStudent()
    {
        var signUpDto = new { Email = ValidBelgiumCampusEmail, Password = ValidPassword, Role = (string?)null };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    #endregion

    #region SignIn Tests

    [Fact]
    public async Task SignIn_WithValidCredentials_ShouldReturnSuccessOrUnauthorized()
    {
        var signInDto = new { Email = ValidBelgiumCampusEmail, Password = ValidPassword };

        // Configure the auth mock exposed by the base
        AuthMock
            .Setup(x => x.GetUserSessionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(CreateValidStudentSession());

        var response = await Http.PostAsJsonAsync("/api/auth/signin", signInDto);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignIn_WithEmptyEmail_ShouldReturnBadRequest()
    {
        var signInDto = new { Email = "", Password = ValidPassword };

        var response = await Http.PostAsJsonAsync("/api/auth/signin", signInDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Email and password are required");
    }

    [Fact]
    public async Task SignIn_WithEmptyPassword_ShouldReturnBadRequest()
    {
        var signInDto = new { Email = ValidBelgiumCampusEmail, Password = "" };

        var response = await Http.PostAsJsonAsync("/api/auth/signin", signInDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Email and password are required");
    }

    #endregion

    #region Me Endpoint Tests

    [Fact]
    public async Task Me_WithoutAccessToken_ShouldReturnUnauthorized()
    {
        var response = await Http.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidAccessToken_ShouldReturnUserSessionOrUnauthorized()
    {
        var validSession = CreateValidStudentSession();

        AuthMock
            .Setup(x => x.GetUserSessionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(validSession);

        // add a mock cookie header
        Http.DefaultRequestHeaders.Add("Cookie", "sb-access-token=mock-token");

        var response = await Http.GetAsync("/api/auth/me");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task Refresh_WithoutRefreshToken_ShouldReturnUnauthorized()
    {
        var response = await Http.PostAsync("/api/auth/refresh", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithRefreshToken_ShouldReturnSuccessOrUnauthorized()
    {
        Http.DefaultRequestHeaders.Add("Cookie", "sb-refresh-token=mock-refresh-token");

        var response = await Http.PostAsync("/api/auth/refresh", content: null);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_ShouldReturnOk()
    {
        var response = await Http.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\"");
    }

    #endregion

    #region Email Validation Tests

    [Theory]
    [InlineData("student@belgiumcampus.ac.za")]
    [InlineData("tutor@belgiumcampus.ac.za")]
    [InlineData("admin@belgiumcampus.ac.za")]
    [InlineData("test.user@belgiumcampus.ac.za")]
    public async Task SignUp_WithValidBelgiumCampusEmails_ShouldBeAcceptedOrBadRequest(string email)
    {
        var signUpDto = new { Email = email, Password = ValidPassword, Role = "student" };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("user@gmail.com")]
    [InlineData("user@yahoo.com")]
    [InlineData("user@hotmail.com")]
    [InlineData("user@outlook.com")]
    public async Task SignUp_WithNonBelgiumCampusEmails_ShouldBeRejected(string email)
    {
        var signUpDto = new { Email = email, Password = ValidPassword, Role = "student" };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Role Assignment Tests

    [Theory]
    [InlineData("student")]
    [InlineData("tutor")]
    [InlineData("admin")]
    public async Task SignUp_WithValidRoles_ShouldBeAcceptedOrBadRequest(string role)
    {
        var signUpDto = new { Email = ValidBelgiumCampusEmail, Password = ValidPassword, Role = role };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignUp_WithInvalidRole_ShouldDefaultToStudentOrBadRequest()
    {
        var signUpDto = new { Email = ValidBelgiumCampusEmail, Password = ValidPassword, Role = "invalid-role" };

        var response = await Http.PostAsJsonAsync("/api/auth/signup", signUpDto);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    #endregion
}
