namespace Tutorly.Server.Services
{
    public interface IEmailNotificationService
    {
        /// <summary>
        /// Sends email notification to community members when a new post is created
        /// </summary>
        Task NotifyNewPostInCommunityAsync(int communityId, string postTitle, string postAuthor, string communityName, int postId);

        /// <summary>
        /// Sends email notification to post author when someone replies to their post
        /// </summary>
        Task NotifyPostAuthorOfReplyAsync(int postId, string postTitle, string replyAuthor, string postAuthorEmail);

        /// <summary>
        /// Sends email notification to post author when a tutor answers their post
        /// </summary>
        Task NotifyPostAuthorOfTutorAnswerAsync(int postId, string postTitle, string tutorName, string postAuthorEmail);

        /// <summary>
        /// Sends email notification to community members when a tutor answers any post in the community
        /// </summary>
        Task NotifyCommunityMembersOfTutorAnswerAsync(int communityId, string postTitle, string tutorName, string communityName, int postId);

        /// <summary>
        /// Sends a generic email notification
        /// </summary>
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}

