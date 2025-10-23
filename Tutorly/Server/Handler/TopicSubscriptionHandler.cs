using Tutorly.Server.Controllers;
using Tutorly.Server.DatabaseModels;
using Supabase;

public class TopicSubscriptionHandler
{
    private readonly TopicController _controller;
    private readonly Supabase.Client _client;

    public TopicSubscriptionHandler(TopicController controller, Supabase.Client client)
    {
        _controller = controller;
        _client = client;
    }

    public async Task<bool> SubscribeStudent(TopicSubscriptionEntity subscription)
    {
        // add extra validation for null checks
        if (subscription.StudentId <= 0 || subscription.TopicId <= 0)
            return false;

        await _controller.SubscribeStudentToTopic(subscription.StudentId, subscription.TopicId);
        return true;
    }

    public async Task<bool> SubscribeStudentToModuleTopics(int studentId, int moduleId)
    {
        if (studentId <= 0 || moduleId <= 0) return false;

        // find topics under module
        var topicsResp = await _client.From<TopicEntity>()
            .Filter("module_id", Supabase.Postgrest.Constants.Operator.Equals, moduleId)
            .Get();

        foreach (var topic in topicsResp.Models)
        {
            var sub = new TopicSubscriptionEntity { StudentId = studentId, TopicId = topic.Id };
            await _controller.SubscribeStudentToTopic(studentId, topic.Id);
        }
        return true;
    }
}
