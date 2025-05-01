using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BAPortalMVC.Models;
using System.ComponentModel.Design;
using BAPortalMVC.Services;
using Newtonsoft.Json.Linq;
using System.Text;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;

namespace BAPortalMVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ZohoServices _zohoService;
    private readonly GoogleServices _googleService;
    private readonly OpenAiServices _openAiService;

    public HomeController(ZohoServices zohoService, GoogleServices googleService, OpenAiServices openAiService, ILogger<HomeController> logger)
    {
        _zohoService = zohoService;
        _googleService = googleService;
        _openAiService = openAiService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> DeliverableAsync([FromRoute(Name = "id")] string DeliverableId)
    {
        var model = new DeliverableFileModel
        {
            DeliverableId = DeliverableId, // User can fill this in
            EditedText = ""
        };
        var deliverable = await _zohoService.GetZohoDeliverablesByIdAsync(DeliverableId);
        var json = JObject.Parse(deliverable);
        string transcriptUrl = (string)json["Transcript_URL"];
        ViewBag.Id = "Transcript URL : "+transcriptUrl;
        if (!(transcriptUrl == "" || transcriptUrl == null))
        {
            ViewBag.Confirmation = "Transcript URL Not exist";
        }
        else
        {
            // Extract the "Main_Status" value
            string mainStatus = (string)json["Main_Status"];
            if (mainStatus == "Social")
            {
                string subCategory = (string)json["Sub_Category_Social"];
                if (subCategory.Contains("Evergreen"))
                {
                    var googleFileRequest = new GoogleFileRequest
                    {
                        CompanyName = (string)json["Company.Account_Name"],
                        MainContentType = (string)json["Main_Status"],
                        Topic = (string)json["Name"],
                        FinalDeliverableUrl = (string)json["Final_Deliverable"],
                        ThumbnailUrl = (string)json["Thumbnail_URL"],
                        GoogleFileId = "1UmD6IvcvSqrHDb3KIo0Rz_5txh6DvH5o"
                    };
                    var fileContent = await _googleService.ReadDriveFilesAsync(googleFileRequest);
                    var res = await _openAiService.AskChatGPT(fileContent);

                    //var response = new
                    //{
                    //    success = true,
                    //    companyName = (string)json["Company.Account_Name"],
                    //    mainContentType = (string)json["Main_Status"],
                    //    topic = (string)json["Name"],
                    //    finalDeliverableUrl = (string)json["Final_Deliverable"],
                    //    thumbnailUrl = (string)json["Thumbnail_URL"],
                    //    transcript = (string)json["Transcript_URL"],
                    //    filecontent = fileContent,
                    //    response = res
                    //};

                    var doc = JsonDocument.Parse(res);
                    var root = doc.RootElement;

                    // Navigate to output[0].content[0].text
                    var outputText = root
                        .GetProperty("output")[0]
                        .GetProperty("content")[0]
                        .GetProperty("text")
                        .GetString();

                    model = new DeliverableFileModel
                    {
                        DeliverableId = DeliverableId, // User can fill this in
                        EditedText = outputText
                    };
                    return View("Deliverable", model);
                }
                else
                {
                    ViewBag.Confirmation = subCategory;
                }
            }
            else
            {
                ViewBag.Confirmation = "Its not Evergreen Post";
            }
        }

        return View("Deliverable", model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public async Task<IActionResult> DeliverableFileUploadAsync(DeliverableFileModel model)
    {
        string filePath = Path.Combine(Path.GetTempPath(), model.DeliverableId + "-openai_response.txt");
        await System.IO.File.WriteAllTextAsync(filePath, model.EditedText, Encoding.UTF8);

        var uploadStatus = await _zohoService.UploadFileToZohoAsync(model.DeliverableId, filePath);
        if((int)uploadStatus.StatusCode == 200)
        {
            ViewBag.Confirmation = "Document Uploaded successfully!";
        } else
        {
            ViewBag.Confirmation = "Document Upload Failed!";
        }
        return View("Deliverable", model);
    }
}