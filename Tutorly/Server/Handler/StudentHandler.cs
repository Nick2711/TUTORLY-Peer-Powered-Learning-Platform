using Tutorly.Shared;

namespace Tutorly.Server.Handler
{
    public class StudentHandler: Student
    {
        public StudentHandler(bool active, int userId, string email, string name, string password, RoleType role) : base(active, userId, email, name, password, role)
        {
        }

        //public required int StudentID { get; set; }
        //public required string Name { get; set; }
        public List<Topic> SubscribedTopics { get; set; } = new List<Topic>();

        public void SubscribeToTopic(Topic topic)
        {
            SubscribedTopics.Add(topic);
            topic.Subscribe(this);
        }
    }
}
