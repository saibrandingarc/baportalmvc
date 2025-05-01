using System;
namespace BAPortalMVC.Models
{
    public class Settings
    {
        public int Id { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Scope { get; set; }
        public DateTime DateAdded { get; set; }
        public int ExpiresIn { get; set; }
        public string tokenFrom { get; set; }
    }
}

