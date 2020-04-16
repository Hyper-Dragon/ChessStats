using ChessDotComSharp.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ChessDotComSharp
{
    public partial class ChessDotComClient : IDisposable
    {
        private static HttpClient _client;

        public ChessDotComClient()
        {
            _client = new HttpClient();
        }

        private async Task<T> GetAsync<T>(string uri)
        {
            HttpResponseMessage response = await _client.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(content, Converter.Settings);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
