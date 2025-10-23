using Tutorly.Server.Helpers;
using Tutorly.Server.DatabaseModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Tutorly.Shared;

namespace Tutorly.Server.Controllers
{
    public class TopicController
    {
        private readonly SupabaseClientFactory _supabase;

        public TopicController(SupabaseClientFactory supabase)
        {
            _supabase = supabase;
        }

        public async Task SubscribeStudentToTopic(int studentId, int topicId)
        {
            var subscription = new TopicSubscriptionEntity
            {
                StudentId = studentId,
                TopicId = topicId
            };
            await _supabase.AddEntity(subscription);
        }

        public async Task CreateTopic(int topicId, string topicName, string topicDescription, string createdBy, string moduleId)
        {
            var Topic = new TopicEntity
            {
                Id = topicId,
                Name = topicName,
                Description = topicDescription,
                CreatedBy = createdBy,
                ModuleId = moduleId
            };
            await _supabase.AddEntity(Topic);
        }
       

    }
}
