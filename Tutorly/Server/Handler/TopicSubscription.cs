namespace Tutorly.Server.Handler
{
    public class TopicSubscription
    {
        public int StudentId { get; set; }
        public int TopicId { get; set; }

        public TopicSubscription(int studentId, int topicId)
        {
            StudentId = studentId;
            TopicId = topicId;
        }
    }

}
