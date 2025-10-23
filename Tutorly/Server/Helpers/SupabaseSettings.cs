namespace Tutorly.Server.Helpers
{
    public class SupabaseSettings
    {
        public string Url { get; set; } = string.Empty;
        public string AnonKey { get; set; } = string.Empty;
        public string ServiceRoleKey { get; set; } = string.Empty;
        public string JwtSecret { get; set; } = string.Empty;
    }

}
