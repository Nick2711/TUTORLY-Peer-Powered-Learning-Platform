using System.Net.Http.Json;
using Tutorly.Shared;

namespace Tutorly.Shared
{
    public class TopicService
    {
        private readonly HttpClient _http;

        public TopicService(HttpClient http)
        {
            _http = http;
        }

        public async Task<bool> Subscribe(int studentId, int topicId)
        {
            var subscription = new { studentId, topicId };
            var response = await _http.PostAsJsonAsync("api/subscribe", subscription);
            return response.IsSuccessStatusCode;
        }

        // Subscribe by ModuleId (resolving TopicId on the server)
        public async Task<bool> SubscribeToTopicByModuleId(int studentId, int moduleId)
        {
            var response = await _http.PostAsJsonAsync("api/subscribe/module", new { StudentId = studentId, ModuleId = moduleId });
            return response.IsSuccessStatusCode;
        }
    }
}
