namespace Tutorly.Shared
{

    public interface IEmbeddingApiService
    {

        Task InitializeAsync();

        Task<float[]> GetEmbeddingAsync(string text);

        Task StartAsync();


        Task StopAsync();
    }
}