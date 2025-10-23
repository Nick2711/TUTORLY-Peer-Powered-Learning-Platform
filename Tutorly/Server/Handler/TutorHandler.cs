using System.Security.Cryptography.Xml;
using Tutorly.Server.Controller;
using Tutorly.Shared;

namespace Tutorly.Server.Handler
{
    public class TutorHandler
    {
        private readonly TutorController _controller;

        public TutorHandler(TutorController controller)
        {
            _controller = controller;
        }

        public void HandleAddTopic(int topicId, string name, string description, string createdBy, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Topic name cannot be empty");

            if (Topic.TopicList.Any(t => t.TopicId == topicId))
                throw new InvalidOperationException("Topic ID already exists");

            if (string.IsNullOrEmpty(createdBy))
                throw new ArgumentException("Topic must be assigned to creator");

            if (string.IsNullOrEmpty(moduleId))
                throw new ArgumentException($"Topic must be assigned to a module");

             _controller.AddNewTopic(topicId, name, description, createdBy, moduleId);
        }
    }

}
