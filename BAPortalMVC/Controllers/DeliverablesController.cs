// BAPortalMVC.Controllers.DeliverablesController
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BAPortalMVC.Controllers;
using BAPortalMVC.Models;
using BAPortalMVC.Services;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Drive.v3.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BAPortalMVC.Controllers;

public class DeliverablesController : Controller
{
    private readonly ILogger<DeliverablesController> _logger;

    private readonly ZohoServices _zohoService;

    private readonly GoogleServices _googleService;

    private readonly OpenAiServices _openAiService;

    private object googleServices;

    public DeliverablesController(ZohoServices zohoService, GoogleServices googleService, OpenAiServices openAiService, ILogger<DeliverablesController> logger)
    {
        _zohoService = zohoService;
        _googleService = googleService;
        _openAiService = openAiService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View("Deliverables");
    }

    public async Task<IActionResult> DeliverableAsync([FromRoute(Name = "id")] string DeliverableId)
    {
        string deliverable = await _zohoService.GetZohoDeliverablesByIdAsync(DeliverableId);
        JObject json = JObject.Parse(deliverable);
        string Sub_Category_Social = (string?)json["Sub_Category_Social"];
        if (Sub_Category_Social == "Holiday Post")
        {
            HolidayFileModel model2 = new HolidayFileModel
            {
                DeliverableId = DeliverableId,
                HolidayList = "",
                CompanyName = (string?)json["Company.Account_Name"],
                CompanyWebsite = (string?)json["Company.Website"]
            };
            await _googleService.ListDriveFilesAsync();
            base.ViewBag.ErrorMessage = "";
            base.ViewBag.Confirmation = "";
            return View("HolidayPost", model2);
        }
        DeliverableFileModel model = new DeliverableFileModel
        {
            DeliverableId = DeliverableId,
            EditedText = ""
        };
        string transcriptUrl = (string?)json["Transcript_URL"];
        string thumbnailUrl = (string?)json["Thumbnail_URL"];
        string finalDeliverable = (string?)json["Final_Deliverable"];
        base.ViewBag.ErrorMessage = "";
        base.ViewBag.Id = "Transcript URL : " + transcriptUrl;
        if (string.IsNullOrEmpty(transcriptUrl))
        {
            base.ViewBag.ErrorMessage = "Transcript URL Not exist";
        }
        if (string.IsNullOrEmpty(thumbnailUrl))
        {
            base.ViewBag.ErrorMessage = "Thumbnail URL Not exist";
        }
        if (string.IsNullOrEmpty(finalDeliverable))
        {
            base.ViewBag.ErrorMessage = "Final Deliverable URL Not exist";
        }
        if (base.ViewBag.ErrorMessage == "")
        {
            bool ActiveEverGreen = (bool)json["Active_Evergreen"];
            if (!ActiveEverGreen)
            {
                base.ViewBag.Confirmation = "It is not Evergreen." + ActiveEverGreen;
            }
            else
            {
                GoogleFileRequest googleFileRequest = new GoogleFileRequest
                {
                    CompanyName = (string?)json["Company.Account_Name"],
                    MainContentType = (string?)json["Main_Status"],
                    Topic = (string?)json["Name"],
                    FinalDeliverableUrl = (string?)json["Final_Deliverable"],
                    ThumbnailUrl = (string?)json["Thumbnail_URL"],
                    GoogleFileId = "1UmD6IvcvSqrHDb3KIo0Rz_5txh6DvH5o"
                };
                string personalizedContent = (await _googleService.ReadDriveFilesAsync(googleFileRequest)).Replace("[Insert Company Name]", googleFileRequest.CompanyName, StringComparison.OrdinalIgnoreCase).Replace("[Company Website]", googleFileRequest.CompanyWebsite, StringComparison.OrdinalIgnoreCase).Replace("[HolidaysList]", googleFileRequest.HolidaysList, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Insert URL]", googleFileRequest.FinalDeliverableUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Insert Thumbnail URL]", googleFileRequest.ThumbnailUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Use for content reference]", googleFileRequest.TranscriptUrl, StringComparison.OrdinalIgnoreCase);
                JsonDocument doc = JsonDocument.Parse(await _openAiService.AskChatGPT(personalizedContent));
                string outputText = doc.RootElement.GetProperty("output")[0].GetProperty("content")[0].GetProperty("text").GetString();
                model = new DeliverableFileModel
                {
                    DeliverableId = DeliverableId,
                    EditedText = outputText
                };
                base.ViewBag.Confirmation = "";
            }
        }
        base.ViewBag.ErrorMessage = deliverable;
        return View("Deliverable", model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = (Activity.Current?.Id ?? base.HttpContext.TraceIdentifier)
        });
    }

    [HttpPost]
    public async Task<IActionResult> DeliverableFileUploadAsync(DeliverableFileModel model)
    {
        string filePath = Path.Combine(Path.GetTempPath(), model.DeliverableId + "-openai_response.txt");
        await System.IO.File.WriteAllTextAsync(filePath, model.EditedText, Encoding.UTF8);
        if ((await _zohoService.UploadFileToZohoAsync(model.DeliverableId, filePath)).StatusCode != HttpStatusCode.OK)
        {
            base.ViewBag.ErrorMessage = "Document Upload Failed!";
            base.ViewBag.Confirmation = "";
        }
        else
        {
            base.ViewBag.Confirmation = "Document Uploaded successfully!";
            base.ViewBag.ErrorMessage = "";
        }
        return View("Deliverable", model);
    }

    [HttpPost]
    public async Task<IActionResult> HolidayAIGenerate(HolidayFileModel model)
    {
        GoogleFileRequest googleFileRequest = new GoogleFileRequest
        {
            CompanyName = model.CompanyName,
            CompanyWebsite = model.CompanyName,
            GoogleFileId = "1oAP8Co7UD4UlO6QOWABFUg6aEYxWC3cA",
            HolidaysList = model.HolidayList
        };
        string personalizedContent = (await _googleService.ReadDriveFilesAsync(googleFileRequest)).Replace("[Company Name]", googleFileRequest.CompanyName, StringComparison.OrdinalIgnoreCase).Replace("[Company Website]", googleFileRequest.CompanyWebsite, StringComparison.OrdinalIgnoreCase).Replace("[HolidaysList]", googleFileRequest.HolidaysList, StringComparison.OrdinalIgnoreCase);
        JsonDocument doc = JsonDocument.Parse(await _openAiService.AskChatGPT(personalizedContent));
        string outputText = (model.FileContent = doc.RootElement.GetProperty("output")[0].GetProperty("content")[0].GetProperty("text").GetString());
        base.ViewBag.ErrorMessage = "";
        base.ViewBag.Confirmation = "";
        return View("HolidayPost", model);
    }

    [HttpGet("/AccountsEvergreen1/{id}")]
    public async Task<IActionResult> AccountsEvergreenAsync([FromRoute(Name = "id")] string AccountId)
    {
        using JsonDocument doc2 = JsonDocument.Parse(await _zohoService.GetAccountByIdAsync(AccountId));
        string accountName = Regex.Replace(doc2.RootElement.GetProperty("data")[0].GetProperty("Account_Name").GetString() ?? "", "[^a-zA-Z0-9\\s]", "").Replace(" ", "_");
        Console.WriteLine("Account Name: " + accountName);
        List<Deliverable> EvergreenList = (await _zohoService.GetZohoDeliverablesByAccountIdAsync(AccountId)).Where((Deliverable d) => d.Active_Evergreen).ToList();
        if (EvergreenList.Any())
        {
            string folderId = await _googleService.GetFolderIdByNameAsync("Evergreen");
            string fileName = $"EverGreen_{accountName}_{DateTime.Now}.docx";
            string googleDocStatus = await _googleService.CreateGoogleDocAsync(fileName, folderId);
            List<DeliverableFileModel> fileModels = new List<DeliverableFileModel>();
            string fileId = googleDocStatus.Split("/d/")[1].Split("/")[0];
            new List<Request>();
            foreach (Deliverable item in EvergreenList)
            {
                string id = item.id;
                Console.WriteLine("Active Evergreen Item Id: " + id);
                JObject json = JObject.Parse(await _zohoService.GetZohoDeliverablesByIdAsync(id));
                new DeliverableFileModel
                {
                    DeliverableId = id,
                    EditedText = ""
                };
                string transcriptUrl = (string?)json["Transcript_URL"];
                string thumbnailUrl = (string?)json["Thumbnail_URL"];
                string finalDeliverable = (string?)json["Final_Deliverable"];
                base.ViewBag.ErrorMessage = "";
                base.ViewBag.Id = "Transcript URL : " + transcriptUrl;
                if (string.IsNullOrEmpty(transcriptUrl))
                {
                    base.ViewBag.ErrorMessage = "Transcript URL Not exist";
                }
                if (string.IsNullOrEmpty(thumbnailUrl))
                {
                    base.ViewBag.ErrorMessage = "Thumbnail URL Not exist";
                }
                if (string.IsNullOrEmpty(finalDeliverable))
                {
                    base.ViewBag.ErrorMessage = "Final Deliverable URL Not exist";
                }
                if (base.ViewBag.ErrorMessage == "")
                {
                    bool ActiveEverGreen = (bool)json["Active_Evergreen"];
                    if (ActiveEverGreen)
                    {
                        GoogleFileRequest googleFileRequest = new GoogleFileRequest
                        {
                            CompanyName = (string?)json["Company.Account_Name"],
                            MainContentType = (string?)json["Main_Status"],
                            Topic = (string?)json["Name"],
                            FinalDeliverableUrl = (string?)json["Final_Deliverable"],
                            ThumbnailUrl = (string?)json["Thumbnail_URL"],
                            GoogleFileId = "1UmD6IvcvSqrHDb3KIo0Rz_5txh6DvH5o"
                        };
                        string personalizedContent = (await _googleService.ReadDriveFilesAsync(googleFileRequest)).Replace("[Insert Company Name]", googleFileRequest.CompanyName, StringComparison.OrdinalIgnoreCase).Replace("[Company Website]", googleFileRequest.CompanyWebsite, StringComparison.OrdinalIgnoreCase).Replace("[Insert Topic]", googleFileRequest.Topic, StringComparison.OrdinalIgnoreCase)
                            .Replace("[Insert URL]", googleFileRequest.FinalDeliverableUrl, StringComparison.OrdinalIgnoreCase)
                            .Replace("[Insert Thumbnail URL]", googleFileRequest.ThumbnailUrl, StringComparison.OrdinalIgnoreCase)
                            .Replace("[Use for content reference]", googleFileRequest.TranscriptUrl, StringComparison.OrdinalIgnoreCase);
                        personalizedContent += "\n\nDo not include any disclaimer.";
                        JsonDocument doc = JsonDocument.Parse(await _openAiService.AskChatGPT(personalizedContent));
                        string outputText = doc.RootElement.GetProperty("output")[0].GetProperty("content")[0].GetProperty("text").GetString();
                        DeliverableFileModel filemodel = new DeliverableFileModel
                        {
                            DeliverableId = id,
                            EditedText = outputText,
                            Title = item.Name,
                            Body = outputText,
                            Url = transcriptUrl,
                            Hashtags = "",
                            ThumbnailUrl = thumbnailUrl
                        };
                        base.ViewBag.Confirmation = "";
                        fileModels.Add(filemodel);
                    }
                    else
                    {
                        base.ViewBag.Confirmation = "It is not Evergreen." + ActiveEverGreen;
                    }
                }
            }
            List<PostBookmarkInfo> googlestat = await _googleService.InsertContentToGoogleDocAsync(fileId, fileModels);
            Dictionary<string, string> bookmarkDict = googlestat.ToDictionary((PostBookmarkInfo b) => b.PostId, (PostBookmarkInfo b) => b.BookmarkUrl);
            foreach (Deliverable video in EvergreenList)
            {
                if (bookmarkDict.TryGetValue(video.id, out var bookmarkUrl))
                {
                    video.GDocs_Content = bookmarkUrl;
                }
            }
            SharePermissionsAsync(fileId);
            Console.WriteLine(googlestat);
        }
        base.ViewBag.Confirmation = EvergreenList;
        base.ViewBag.ErrorMessage = "";
        return View("Deliverable", "");
    }

    [HttpGet("/AccountDeliverables/{type}/{id}")]
    public async Task<IActionResult> AccountDeliverablesAsync([FromRoute(Name = "type")] string type, [FromRoute(Name = "id")] string AccountId)
    {
        string accounts = await _zohoService.GetAccountByIdAsync(AccountId);
        JObject json = JObject.Parse(accounts);
        JToken firstRecord = json["data"]!.First;
        using JsonDocument doc1 = JsonDocument.Parse(accounts);
        string accountName = Regex.Replace(doc1.RootElement.GetProperty("data")[0].GetProperty("Account_Name").GetString() ?? "", "[^a-zA-Z0-9\\s]", "").Replace(" ", "_");
        Console.WriteLine("Account Name: " + accountName);
        string nextYear = (DateTime.Now.Year + 1).ToString();
        List<Deliverable> deliverables = await _zohoService.GetZohoDeliverablesByAccountIdCategoryYearAsync(AccountId, type, nextYear);
        //new List<Deliverable>();
        List<Deliverable> DeliverablePostsList = (type.Equals("Holidays", StringComparison.OrdinalIgnoreCase) ? deliverables.Where((Deliverable d) => (d.Topic_Category ?? "Unknown").Equals("holidays", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(d.Block) && d.Block.StartsWith(nextYear)).ToList() : ((!type.Equals("Evergreen", StringComparison.OrdinalIgnoreCase)) ? null : deliverables.Where((Deliverable d) => d.Active_Evergreen && !string.IsNullOrEmpty(d.Block) && d.Block.StartsWith(nextYear)).ToList()));
        Console.WriteLine("Holidays : " + DeliverablePostsList.Count);
        JsonConvert.SerializeObject(DeliverablePostsList, Formatting.Indented);
        var data = new
        {
            CompanyName = firstRecord,
            DeliverableType = type,
            HolidaysList = DeliverablePostsList
        };
        base.ViewBag.Confirmation = DeliverablePostsList;
        base.ViewBag.ErrorMessage = "";
        return View("Deliverables", data);
    }

    [HttpGet("GetDeliverablesByYear/{account}/{year}")]
    public async Task<List<Deliverable>> GetDeliverablesByYear(string account, string year)
    {
        string year2 = year;
        string accounts = await _zohoService.GetAccountByIdAsync(account);
        JObject json = JObject.Parse(accounts);
        _ = json["data"]!.First;
        using JsonDocument doc1 = JsonDocument.Parse(accounts);
        string accountName = Regex.Replace(doc1.RootElement.GetProperty("data")[0].GetProperty("Account_Name").GetString() ?? "", "[^a-zA-Z0-9\\s]", "").Replace(" ", "_");
        Console.WriteLine("Account Name: " + accountName);
        List<Deliverable> holidaysList = (await _zohoService.GetZohoDeliverablesByAccountIdCategoryYearAsync(account, "Holidays", year2)).Where((Deliverable d) => d.Block != null && d.Block.StartsWith(year2)).ToList();
        Console.WriteLine("Holidays : " + holidaysList.Count);
        return holidaysList;
    }

    [HttpGet("/AccountsHolidayPosts1/{id}")]
    public async Task<IActionResult> AccountsHolidayPostsAsync([FromRoute(Name = "id")] string AccountId)
    {
        using JsonDocument doc1 = JsonDocument.Parse(await _zohoService.GetAccountByIdAsync(AccountId));
        string accountName = Regex.Replace(doc1.RootElement.GetProperty("data")[0].GetProperty("Account_Name").GetString() ?? "", "[^a-zA-Z0-9\\s]", "").Replace(" ", "_");
        Console.WriteLine("Account Name: " + accountName);
        List<Deliverable> deliverables = await _zohoService.GetZohoDeliverablesByAccountIdAsync(AccountId);
        _ = DateTime.Now.Year + 1;
        List<Deliverable> holidaysList = deliverables.Where((Deliverable d) => (d.Topic_Category ?? "Unknown").Equals("holidays", StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine("Holidays : " + holidaysList.Count);
        JsonConvert.SerializeObject(holidaysList, Formatting.Indented);
        base.ViewBag.Confirmation = holidaysList;
        base.ViewBag.ErrorMessage = "";
        return View("Deliverables", "");
    }

    public async Task<IActionResult> DeliverableInfoAsync([FromRoute(Name = "id")] string DeliverableId)
    {
        string deliverable = await _zohoService.GetZohoDeliverablesByIdAsync(DeliverableId);
        JObject json = JObject.Parse(deliverable);
        string Sub_Category_Social = (string?)json["Sub_Category_Social"];
        if (Sub_Category_Social == "Holiday Post")
        {
            HolidayFileModel model2 = new HolidayFileModel
            {
                DeliverableId = DeliverableId,
                HolidayList = "",
                CompanyName = (string?)json["Company.Account_Name"],
                CompanyWebsite = (string?)json["Company.Website"]
            };
            await _googleService.ListDriveFilesAsync();
            base.ViewBag.ErrorMessage = "";
            base.ViewBag.Confirmation = "";
            return View("HolidayPost", model2);
        }
        DeliverableFileModel model = new DeliverableFileModel
        {
            DeliverableId = DeliverableId,
            EditedText = ""
        };
        string transcriptUrl = (string?)json["Transcript_URL"];
        string thumbnailUrl = (string?)json["Thumbnail_URL"];
        string finalDeliverable = (string?)json["Final_Deliverable"];
        base.ViewBag.ErrorMessage = "";
        base.ViewBag.Id = "Transcript URL : " + transcriptUrl;
        if (string.IsNullOrEmpty(transcriptUrl))
        {
            base.ViewBag.ErrorMessage = "Transcript URL Not exist";
        }
        if (string.IsNullOrEmpty(thumbnailUrl))
        {
            base.ViewBag.ErrorMessage = "Thumbnail URL Not exist";
        }
        if (string.IsNullOrEmpty(finalDeliverable))
        {
            base.ViewBag.ErrorMessage = "Final Deliverable URL Not exist";
        }
        if (base.ViewBag.ErrorMessage == "")
        {
            bool ActiveEverGreen = (bool)json["Active_Evergreen"];
            if (!ActiveEverGreen)
            {
                base.ViewBag.Confirmation = "It is not Evergreen." + ActiveEverGreen;
            }
            else
            {
                GoogleFileRequest googleFileRequest = new GoogleFileRequest
                {
                    CompanyName = (string?)json["Company.Account_Name"],
                    MainContentType = (string?)json["Main_Status"],
                    Topic = (string?)json["Name"],
                    FinalDeliverableUrl = (string?)json["Final_Deliverable"],
                    ThumbnailUrl = (string?)json["Thumbnail_URL"],
                    GoogleFileId = "1UmD6IvcvSqrHDb3KIo0Rz_5txh6DvH5o"
                };
                string personalizedContent = (await _googleService.ReadDriveFilesAsync(googleFileRequest)).Replace("[Insert Company Name]", googleFileRequest.CompanyName, StringComparison.OrdinalIgnoreCase).Replace("[Company Website]", googleFileRequest.CompanyWebsite, StringComparison.OrdinalIgnoreCase).Replace("[HolidaysList]", googleFileRequest.HolidaysList, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Insert URL]", googleFileRequest.FinalDeliverableUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Insert Thumbnail URL]", googleFileRequest.ThumbnailUrl, StringComparison.OrdinalIgnoreCase)
                    .Replace("[Use for content reference]", googleFileRequest.TranscriptUrl, StringComparison.OrdinalIgnoreCase);
                JsonDocument doc = JsonDocument.Parse(await _openAiService.AskChatGPT(personalizedContent));
                string outputText = doc.RootElement.GetProperty("output")[0].GetProperty("content")[0].GetProperty("text").GetString();
                model = new DeliverableFileModel
                {
                    DeliverableId = DeliverableId,
                    EditedText = outputText
                };
                base.ViewBag.Confirmation = "";
            }
        }
        base.ViewBag.ErrorMessage = deliverable;
        return View("Deliverable", model);
    }

    public async Task<List<Permission>> SharePermissionsAsync(string FileId)
    {
        List<string> emailList = new List<string> { "priti@brandingarc.com", "sai@brandingarc.com" };
        return await _googleService.ShareDocumentWithUsersAsync(FileId, emailList);
    }
}
