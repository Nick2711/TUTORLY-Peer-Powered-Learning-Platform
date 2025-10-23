using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Tutorly.Server.Helpers;
using Tutorly.Server.DatabaseModels;


namespace Tutorly.Server.Services
{
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly SmtpSettings _smtp;
        private readonly ISupabaseClientFactory _supabaseFactory;
        private readonly ISupabaseAuthService _supabaseAuthService;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly bool _isDev;

        public EmailNotificationService(
            IConfiguration configuration,
            ISupabaseClientFactory supabaseFactory,
            ISupabaseAuthService supabaseAuthService,
            ILogger<EmailNotificationService> logger)
        {
            _smtp = configuration.GetSection("Smtp").Get<SmtpSettings>() ?? new();
            _supabaseFactory = supabaseFactory;
            _supabaseAuthService = supabaseAuthService;
            _logger = logger;
            _isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        public async Task NotifyNewPostInCommunityAsync(int communityId, string postTitle, string postAuthor, string communityName, int postId)
        {
            try
            {
                _logger.LogInformation($"NotifyNewPostInCommunityAsync: Getting members of community {communityId}");

                // Get all community members
                var memberEmails = await GetCommunityMemberEmailsAsync(communityId);

                if (!memberEmails.Any())
                {
                    _logger.LogInformation($"No members found for community {communityId}");
                    return;
                }

                var subject = $"New post in {communityName}: {postTitle}";
                var html = $@"
<div style=""font-family:Inter,system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;background-color:#f9fafb"">
  <div style=""background-color:white;border-radius:8px;padding:24px;box-shadow:0 1px 3px rgba(0,0,0,0.1)"">
    <h2 style=""margin:0 0 16px;color:#1f2937;font-size:24px"">📝 New Post in {communityName}</h2>
    <p style=""margin:0 0 12px;color:#6b7280;font-size:14px"">Posted by <strong>{postAuthor}</strong></p>
    <h3 style=""margin:0 0 16px;color:#374151;font-size:18px"">{postTitle}</h3>
    <p style=""margin:0 0 24px;color:#6b7280"">A new post has been created in a community you're following.</p>
    <a href=""https://tutorly.app/forum/post/{postId}"" style=""display:inline-block;background-color:#3b82f6;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;font-weight:600"">View Post</a>
    <hr style=""margin:24px 0;border:none;border-top:1px solid #e5e7eb"">
    <p style=""margin:0;color:#9ca3af;font-size:12px"">You're receiving this because you joined the {communityName} community.</p>
  </div>
</div>";

                foreach (var email in memberEmails)
                {
                    await SendEmailAsync(email, subject, html);
                }

                _logger.LogInformation($"Sent new post notifications to {memberEmails.Count} members");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending new post notifications");
            }
        }

        public async Task NotifyPostAuthorOfReplyAsync(int postId, string postTitle, string replyAuthor, string postAuthorEmail)
        {
            try
            {
                var subject = $"New reply to your post: {postTitle}";
                var html = $@"
<div style=""font-family:Inter,system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;background-color:#f9fafb"">
  <div style=""background-color:white;border-radius:8px;padding:24px;box-shadow:0 1px 3px rgba(0,0,0,0.1)"">
    <h2 style=""margin:0 0 16px;color:#1f2937;font-size:24px"">💬 New Reply to Your Post</h2>
    <p style=""margin:0 0 12px;color:#6b7280;font-size:14px""><strong>{replyAuthor}</strong> replied to your post</p>
    <h3 style=""margin:0 0 16px;color:#374151;font-size:18px"">{postTitle}</h3>
    <a href=""https://tutorly.app/forum/post/{postId}"" style=""display:inline-block;background-color:#3b82f6;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;font-weight:600"">View Reply</a>
    <hr style=""margin:24px 0;border:none;border-top:1px solid #e5e7eb"">
    <p style=""margin:0;color:#9ca3af;font-size:12px"">You're receiving this because this is your post.</p>
  </div>
</div>";

                await SendEmailAsync(postAuthorEmail, subject, html);
                _logger.LogInformation($"Sent reply notification to post author at {postAuthorEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending reply notification to post author");
            }
        }

        public async Task NotifyPostAuthorOfTutorAnswerAsync(int postId, string postTitle, string tutorName, string postAuthorEmail)
        {
            try
            {
                var subject = $"🎓 A tutor answered your post: {postTitle}";
                var html = $@"
<div style=""font-family:Inter,system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;background-color:#f9fafb"">
  <div style=""background-color:white;border-radius:8px;padding:24px;box-shadow:0 1px 3px rgba(0,0,0,0.1)"">
    <h2 style=""margin:0 0 16px;color:#10b981;font-size:24px"">🎓 Tutor Answer!</h2>
    <p style=""margin:0 0 12px;color:#6b7280;font-size:14px""><strong>{tutorName}</strong> (Tutor) answered your question</p>
    <h3 style=""margin:0 0 16px;color:#374151;font-size:18px"">{postTitle}</h3>
    <p style=""margin:0 0 24px;color:#6b7280"">Great news! A tutor has provided an answer to your question.</p>
    <a href=""https://tutorly.app/forum/post/{postId}"" style=""display:inline-block;background-color:#10b981;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;font-weight:600"">View Answer</a>
    <hr style=""margin:24px 0;border:none;border-top:1px solid #e5e7eb"">
    <p style=""margin:0;color:#9ca3af;font-size:12px"">You're receiving this because this is your post.</p>
  </div>
</div>";

                await SendEmailAsync(postAuthorEmail, subject, html);
                _logger.LogInformation($"Sent tutor answer notification to post author at {postAuthorEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending tutor answer notification");
            }
        }

        public async Task NotifyCommunityMembersOfTutorAnswerAsync(int communityId, string postTitle, string tutorName, string communityName, int postId)
        {
            try
            {
                _logger.LogInformation($"NotifyCommunityMembersOfTutorAnswerAsync: Getting members of community {communityId}");

                var memberEmails = await GetCommunityMemberEmailsAsync(communityId);

                if (!memberEmails.Any())
                {
                    _logger.LogInformation($"No members found for community {communityId}");
                    return;
                }

                var subject = $"🎓 Tutor answered in {communityName}";
                var html = $@"
<div style=""font-family:Inter,system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;background-color:#f9fafb"">
  <div style=""background-color:white;border-radius:8px;padding:24px;box-shadow:0 1px 3px rgba(0,0,0,0.1)"">
    <h2 style=""margin:0 0 16px;color:#10b981;font-size:24px"">🎓 Tutor Activity in {communityName}</h2>
    <p style=""margin:0 0 12px;color:#6b7280;font-size:14px""><strong>{tutorName}</strong> (Tutor) answered a question</p>
    <h3 style=""margin:0 0 16px;color:#374151;font-size:18px"">{postTitle}</h3>
    <p style=""margin:0 0 24px;color:#6b7280"">A tutor has provided an answer in a community you're following.</p>
    <a href=""https://tutorly.app/forum/post/{postId}"" style=""display:inline-block;background-color:#10b981;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;font-weight:600"">View Answer</a>
    <hr style=""margin:24px 0;border:none;border-top:1px solid #e5e7eb"">
    <p style=""margin:0;color:#9ca3af;font-size:12px"">You're receiving this because you joined the {communityName} community.</p>
  </div>
</div>";

                foreach (var email in memberEmails)
                {
                    await SendEmailAsync(email, subject, html);
                }

                _logger.LogInformation($"Sent tutor answer notifications to {memberEmails.Count} community members");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending tutor answer notifications to community");
            }
        }

        private async Task<List<string>> GetCommunityMemberEmailsAsync(int communityId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Get all memberships for this community
                var membershipsResponse = await client
                    .From<ForumCommunityMembershipEntity>()
                    .Where(x => x.CommunityId == communityId)
                    .Get();

                if (membershipsResponse?.Models == null || !membershipsResponse.Models.Any())
                {
                    _logger.LogInformation($"No memberships found for community {communityId}");
                    return new List<string>();
                }

                var studentIds = membershipsResponse.Models
                    .Where(m => m.StudentId.HasValue)
                    .Select(m => m.StudentId!.Value)
                    .Distinct()
                    .ToList();

                if (!studentIds.Any())
                {
                    _logger.LogInformation($"No student IDs found in memberships for community {communityId}");
                    return new List<string>();
                }

                _logger.LogInformation($"Found {studentIds.Count} members for community {communityId}");

                // Get emails for each student using Supabase Auth API
                var emails = new List<string>();

                foreach (var studentId in studentIds)
                {
                    try
                    {
                        // Get user_id from student profile
                        var studentResponse = await client
                            .From<StudentProfileEntity>()
                            .Where(x => x.StudentId == studentId)
                            .Single();

                        if (studentResponse != null && !string.IsNullOrEmpty(studentResponse.UserId))
                        {
                            // Get email from Supabase Auth API
                            var email = await _supabaseAuthService.GetUserEmailByUserIdAsync(studentResponse.UserId);

                            if (!string.IsNullOrEmpty(email))
                            {
                                emails.Add(email);
                                _logger.LogInformation($"Found email for student {studentId}: {email}");
                            }
                            else
                            {
                                _logger.LogWarning($"No email found for student {studentId} (userId: {studentResponse.UserId})");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"No student profile found for studentId {studentId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Could not get email for student {studentId}");
                    }
                }

                _logger.LogInformation($"Retrieved {emails.Count} emails for community {communityId}");
                return emails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting community member emails");
                return new List<string>();
            }
        }

        public async Task SendEmailAsync(string to, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(_smtp.Host))
            {
                _logger.LogWarning("SMTP not configured, skipping email");
                if (_isDev)
                {
                    Console.WriteLine($"[DEV] Would send email to {to}");
                    Console.WriteLine($"Subject: {subject}");
                }
                return;
            }

            try
            {
                using var client = new SmtpClient(_smtp.Host, _smtp.Port)
                {
                    Credentials = new NetworkCredential(_smtp.User, _smtp.Pass),
                    EnableSsl = _smtp.EnableSsl
                };

                var msg = new MailMessage(_smtp.From ?? _smtp.User, to, subject, html)
                {
                    IsBodyHtml = true
                };

                await client.SendMailAsync(msg);
                _logger.LogInformation($"Sent email to {to}: {subject}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}");
                if (_isDev)
                {
                    Console.WriteLine($"[DEV] Email failed to {to}: {ex.Message}");
                }
            }
        }
    }
}

