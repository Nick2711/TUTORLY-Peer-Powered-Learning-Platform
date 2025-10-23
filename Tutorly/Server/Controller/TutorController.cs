using Tutorly.Shared;
using Tutorly.Server.Helpers;
using Tutorly.Server.DatabaseModels;

public class TutorController
{
    private readonly Tutor _tutor;
    private readonly SupabaseClientFactory _supabase;

    public TutorController(Tutor tutor, SupabaseClientFactory supabase)
    {
        _tutor = tutor;
        _supabase = supabase;
    }

    public async Task AddNewTopic(int topicId, string name, string description, string createdBy, string moduleId)
    {
        try
        {
            var topic = new TopicEntity
            {
                Id = topicId,
                Name = name,
                Description = description,
                CreatedBy = createdBy,
                ModuleId = moduleId
            };

            await _supabase.AddEntity(topic);

            _tutor.Notify(new Topic(topicId, name, description, createdBy, moduleId));
        }
        catch (Exception ex)
        {
            //add as actual in browser error message
            Console.WriteLine($"Error adding new topic: {ex.Message}");
            throw; 
        }
    }
}
