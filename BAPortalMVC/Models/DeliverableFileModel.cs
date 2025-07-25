using System;
namespace BAPortalMVC.Models
{
    // BAPortalMVC.Models.DeliverableFileModel
    public class DeliverableFileModel
    {
        public string DeliverableId { get; set; }

        public string Title { get; set; }

        public string Body { get; set; }

        public string? Url { get; set; }

        public string? Hashtags { get; set; }

        public string? ThumbnailUrl { get; set; }

        public string EditedText { get; set; }
    }

}

