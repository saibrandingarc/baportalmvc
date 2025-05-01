using System;
using System.Net.Http.Headers;
using System.Text;
using System.Data;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using Microsoft.Extensions.Configuration;

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
            var apiKey = _configuration["OpenAI:ApiKey"];
            _client = new OpenAIClient(apiKey);
        }

        public async Task<string> AskChatGPT(string prompt)
        {
            //ChatClient client = new(model: "gpt-4o", apiKey: _configuration["OpenAI:ApiKey"]);

            //ChatCompletion completion = client.CompleteChat(prompt);

            //Console.WriteLine($"[ASSISTANT]: {completion.Content[0].Text}");
            //return completion.Content[0].Text;
            var url = "https://api.openai.com/v1/responses";

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["OpenAI:ApiKey"]);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                model = "gpt-4.1",
                input = prompt
            };

            string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"API error: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}