// BAPortalMVC.Services.OpenAiServices
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI;
namespace BAPortalMVC.Services
{
    public class OpenAiServices
    {
        private readonly IConfiguration _configuration;

        private readonly HttpClient _httpClient;

        private readonly ApplicationDbContext _context;

        private readonly OpenAIClient _client;

        public OpenAiServices(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
            string apiKey = _configuration["OpenAI:ApiKey"];
            _client = new OpenAIClient(apiKey);
        }

        public async Task<string> AskChatGPT(string prompt)
        {
            string url = "https://api.openai.com/v1/responses";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["OpenAI:ApiKey"]);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var payload = new
            {
                model = "gpt-4.1",
                input = prompt
            };
            string jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                return $"API error: {response.StatusCode} - {error}";
            }
            return await response.Content.ReadAsStringAsync();
        }
    }
}