// BAPortalMVC.Services.ZohoServices
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BAPortalMVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BAPortalMVC.Services
{
    public class ZohoServices
    {
        private readonly IConfiguration _configuration;

        private readonly HttpClient _httpClient;

        private readonly ApplicationDbContext _context;

        public ZohoServices(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        public string GetAuthorizationUrl()
        {
            string clientId = _configuration["Zoho:ClientId"];
            string redirectUri = _configuration["Zoho:RedirectUri"];
            string authEndpoint = _configuration["Zoho:AuthEndpoint"];
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(114, 3);
            defaultInterpolatedStringHandler.AppendFormatted(authEndpoint);
            defaultInterpolatedStringHandler.AppendLiteral("?scope=AaaServer.profile.Read,ZohoCRM.modules.ALL&client_id=");
            defaultInterpolatedStringHandler.AppendFormatted(clientId);
            defaultInterpolatedStringHandler.AppendLiteral("&response_type=token&access_type=offline&redirect_uri=");
            defaultInterpolatedStringHandler.AppendFormatted(redirectUri);
            return defaultInterpolatedStringHandler.ToStringAndClear();
        }

        public async Task<string> GetAccessTokenAsync()
        {
            string clientId = _configuration["Zoho:ClientId"];
            string clientSecret = _configuration["Zoho:ClientSecret"];
            _ = _configuration["Zoho:TokenEndpoint"];
            string code = _configuration["Zoho:code"];
            HttpClient client = new HttpClient();
            Dictionary<string, string> parameters = new Dictionary<string, string>
        {
            { "code", code },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "authorization_code" }
        };
            try
            {
                HttpResponseMessage response = await client.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));
                if (response.IsSuccessStatusCode)
                {
                    Dictionary<string, string> responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());
                    if (responseData.ContainsKey("error"))
                    {
                        Console.WriteLine("Error: Missing required key");
                        return "";
                    }
                    Console.WriteLine("no errors");
                    return await AddSettingsAsync(responseData);
                }
                throw new Exception("Failed to regenerate token.");
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetValidTokenAsync()
        {
            Settings settings = await (from p in _context.Settings
                                       where p.tokenFrom == "zoho"
                                       orderby p.DateAdded descending
                                       select p).FirstOrDefaultAsync();
            if (settings == null)
            {
                if (await GetAccessTokenAsync() == "")
                {
                    return "Invalid Token";
                }
                settings = await (from p in _context.Settings
                                  where p.tokenFrom == "zoho"
                                  orderby p.DateAdded descending
                                  select p).FirstOrDefaultAsync();
            }
            DateTime expirationTime = settings.DateAdded.AddSeconds(settings.ExpiresIn);
            Console.WriteLine(expirationTime);
            Console.WriteLine(DateTime.UtcNow);
            if (DateTime.UtcNow >= expirationTime)
            {
                string newToken = (settings.AccessToken = await RegenerateTokenAsync(settings.RefreshToken));
                settings.DateAdded = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return newToken;
            }
            return settings.AccessToken;
        }

        private async Task<string> RegenerateTokenAsync(string refreshToken)
        {
            using HttpClient httpClient = new HttpClient();
            string clientId = _configuration["Zoho:ClientId"];
            string clientSecret = _configuration["Zoho:ClientSecret"];
            _ = _configuration["Zoho:TokenEndpoint"];
            _ = _configuration["Zoho:code"];
            Dictionary<string, string> parameters = new Dictionary<string, string>
        {
            { "refresh_token", refreshToken },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "refresh_token" }
        };
            try
            {
                HttpResponseMessage response = await httpClient.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));
                if (response.IsSuccessStatusCode)
                {
                    await (from p in _context.Settings
                           where p.tokenFrom == "zoho"
                           orderby p.DateAdded descending
                           select p).FirstOrDefaultAsync();
                    Dictionary<string, string> responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());
                    return responseData["access_token"];
                }
                throw new Exception("Failed to regenerate token.");
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string?> AddSettingsAsync(Dictionary<string, string>? responseData)
        {
            string accessToken = responseData!["access_token"];
            Settings existData = await (from p in _context.Settings
                                        where p.tokenFrom == "zoho"
                                        orderby p.DateAdded descending
                                        select p).FirstOrDefaultAsync();
            if (existData != null)
            {
                existData.DateAdded = DateTime.UtcNow;
                existData.AccessToken = accessToken;
                await _context.SaveChangesAsync();
            }
            else
            {
                Settings newSetting = new Settings
                {
                    AccessToken = responseData!["access_token"],
                    RefreshToken = responseData!["refresh_token"],
                    Scope = responseData!["scope"],
                    ExpiresIn = int.Parse(responseData!["expires_in"]),
                    DateAdded = DateTime.UtcNow,
                    tokenFrom = "zoho"
                };
                _context.Settings.Add(newSetting);
            }
            await _context.SaveChangesAsync();
            return accessToken;
        }

        public async Task<string> GetZohoDeliverablesByIdAsync(string DeliverableId)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine("Access Token : " + accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                string query = "\n                    {\n                        \"select_query\": \"select id, Active_Evergreen, Transcript_URL, Thumbnail_URL, Final_Deliverable, GDrive_Folder, Tool_Upload_Status,Quarter, Block, Credit_Multiplier, Credit_Cost, Main_Status, Topic_Category, Sub_Category_Article, Sub_Category_Graphics, Type_Category_Mass_Email, Type_Category_SEO, Sub_Category_Social, Sub_Category_Website, Sub_Category_YouTube1, Sub_Category_Other, Name, Short_Description, Company.id, Company.Account_Name, Company.Website, Email, Priority, Due_Date, Staff_Manager, Staff_SEO, Staff_Client_Contact, Admin_Approval.first_name as Admin_Approval_First_Name, Admin_Approval.last_name as Admin_Approval_Last_Name,Deliverable_Author, Quality_Control, Graphic_Designer, Web_Designer from Deliverables where (id=" + DeliverableId + ")\"\n                    }";
                StringContent content = new StringContent(query, Encoding.UTF8, "application/json");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                {
                    Content = content
                };
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                JToken firstRecord = json["data"]!.First;
                return firstRecord.ToString();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<List<Deliverable>?> GetZohoDeliverablesByAccountIdAsync(string AccountId)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine("Access Token : " + accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                string query = "\n                    {\n                        \"select_query\": \"select id, Active_Evergreen, Quarter, Block, Main_Status, Topic_Category, Name, Company.id as Company_Id, Company.Account_Name as Company_Name, Company.Website as Company_Website, GDocs_Content from Deliverables where (Company.id=" + AccountId + ")\"\n                    }";
                StringContent content = new StringContent(query, Encoding.UTF8, "application/json");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                {
                    Content = content
                };
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                string dataArray = json["data"]?.ToString();
                return JsonConvert.DeserializeObject<List<Deliverable>>(dataArray);
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return null;
            }
        }

        public async Task<List<Deliverable>?> GetZohoDeliverablesByAccountIdCategoryYearAsync(string AccountId, string Category, string year)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine("Access Token : " + accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                //var client = new HttpClient();
                //var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql");
                //request.Headers.Add("Authorization", "Bearer " + accessToken);
                ////request.Headers.Add("Cookie", "_zcsr_tmp=310c83be-41a0-4305-89a7-7a77f529cd5b; crmcsr=310c83be-41a0-4305-89a7-7a77f529cd5b");
                //var content = new StringContent("\n{\n                    \"select_query\": \"select id, Active_Evergreen, Quarter, Block, Main_Status, Topic_Category, Name, Company.id as Company_Id, Company.Account_Name as Company_Name, Company.Website as Company_Website, GDocs_Content from Deliverables where (Company.id=3293516000000866604 and Topic_Category=Holidays) ORDER BY Due_Date DESC\"\n                }", null, "application/json");
                //request.Content = content;
                //var response = await _httpClient.SendAsync(request);
                //response.EnsureSuccessStatusCode();
                //Console.WriteLine(await response.Content.ReadAsStringAsync());
                string query = $@"
                {{
                    ""select_query"": ""select id, Active_Evergreen, Quarter, Block, Main_Status, Topic_Category, Name, Company.id as Company_Id, Company.Account_Name as Company_Name, Company.Website as Company_Website, GDocs_Content from Deliverables where (Company.id={AccountId} and Topic_Category={Category}) ORDER BY Due_Date DESC""
                }}";
                
                var content = new StringContent(query, Encoding.UTF8, "application/json");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                {
                    Content = content
                };
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                string dataArray = json["data"]?.ToString();
                return JsonConvert.DeserializeObject<List<Deliverable>>(dataArray);
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return null;
            }
        }

        public async Task<HttpResponseMessage> UploadFileToZohoAsync(string deliverableId, string filePath)
        {
            string accessToken = await GetValidTokenAsync();
            string url = "https://www.zohoapis.com/crm/v7/Deliverables/" + deliverableId + "/Attachments";
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);
            using MultipartFormDataContent multipart = new MultipartFormDataContent();
            using FileStream fileStream = File.OpenRead(filePath);
            StreamContent fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(fileContent, "file", Path.GetFileName(filePath));
            return await client.PostAsync(url, multipart);
        }

        public async Task<string> GetZohoContactByEmailAsync(string email)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Contacts/search?email=" + email)
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetZohoCasesAsync()
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases?fields=Case_Number,Subject,Status,Account_Name")
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetZohoCasesByCompanyAsync(string companyId)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/search?criteria=Account_Name.id:equals:" + companyId)
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetZohoCasesByCompanyForDahsboardAsync(string companyId)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                string formattedDate = DateTime.UtcNow.AddYears(-2).ToString("yyyy-MM-dd");
                string criteria = "(Case_Open_Date:greater_than:" + formattedDate + ")";
                string criteria2 = "(Account_Name.id:equals: " + companyId + ")";
                Uri.EscapeDataString(criteria);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/search?criteria=(" + criteria + " and " + criteria2 + ")")
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetZohoCaseByIdAsync(string companyId, string caseNumber)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/" + caseNumber)
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetZohoDeliverablesByCompanyAsync(string companyId)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Deliverables/search?criteria=Company.id:equals:" + companyId)
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetZohoDeliverablesByCompanyAndBlockAsync(string companyId, string blockId)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                string query = $@"
                {{
                    ""select_query"": ""select id, Tool_Upload_Status, Quarter, Block, Credit_Multiplier, Credit_Cost, Main_Status, Topic_Category, Sub_Category_Article, Sub_Category_Graphics, Type_Category_Mass_Email, Type_Category_SEO, Sub_Category_Social, Sub_Category_Website, Sub_Category_YouTube1, Sub_Category_Other, Name, Short_Description, Company.id, Company.Account_Name, Email, Priority, Due_Date, Staff_Manager, Staff_SEO, Staff_Client_Contact, Admin_Approval.first_name as Admin_Approval_First_Name, Admin_Approval.last_name as Admin_Approval_Last_Name, Deliverable_Author, Quality_Control, Graphic_Designer, Web_Designer 
                    from Deliverables 
                    where (Company={companyId} and Block like '{blockId}%')""
                }}";
                StringContent content = new StringContent(query, Encoding.UTF8, "application/json");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                {
                    Content = content
                };
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request failed: {response}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> UpdateZohoCasesAsync(string jsonPayload, string id)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                Console.WriteLine(content);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, "https://www.zohoapis.com/crm/v7/Cases/" + id);
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Content = content;
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine(response);
                response.EnsureSuccessStatusCode();
                string response_json = await response.Content.ReadAsStringAsync();
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return response_json.ToString();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> checkEmail(string email)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Contacts/search?email=" + email)
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                StringContent content = (StringContent)(request.Content = new StringContent("", null, "text/plain"));
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string userInfo = await response.Content.ReadAsStringAsync();
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return userInfo;
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetZohoCompanies()
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Accounts/search?criteria=(MD_Status:equals:Active)&fields=Account_Name,Short_Name")
                {
                    Headers = {
                {
                    "Authorization",
                    "Bearer " + accessToken
                } }
                };
                StringContent content = (StringContent)(request.Content = new StringContent("", null, "text/plain"));
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string userInfo = await response.Content.ReadAsStringAsync();
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return userInfo;
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> DeleteZohoCasesAsync(string id)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, "https://www.zohoapis.com/crm/v7/Cases/" + id);
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine(response);
                response.EnsureSuccessStatusCode();
                string response_json = await response.Content.ReadAsStringAsync();
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return response_json.ToString();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> GetAccountByIdAsync(string AccountId)
        {
            string accessToken = await GetValidTokenAsync();
            Console.WriteLine("Access Token : " + accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                string query = "\n                    {\n                        \"select_query\": \"select id, Account_Name, Website from Accounts where (id=" + AccountId + ")\"\n                    }";
                StringContent content = new StringContent(query, Encoding.UTF8, "application/json");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                {
                    Content = content
                };
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine($"Request : {response}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return null;
            }
        }

        public async Task<string> UpdateZohoDeliverableAsync(string deliverableId, string jsonPayload)
        {
            string accessToken = await GetValidTokenAsync();
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            try
            {
                string url = "https://www.zohoapis.com/crm/v7/Deliverables/" + deliverableId;
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                Console.WriteLine(content);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Content = content;
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                Console.WriteLine(response);
                response.EnsureSuccessStatusCode();
                string response_json = await response.Content.ReadAsStringAsync();
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return response_json.ToString();
            }
            catch (HttpRequestException ex2)
            {
                Console.WriteLine("Request failed: " + ex2.Message);
                return ex2.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return ex.Message;
            }
        }
    }
}