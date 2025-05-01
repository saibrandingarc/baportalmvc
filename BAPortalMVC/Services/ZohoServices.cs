using System;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Principal;
using System.Text;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using BAPortalMVC.Models;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

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
            var clientId = _configuration["Zoho:ClientId"];
            var redirectUri = _configuration["Zoho:RedirectUri"];
            var authEndpoint = _configuration["Zoho:AuthEndpoint"];

            return $"{authEndpoint}?scope=AaaServer.profile.Read,ZohoCRM.modules.ALL&client_id={clientId}&response_type=token&access_type=offline&redirect_uri={redirectUri}";
        }

        // Method to Get Access Token
        public async Task<string> GetAccessTokenAsync()
        {
            var clientId = _configuration["Zoho:ClientId"];
            var clientSecret = _configuration["Zoho:ClientSecret"];
            var tokenEndpoint = _configuration["Zoho:TokenEndpoint"];
            var code = _configuration["Zoho:code"];

            var client = new HttpClient();
            var parameters = new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "authorization_code" }
            };
            try
            {
                var response = await client.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (responseData.ContainsKey("error"))
                    {
                        Console.WriteLine("Error: Missing required key");
                        return "";
                    }
                    else
                    {
                        Console.WriteLine("no errors");
                        var accessToken = await AddSettingsAsync(responseData);
                        return accessToken;
                    }
                }
                else
                {
                    throw new Exception("Failed to regenerate token.");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
                return ex.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return ex.Message;
            }
        }

        public async Task<string> GetValidTokenAsync()
        {
            var settings = await _context.Settings
                        .Where(p => p.tokenFrom == "zoho")
                        .OrderByDescending(p => p.DateAdded)
                        .FirstOrDefaultAsync();

            if (settings == null)
            {
                var tokenObj = await GetAccessTokenAsync();
                if (tokenObj == "")
                {
                    return "Invalid Token";
                }
                else
                {
                    settings = await _context.Settings
                        .Where(p => p.tokenFrom == "zoho")
                        .OrderByDescending(p => p.DateAdded)
                        .FirstOrDefaultAsync();
                }
            }

            // Calculate expiration time
            var expirationTime = settings.DateAdded.AddSeconds(settings.ExpiresIn);
            Console.WriteLine(expirationTime);
            Console.WriteLine(DateTime.UtcNow);
            if (DateTime.UtcNow >= expirationTime)
            {
                // Token has expired, regenerate using refresh token
                var newToken = await RegenerateTokenAsync(settings.RefreshToken);

                // Update the token and date in the settings table
                settings.AccessToken = newToken;
                settings.DateAdded = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return newToken;
            }

            // Token is still valid
            return settings.AccessToken;
        }

        // Method to regenerate the token using the refresh token
        private async Task<string> RegenerateTokenAsync(string refreshToken)
        {
            // Replace with actual API call to Zoho to regenerate the token
            using (var httpClient = new HttpClient())
            {
                var clientId = _configuration["Zoho:ClientId"];
                var clientSecret = _configuration["Zoho:ClientSecret"];
                var tokenEndpoint = _configuration["Zoho:TokenEndpoint"];
                var code = _configuration["Zoho:code"];
                var parameters = new Dictionary<string, string>
                {
                    { "refresh_token", refreshToken },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "grant_type", "refresh_token" }
                };

                try
                {
                    var response = await httpClient.PostAsync("https://accounts.zoho.com/oauth/v2/token", new FormUrlEncodedContent(parameters));

                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await _context.Settings
                            .Where(p => p.tokenFrom == "zoho")
                            .OrderByDescending(p => p.DateAdded)
                            .FirstOrDefaultAsync();

                        var json = await response.Content.ReadAsStringAsync();
                        var responseData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                        // Return the new access token
                        return responseData["access_token"];
                    }
                    else
                    {
                        throw new Exception("Failed to regenerate token.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string?> AddSettingsAsync(Dictionary<string, string>? responseData)
        {
            var accessToken = responseData["access_token"];
            var existData = await _context.Settings
                .Where(p => p.tokenFrom == "zoho")
                .OrderByDescending(p => p.DateAdded)
                .FirstOrDefaultAsync();

            if (existData != null)
            {
                existData.DateAdded = DateTime.UtcNow;
                existData.AccessToken = accessToken;

                // Save changes
                await _context.SaveChangesAsync();
            }
            else
            {
                var newSetting = new Settings
                {
                    AccessToken = responseData["access_token"],
                    RefreshToken = responseData["refresh_token"],
                    Scope = responseData["scope"],
                    ExpiresIn = int.Parse(responseData["expires_in"]),
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
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine("Access Token : "+accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var query = $@"
                    {{
                        ""select_query"": ""select id, Transcript_URL, Thumbnail_URL, Final_Deliverable, GDrive_Folder, Tool_Upload_Status,Quarter, Block, Credit_Multiplier, Credit_Cost, Main_Status, Topic_Category, Sub_Category_Article, Sub_Category_Graphics, Type_Category_Mass_Email, Type_Category_SEO, Sub_Category_Social, Sub_Category_Website, Sub_Category_YouTube1, Sub_Category_Other, Name, Short_Description, Company.id, Company.Account_Name, Email, Priority, Due_Date, Staff_Manager, Staff_SEO, Staff_Client_Contact, Admin_Approval.first_name as Admin_Approval_First_Name, Admin_Approval.last_name as Admin_Approval_Last_Name,Deliverable_Author, Quality_Control, Graphic_Designer, Web_Designer from Deliverables where (id={DeliverableId})""
                    }}";
                    var content = new StringContent(query, Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                    {
                        Content = content
                    };

                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request : {response}");
                    response.EnsureSuccessStatusCode();
                    var deliverables = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(deliverables);
                    var firstRecord = json["data"].First;
                    return firstRecord.ToString();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<HttpResponseMessage> UploadFileToZohoAsync(string deliverableId, string filePath)
        {
            var accessToken = await GetValidTokenAsync();

            string url = $"https://www.zohoapis.com/crm/v7/Deliverables/{deliverableId}/Attachments";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);

            using var multipart = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            multipart.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await client.PostAsync(url, multipart);
            return response;
        }

        public async Task<string> GetZohoContactByEmailAsync(string email)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Contacts/search?email=" + email)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var contact = await response.Content.ReadAsStringAsync();
                    return contact;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCasesAsync()
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases?fields=Case_Number,Subject,Status,Account_Name")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var cases = await response.Content.ReadAsStringAsync();
                    return cases;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCasesByCompanyAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/search?criteria=Account_Name.id:equals:" + companyId)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var cases = await response.Content.ReadAsStringAsync();
                    return cases;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCasesByCompanyForDahsboardAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    // Calculate the date 2 years ago
                    DateTime twoYearsAgo = DateTime.UtcNow.AddYears(-2);
                    string formattedDate = twoYearsAgo.ToString("yyyy-MM-dd");

                    // Build the criteria
                    string criteria = $"(Case_Open_Date:greater_than:{formattedDate})";
                    string criteria2 = "(Account_Name.id:equals: " + companyId + ")";
                    string encodedCriteria = Uri.EscapeDataString(criteria);

                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/search?criteria=(" + criteria + " and " + criteria2 + ")")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    //response.EnsureSuccessStatusCode();
                    if (response.IsSuccessStatusCode)
                    {
                        var cases = await response.Content.ReadAsStringAsync();
                        return cases;
                    }

                    // Handle failure
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return errorContent;

                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCaseByIdAsync(string companyId, string caseNumber)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Cases/" + caseNumber)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var cases = await response.Content.ReadAsStringAsync();
                    return cases;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoDeliverablesByCompanyAsync(string companyId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Deliverables/search?criteria=Company.id:equals:" + companyId)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();

                    var deliverables = await response.Content.ReadAsStringAsync();
                    return deliverables;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoDeliverablesByCompanyAndBlockAsync(string companyId, string blockId)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var query = $@"
                    {{
                        ""select_query"": ""select id, Tool_Upload_Status, Quarter, Block, Credit_Multiplier, Credit_Cost, Main_Status, Topic_Category, Sub_Category_Article, Sub_Category_Graphics, Type_Category_Mass_Email, Type_Category_SEO, Sub_Category_Social, Sub_Category_Website, Sub_Category_YouTube1, Sub_Category_Other, Name, Short_Description, Company.id, Company.Account_Name, Email, Priority, Due_Date, Staff_Manager, Staff_SEO, Staff_Client_Contact, Admin_Approval.first_name as Admin_Approval_First_Name, Admin_Approval.last_name as Admin_Approval_Last_Name, Deliverable_Author, Quality_Control, Graphic_Designer, Web_Designer from Deliverables where (Company={companyId} and Block like '{blockId}%')""
                    }}";
                    var content = new StringContent(query, Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/coql")
                    {
                        Content = content
                    };

                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Request failed: {response}");
                    response.EnsureSuccessStatusCode();
                    var deliverables = await response.Content.ReadAsStringAsync();
                    return deliverables;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        //public async Task<string> SaveZohoCasesAsync(CaseModel caseObj)
        //{
        //    var accessToken = await GetValidTokenAsync();
        //    Console.WriteLine(accessToken);
        //    if (accessToken == "Invalid Token")
        //    {
        //        return null;
        //    }
        //    else
        //    {
        //        try
        //        {
        //            var acc = new Account { id = caseObj.Account_Name.id };
        //            var caseData = new
        //            {
        //                data = new[]
        //                {
        //                    new CaseModel
        //                    {
        //                        //Owner = new Owner { id = "user-id" },
        //                        //Product_Name = new Owner { id = "product-id" },
        //                        //Deal_Name = new Owner { id = "deal-id" },
        //                        Account_Name = acc,
        //                        //Related_To = new Owner { id = "contact-id" },
        //                        Status = "New",
        //                        Email = caseObj.Email,
        //                        Description = caseObj.Description,
        //                        Internal_Comments = "",
        //                        Priority = "Medium",
        //                        Reported_By = "",
        //                        Case_Origin = "",
        //                        Case_Reason = "",
        //                        Subject = caseObj.Subject,
        //                        Type = "",
        //                        Phone = "",
        //                        Email_Notes1 = caseObj.Email_Notes1
        //                    }
        //                }
        //            };

        //            // Serialize the case data to JSON
        //            string jsonPayload = System.Text.Json.JsonSerializer.Serialize(caseData);
        //            Console.WriteLine(jsonPayload);
        //            // Create StringContent with the JSON payload, UTF-8 encoding, and application/json MIME type
        //            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        //            Console.WriteLine(content);

        //            var request = new HttpRequestMessage(HttpMethod.Post, "https://www.zohoapis.com/crm/v7/Cases");
        //            request.Headers.Add("Authorization", $"Bearer {accessToken}");

        //            //var content = new StringContent("{\n\t\"data\": [\n\t\t{\n            \"Account_Name\": {\n                \"id\": \"3293516000108143104\"\n            },\n            \"Status\": \"\",\n            \"Description\": \"test description\",\n            \"Internal_Comments\": \"\",\n            \"Priority\": \"\",\n            \"Reported_By\": \"\",\n            \"Case_Origin\": \"\",\n            \"Case_Reason\": \"\",\n            \"Subject\": \"test subject 1\",\n            \"Type\": \"\",\n            \"Phone\": \"\"\n        }\n\t]\n}", null, "application/json");
        //            request.Content = content;
        //            var response = await _httpClient.SendAsync(request);
        //            Console.WriteLine(response);
        //            response.EnsureSuccessStatusCode();
        //            var response_json = await response.Content.ReadAsStringAsync();
        //            Console.WriteLine(await response.Content.ReadAsStringAsync());
        //            return response_json.ToString();
        //        }
        //        catch (HttpRequestException ex)
        //        {
        //            Console.WriteLine($"Request failed: {ex.Message}");
        //            return ex.Message;
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"An error occurred: {ex.Message}");
        //            return ex.Message;
        //        }
        //    }
        //}

        public async Task<string> UpdateZohoCasesAsync(string jsonPayload, string id)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    // Create StringContent with the JSON payload, UTF-8 encoding, and application/json MIME type
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    Console.WriteLine(content);

                    var request = new HttpRequestMessage(HttpMethod.Put, "https://www.zohoapis.com/crm/v7/Cases/" + id);
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");

                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine(response);
                    response.EnsureSuccessStatusCode();
                    var response_json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return response_json.ToString();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> checkEmail(string email)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Contacts/search?email=" + email)
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };
                    var content = new StringContent("", null, "text/plain");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    var userInfo = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return userInfo;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> GetZohoCompanies()
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.zohoapis.com/crm/v7/Accounts/search?criteria=(MD_Status:equals:Active)&fields=Account_Name,Short_Name")
                    {
                        Headers = { { "Authorization", $"Bearer {accessToken}" } }
                    };
                    var content = new StringContent("", null, "text/plain");
                    request.Content = content;
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    var userInfo = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return userInfo;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        public async Task<string> DeleteZohoCasesAsync(string id)
        {
            var accessToken = await GetValidTokenAsync();
            Console.WriteLine(accessToken);
            if (accessToken == "Invalid Token")
            {
                return null;
            }
            else
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Delete, "https://www.zohoapis.com/crm/v7/Cases/" + id);
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine(response);
                    response.EnsureSuccessStatusCode();
                    var response_json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    return response_json.ToString();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }

        internal Task GetZohoDeliverablesByCompanyAndBlockAsync(string companyId)
        {
            throw new NotImplementedException();
        }
    }
}