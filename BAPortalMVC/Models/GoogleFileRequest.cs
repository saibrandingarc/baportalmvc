using System;
namespace BAPortalMVC.Models
{
	public class GoogleFileRequest
	{
        public string CompanyName { get; set; }
        public string MainContentType { get; set; }
        public string Topic { get; set; }
        public string FinalDeliverableUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public string TranscriptUrl { get; set; }
        public string GoogleFileId { get; set; }
    }
}