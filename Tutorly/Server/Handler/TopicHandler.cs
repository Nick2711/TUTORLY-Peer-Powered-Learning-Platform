using Tutorly.Shared;
using Tutorly.Server.Controllers;

namespace Tutorly.Server.Handler
{
    public class TopicHandler
    {
        private readonly TopicController _controller;

        public TopicHandler(TopicController controller)
        {
            _controller = controller;
        }

        // Handle creating a topic
        public async Task HandleCreateTopic(int topicId, string topicName, string topicDescription, string createdBy, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(topicName))
                throw new ArgumentException("Topic name cannot be empty");

            // Optional: add local validation here if needed

            await _controller.CreateTopic(topicId, topicName, topicDescription, createdBy, moduleId);
        }

        // Handle subscribing a student to a topic
        public async Task HandleSubscribe(Student student, int topicId)
        {
            if (student == null)
                throw new ArgumentNullException(nameof(student));

            await _controller.SubscribeStudentToTopic(student.UserID, topicId);
        }
    }
}
