
namespace Tutorly.Shared
{
    public class Topic
    {
        public Topic(int topicId, string topicName, string topicDescription, string createdBy, string moduleId)
        {
            TopicId = topicId;
            TopicName = topicName ?? throw new ArgumentNullException(nameof(topicName));
            TopicDescription = topicDescription ?? throw new ArgumentNullException(nameof(topicDescription));
            CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        }

        public int TopicId { get; set; }
        public string TopicName { get; set; }
        public string TopicDescription { get; set; }
        public string CreatedBy { get; set; }
        public string ModuleId { get; set; }

        public static List<Topic> TopicList { get; set; } = new();

        private readonly List<Student> subscribers = new();



        public void Subscribe(Student student)
        {
            if (!subscribers.Contains(student))
                subscribers.Add(student);
        }
    }

}
