using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Text;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

using System;
using System.Text;
using BAPortalMVC.Models;

namespace BAPortalMVC.Services
{
    public class GoogleServices
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public GoogleServices(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        public async Task<IList<GoogleFile>> ListDriveFilesAsync()
        {
            GoogleCredential credential;
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "badrive-baef0ef44a19.json");
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Service account file not found at {fullPath}");
            }

            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(DriveService.Scope.DriveReadonly);
            }

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive",
            });

            string folderId = "1koengU0pcVdPAAqHT1vOXcejuwVjsjMU";

            var listRequest = service.Files.List();

            listRequest.Q = $"'{folderId}' in parents and trashed = false";
            //listRequest.Fields = "files(id, name)";
            listRequest.Fields = "files(id, name, mimeType)";
            var files = await listRequest.ExecuteAsync();
            foreach (var file in files.Files)
            {
                Console.WriteLine($"File Name: {file.Name}, ID: {file.Id}, Type: {file.MimeType}");
            }
            return files.Files;
        }

        public async Task<string> ReadDriveFilesAsync(GoogleFileRequest googleFileRequest)
        {
            GoogleCredential credential;
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "badrive-baef0ef44a19.json");
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Service account file not found at {fullPath}");
            }

            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(DriveService.Scope.DriveReadonly);
            }

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive",
            });

            var request = service.Files.Get(googleFileRequest.GoogleFileId);
            var memoryStream = new MemoryStream();

            // Download the file content
            await request.DownloadAsync(memoryStream);

            // Reset the stream position to read from the beginning
            memoryStream.Position = 0;

            // Read the content as a string
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                string fileContent = await reader.ReadToEndAsync();
                var personalizedContent = fileContent
                    .Replace("[Insert Company Name]", googleFileRequest.CompanyName, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Video/Article]", googleFileRequest.MainContentType, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Insert Topic]", googleFileRequest.Topic, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Insert URL]", googleFileRequest.FinalDeliverableUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Insert Thumbnail URL]", googleFileRequest.ThumbnailUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Use for content reference]", googleFileRequest.TranscriptUrl, StringComparison.OrdinalIgnoreCase);

                return personalizedContent;
            }
        }
    }
}

