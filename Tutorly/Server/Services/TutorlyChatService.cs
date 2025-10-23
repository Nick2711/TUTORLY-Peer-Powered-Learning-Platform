using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Tutorly.Shared;
using Azure.Storage.Blobs;
using System.Runtime.CompilerServices;

namespace Tutorly.Shared
{
    public class TutorlyChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _blobConnectionString;
        private readonly string _blobContainerName;
        private readonly Dictionary<string, float[]> _documentEmbeddings = new();

        public TutorlyChatService(HttpClient httpClient, string blobConnectionString, string blobContainerName)
        {
            _httpClient = httpClient;
            _blobConnectionString = blobConnectionString;
            _blobContainerName = blobContainerName;
        }

        public async Task InitializeAsync()
        {
            var containerClient = new BlobContainerClient(_blobConnectionString, _blobContainerName);

            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                string content;
                using (var stream = await blobClient.OpenReadAsync())
                using (var reader = new StreamReader(stream))
                {
                    content = await reader.ReadToEndAsync();
                }

                var embedding = await GetEmbeddingAsync(content);
                _documentEmbeddings[blobItem.Name] = embedding;
            }

        }

        #region python embedding service
        private async Task<float[]> GetEmbeddingAsync(string text)
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/embed", new { text });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<float>>>();
            if (result == null || !result.ContainsKey("embedding"))
                throw new Exception("Embedding API returned invalid data.");
            return result["embedding"].ToArray();

        }
        #endregion

        #region compute cosine similarity
        private float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0f, normA = 0f, normB = 0f; //single digit floating point values
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i]; //dot products
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            return dot / ((float)(Math.Sqrt(normA) * (Math.Sqrt(normB)))); //calc cosine similarity

        }
        #endregion

        #region retrieve most relevant documents for RAG 
        public async Task<List<string>> RetrieveTopDocumentsAsync(string query, int k = 3)
        //k amount of docs - test and change to fit model accuracy?
        {
            var queryVec = await GetEmbeddingAsync(query);
            return _documentEmbeddings
                .OrderByDescending(doc => CosineSimilarity(doc.Value, queryVec))
                .Take(k)
                .Select(doc => doc.Key)
                .ToList();
        }
        #endregion

    }
}
