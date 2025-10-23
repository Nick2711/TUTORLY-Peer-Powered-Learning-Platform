using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Services;


//  avoid clash with your Tutorly.Client namespace
using SupaClient = Supabase.Client;

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using static Supabase.Postgrest.Constants;

namespace Tutorly.Server.Controller
{
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly SupaClient _supabase;
        private readonly IUnifiedAuthService _unifiedAuthService;

        public ProfileController(SupaClient supabase, IUnifiedAuthService unifiedAuthService)
        {
            _supabase = supabase;
            _unifiedAuthService = unifiedAuthService;
        }

        // GET /api/profile/me
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            // 1) Read the HttpOnly access token set by your /api/auth/signin
            var accessToken = Request.Cookies["sb-access-token"];
            if (string.IsNullOrWhiteSpace(accessToken))
                return Unauthorized();

            // 2) Verify token and get user (your library version returns User directly)
            var user = await _supabase.Auth.GetUser(accessToken);
            if (user == null)
                return Unauthorized();

            // 3) Use UnifiedAuthService to get proper profile (student/tutor/admin)
            var session = await _unifiedAuthService.GetUserSessionAsync(user.Id, user.Email);
            if (session == null)
            {
                return Unauthorized("No profile found for this account.");
            }

            return Ok(session);
        }
    }

    //  cahnge to admin, tutor and student profiles
    [Table("profiles")]
    public class Profile : BaseModel
    {
        [PrimaryKey("user_id")]
        public string UserId { get; set; }

        [Column("student_id")]
        public string StudentId { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("full_name")]
        public string FullName { get; set; }

        [Column("programme")]
        public string Programme { get; set; }

        [Column("year_of_study")]
        public int? YearOfStudy { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("avatar_url")]
        public string AvatarUrl { get; set; }
    }
}
