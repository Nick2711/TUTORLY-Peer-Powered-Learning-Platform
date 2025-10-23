namespace Tutorly.Server.Helpers
{
    using Microsoft.Extensions.Options;
    using Supabase.Postgrest.Models;
    using Supabase;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ISupabaseClientFactory
    {
        Client CreateAnon();
        Client CreateService();
    }

    public class SupabaseClientFactory : ISupabaseClientFactory
    {
        private readonly SupabaseSettings _opt;

        public SupabaseClientFactory(IOptions<SupabaseSettings> opt)
        {
            _opt = opt.Value;

            // Initialize the default client with service role key
            _client = new Client(_opt.Url, _opt.ServiceRoleKey, new Supabase.SupabaseOptions { AutoConnectRealtime = false });
            _client.InitializeAsync().Wait();
        }

        public Client CreateAnon()
            => new Client(_opt.Url, _opt.AnonKey, new Supabase.SupabaseOptions { AutoConnectRealtime = false });

        public Client CreateService()
            => new Client(_opt.Url, _opt.ServiceRoleKey, new Supabase.SupabaseOptions { AutoConnectRealtime = false });

        // This is the single client we use for backend operations
        private readonly Supabase.Client _client;

        // Generic method to add any entity
        public async Task AddEntity<T>(T entity) where T : BaseModel, new()
        {
            await _client.From<T>().Insert(entity);
        }

        // Generic method to get all entities from a table
        public async Task<List<T>> GetEntities<T>() where T : BaseModel, new()
        {
            var result = await _client.From<T>().Get();
            return result.Models;
        }
    }
}
