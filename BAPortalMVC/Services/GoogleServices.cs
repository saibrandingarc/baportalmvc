// BAPortalMVC.Services.GoogleServices
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BAPortalMVC.Models;
using BAPortalMVC.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Sheets.v4.Data;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;

using Document = Google.Apis.Docs.v1.Data.Document;
using Link = Google.Apis.Docs.v1.Data.Link;
using Request = Google.Apis.Docs.v1.Data.Request;

namespace BAPortalMVC.Services
{
    public class GoogleServices
    {
        public class PostContent
        {
            public string Title { get; set; }

            public string Body { get; set; }

            public string Url { get; set; }
        }

        private class TextSegment
        {
            public string CleanText { get; set; }

            public List<(int Start, int End)> BoldRanges { get; set; }
        }

        private readonly IConfiguration _configuration;

        private readonly HttpClient _httpClient;

        private readonly ApplicationDbContext _context;

        private readonly DriveService _driveService;

        private readonly DocsService _docsService;

        public GoogleServices(IConfiguration configuration, HttpClient httpClient, ApplicationDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "badrive-baef0ef44a19.json");
            if (!System.IO.File.Exists(fullPath))
            {
                throw new FileNotFoundException("Service account file not found at " + fullPath);
            }
            GoogleCredential credential;
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(DriveService.Scope.Drive, DocsService.Scope.Documents);
            }
            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive"
            });
            _docsService = new DocsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive"
            });
        }

        public async Task<IList<Google.Apis.Drive.v3.Data.File>> ListDriveFilesAsync()
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "badrive-baef0ef44a19.json");
            if (!System.IO.File.Exists(fullPath))
            {
                throw new FileNotFoundException("Service account file not found at " + fullPath);
            }
            GoogleCredential credential;
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(DriveService.Scope.DriveReadonly);
            }
            DriveService service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive"
            });
            FilesResource.ListRequest listRequest2 = service.Files.List();
            listRequest2.Q = "mimeType = 'application/vnd.google-apps.folder'";
            listRequest2.Fields = "files(id, name)";
            foreach (Google.Apis.Drive.v3.Data.File file2 in (await listRequest2.ExecuteAsync()).Files)
            {
                Console.WriteLine("Folder Name: " + file2.Name + ", ID: " + file2.Id);
            }
            string folderId = "1koengU0pcVdPAAqHT1vOXcejuwVjsjMU";
            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.Q = "'" + folderId + "' in parents and trashed = false";
            listRequest.Fields = "files(id, name, mimeType)";
            FileList files = await listRequest.ExecuteAsync();
            foreach (Google.Apis.Drive.v3.Data.File file in files.Files)
            {
                Console.WriteLine($"File Name: {file.Name}, ID: {file.Id}, Type: {file.MimeType}");
            }
            return files.Files;
        }

        public async Task<FileList> ListDriveFoldersAsync()
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "badrive-baef0ef44a19.json");
            if (!System.IO.File.Exists(fullPath))
            {
                throw new FileNotFoundException("Service account file not found at " + fullPath);
            }
            GoogleCredential credential;
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(DriveService.Scope.DriveReadonly);
            }
            DriveService service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive"
            });
            FilesResource.ListRequest listRequest1 = service.Files.List();
            listRequest1.Q = "mimeType = 'application/vnd.google-apps.folder'";
            listRequest1.Fields = "files(id, name)";
            return await listRequest1.ExecuteAsync();
        }

        public async Task<string?> GetFolderIdByNameAsync(string folderName)
        {
            _ = _configuration["FolderNames:Default"];
            string DriveFolderName = ((folderName == "Evergreen") ? _configuration["FolderNames:Evergreen"] : ((!(folderName == "HolidayPosts")) ? _configuration["FolderNames:Default"] : _configuration["FolderNames:HolidayPosts"]));
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "badrive-baef0ef44a19.json");
            if (!System.IO.File.Exists(fullPath))
            {
                throw new FileNotFoundException("Service account file not found at " + fullPath);
            }
            GoogleCredential credential;
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(DriveService.Scope.DriveReadonly);
            }
            DriveService service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive"
            });
            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.Q = "mimeType='application/vnd.google-apps.folder' and name='" + DriveFolderName + "'";
            listRequest.Fields = "files(id, name)";
            return (await listRequest.ExecuteAsync()).Files.FirstOrDefault()?.Id;
        }

        public async Task<string> EnsureFolderExistsAsync(string folderName)
        {
            FilesResource.ListRequest listRequest = _driveService.Files.List();
            listRequest.Q = "mimeType='application/vnd.google-apps.folder' and name='" + folderName + "' and trashed=false";
            listRequest.Fields = "files(id, name)";
            Google.Apis.Drive.v3.Data.File folder = (await listRequest.ExecuteAsync()).Files.FirstOrDefault();
            if (folder != null)
            {
                return folder.Id;
            }
            Google.Apis.Drive.v3.Data.File folderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = "MyDocument",
                MimeType = "application/vnd.google-apps.document"
            };
            FilesResource.CreateRequest createRequest = _driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            return (await createRequest.ExecuteAsync()).Id;
        }

        public async Task<string> CreateGoogleDocAsync(string docTitle, string folderId)
        {
            string escapedDocTitle = docTitle.Replace("'", "\\'");
            string query = $"name = '{escapedDocTitle}' and mimeType = 'application/vnd.google-apps.document' and '{folderId}' in parents and trashed = false";
            FilesResource.ListRequest listRequest = _driveService.Files.List();
            listRequest.Q = query;
            listRequest.Fields = "files(id, name)";
            FileList files = await listRequest.ExecuteAsync();
            string fileId;
            if (files.Files != null && files.Files.Count > 0)
            {
                fileId = files.Files[0].Id;
            }
            else
            {
                Document doc = new Document
                {
                    Title = docTitle
                };
                fileId = (await _docsService.Documents.Create(doc).ExecuteAsync()).DocumentId;
                Google.Apis.Drive.v3.Data.File file = await _driveService.Files.Get(fileId).ExecuteAsync();
                string previousParents = ((file.Parents != null) ? string.Join(",", file.Parents) : "");
                FilesResource.UpdateRequest updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
                updateRequest.AddParents = folderId;
                if (!string.IsNullOrEmpty(previousParents))
                {
                    updateRequest.RemoveParents = previousParents;
                }
                updateRequest.Fields = "id, parents";
                await updateRequest.ExecuteAsync();
            }
            return fileId;
        }

        public async Task<string> ReadDriveFilesAsync(GoogleFileRequest googleFileRequest)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "badrive-baef0ef44a19.json");
            if (!System.IO.File.Exists(fullPath))
            {
                throw new FileNotFoundException("Service account file not found at " + fullPath);
            }
            GoogleCredential credential;
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(DriveService.Scope.DriveReadonly);
            }
            DriveService service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BADrive"
            });
            FilesResource.GetRequest request = service.Files.Get(googleFileRequest.GoogleFileId);
            MemoryStream memoryStream = new MemoryStream();
            await request.DownloadAsync(memoryStream);
            memoryStream.Position = 0L;
            using StreamReader reader = new StreamReader(memoryStream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        public async Task<List<PostBookmarkInfo>> InsertContentToGoogleDocAsync(string fileId, List<DeliverableFileModel> posts)
        {
            List<Request> requests = new List<Request>();
            List<PostBookmarkInfo> bookmarkInfos = new List<PostBookmarkInfo>();
            Document doc = _docsService.Documents.Get(fileId).Execute();
            int currentIndex = doc.Body.Content.Last().EndIndex.GetValueOrDefault(2) - 1;
            int PostCount = 1;
            foreach (DeliverableFileModel post in posts)
            {
                TextSegment parsedBody = ParseMarkdownBold(post.Body);
                if (parsedBody == null)
                {
                    continue;
                }
                string bookmarkName = "Bookmark_" + Guid.NewGuid().ToString("N");
                string titleText = post.Title + "\n\n";
                Console.WriteLine("Title: " + titleText);
                requests.Add(new Request
                {
                    InsertText = new InsertTextRequest
                    {
                        Text = titleText,
                        Location = new Location
                        {
                            Index = currentIndex
                        }
                    }
                });
                requests.Add(new Request
                {
                    CreateNamedRange = new CreateNamedRangeRequest
                    {
                        Name = bookmarkName,
                        Range = new Google.Apis.Docs.v1.Data.Range
                        {
                            StartIndex = currentIndex,
                            EndIndex = currentIndex + titleText.Length
                        }
                    }
                });
                requests.Add(new Request
                {
                    UpdateTextStyle = new UpdateTextStyleRequest
                    {
                        TextStyle = new TextStyle
                        {
                            Bold = true,
                            FontSize = new Dimension
                            {
                                Magnitude = 14.0,
                                Unit = "PT"
                            },
                            WeightedFontFamily = new WeightedFontFamily
                            {
                                FontFamily = "Cambria"
                            }
                        },
                        Range = new Google.Apis.Docs.v1.Data.Range
                        {
                            StartIndex = currentIndex,
                            EndIndex = currentIndex + titleText.Length
                        },
                        Fields = "bold,fontSize,weightedFontFamily"
                    }
                });
                currentIndex += titleText.Length;
                string bodyText = parsedBody.CleanText + "\n";
                requests.Add(new Request
                {
                    InsertText = new InsertTextRequest
                    {
                        Text = bodyText,
                        Location = new Location
                        {
                            Index = currentIndex
                        }
                    }
                });
                requests.Add(new Request
                {
                    UpdateTextStyle = new UpdateTextStyleRequest
                    {
                        TextStyle = new TextStyle
                        {
                            FontSize = new Dimension
                            {
                                Magnitude = 12.0,
                                Unit = "PT"
                            },
                            WeightedFontFamily = new WeightedFontFamily
                            {
                                FontFamily = "Cambria"
                            }
                        },
                        Range = new Google.Apis.Docs.v1.Data.Range
                        {
                            StartIndex = currentIndex,
                            EndIndex = currentIndex + bodyText.Length
                        },
                        Fields = "weightedFontFamily,fontSize"
                    }
                });
                requests.Add(new Request
                {
                    UpdateParagraphStyle = new UpdateParagraphStyleRequest
                    {
                        ParagraphStyle = new ParagraphStyle
                        {
                            LineSpacing = 150f
                        },
                        Range = new Google.Apis.Docs.v1.Data.Range
                        {
                            StartIndex = currentIndex,
                            EndIndex = currentIndex + bodyText.Length
                        },
                        Fields = "lineSpacing"
                    }
                });
                foreach (var (start, end) in parsedBody.BoldRanges)
                {
                    requests.Add(new Request
                    {
                        UpdateTextStyle = new UpdateTextStyleRequest
                        {
                            TextStyle = new TextStyle
                            {
                                Bold = true,
                                FontSize = new Dimension
                                {
                                    Magnitude = 12.0,
                                    Unit = "PT"
                                },
                                WeightedFontFamily = new WeightedFontFamily
                                {
                                    FontFamily = "Cambria"
                                }
                            },
                            Range = new Google.Apis.Docs.v1.Data.Range
                            {
                                StartIndex = currentIndex + 1 + start,
                                EndIndex = currentIndex + 1 + end
                            },
                            Fields = "bold,fontSize,weightedFontFamily"
                        }
                    });
                }
                currentIndex += bodyText.Length;
                string graphicsUrl = "";
                string graphicsUrlHead = "Graphics Link:\n";
                string graphicsUrlText = graphicsUrlHead + " " + graphicsUrl + "\n";
                requests.Add(new Request
                {
                    InsertText = new InsertTextRequest
                    {
                        Text = graphicsUrlText,
                        Location = new Location
                        {
                            Index = currentIndex
                        }
                    }
                });
                int graphicsUrlStartIndex = currentIndex + graphicsUrlText.IndexOf(graphicsUrlHead);
                int graphicsUrlEndIndex = graphicsUrlStartIndex + graphicsUrlHead.Length;
                requests.Add(new Request
                {
                    UpdateTextStyle = new UpdateTextStyleRequest
                    {
                        TextStyle = new TextStyle
                        {
                            Bold = true,
                            FontSize = new Dimension
                            {
                                Magnitude = 12.0,
                                Unit = "PT"
                            },
                            WeightedFontFamily = new WeightedFontFamily
                            {
                                FontFamily = "Cambria"
                            },
                            Link = new Link
                            {
                                Url = graphicsUrl
                            }
                        },
                        Range = new Google.Apis.Docs.v1.Data.Range
                        {
                            StartIndex = graphicsUrlStartIndex,
                            EndIndex = graphicsUrlEndIndex
                        },
                        Fields = "bold,fontSize,weightedFontFamily"
                    }
                });
                currentIndex += graphicsUrlText.Length;
                string deliverableUrl = "https://crm.zoho.com/crm/org668627929/tab/CustomModule10/" + post.DeliverableId;
                string deliverableUrlHead = "Deliverable Link\n";
                string deliverableUrlText = deliverableUrlHead + "\n";
                requests.Add(new Request
                {
                    InsertText = new InsertTextRequest
                    {
                        Text = deliverableUrlText,
                        Location = new Location
                        {
                            Index = currentIndex
                        }
                    }
                });
                int urlStartIndex = currentIndex + deliverableUrlText.IndexOf(deliverableUrlHead);
                int urlEndIndex = urlStartIndex + deliverableUrlHead.Length;
                requests.Add(new Request
                {
                    UpdateTextStyle = new UpdateTextStyleRequest
                    {
                        TextStyle = new TextStyle
                        {
                            Bold = true,
                            FontSize = new Dimension
                            {
                                Magnitude = 12.0,
                                Unit = "PT"
                            },
                            WeightedFontFamily = new WeightedFontFamily
                            {
                                FontFamily = "Cambria"
                            },
                            Link = new Link
                            {
                                Url = deliverableUrl
                            }
                        },
                        Range = new Google.Apis.Docs.v1.Data.Range
                        {
                            StartIndex = urlStartIndex,
                            EndIndex = urlEndIndex
                        },
                        Fields = "link,bold,fontSize,weightedFontFamily"
                    }
                });
                currentIndex += deliverableUrlText.Length;
                string horizontalRule = "_________________________________________________________________________\n\n";
                requests.Add(new Request
                {
                    InsertText = new InsertTextRequest
                    {
                        Text = horizontalRule,
                        Location = new Location
                        {
                            Index = currentIndex
                        }
                    }
                });
                currentIndex += horizontalRule.Length;
                PostCount++;
            }
            BatchUpdateDocumentRequest batchRequest = new BatchUpdateDocumentRequest
            {
                Requests = requests
            };
            BatchUpdateDocumentResponse status = await _docsService.Documents.BatchUpdate(batchRequest, fileId).ExecuteAsync();
            List<string> namedRangeIds = (from reply in status.Replies
                                          where reply.CreateNamedRange != null
                                          select reply.CreateNamedRange.NamedRangeId into id
                                          where !string.IsNullOrEmpty(id)
                                          select id).ToList();
            JsonSerializer.Serialize(status, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            for (int i = 0; i < namedRangeIds.Count; i++)
            {
                string bookmarkUrl = "https://docs.google.com/document/d/" + fileId + "/edit#bookmark=" + namedRangeIds[i];
                bookmarkInfos.Add(new PostBookmarkInfo
                {
                    PostId = posts[i].DeliverableId,
                    BookmarkUrl = bookmarkUrl
                });
            }
            return bookmarkInfos;
        }

        private static TextSegment ParseMarkdownBold(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            List<(int, int)> boldRanges = new List<(int, int)>();
            StringBuilder output = new StringBuilder();
            int index = 0;
            Regex regex = new Regex("\\*\\*(.*?)\\*\\*");
            foreach (Match match in regex.Matches(text))
            {
                output.Append(text.Substring(index, match.Index - index));
                int start = output.Length;
                output.Append(match.Groups[1].Value);
                int end = output.Length;
                boldRanges.Add((start, end));
                index = match.Index + match.Length;
            }
            output.Append(text.Substring(index));
            return new TextSegment
            {
                CleanText = output.ToString(),
                BoldRanges = boldRanges
            };
        }

        public async Task<List<Permission>> ShareDocumentWithUsersAsync(string documentId, List<string> emailAddresses)
        {
            List<Permission> permissionResponses = new List<Permission>();
            foreach (string email in emailAddresses)
            {
                Permission permission = new Permission
                {
                    Type = "user",
                    Role = "writer",
                    EmailAddress = email
                };
                PermissionsResource.CreateRequest request = _driveService.Permissions.Create(permission, documentId);
                request.Fields = "id";
                try
                {
                    permissionResponses.Add(await request.ExecuteAsync());
                    Console.WriteLine("Shared document '" + documentId + "' with: " + email);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to share document with " + email + ": " + ex.Message);
                }
            }
            return permissionResponses;
        }
    }
}