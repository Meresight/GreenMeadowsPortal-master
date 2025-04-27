using GreenMeadowsPortal.Models;

namespace GreenMeadowsPortal.ViewModels
{
    public class HomeownerDashboardViewModel
    {
        public ApplicationUser HomeownerUser { get; set; } = new ApplicationUser();
        public string FirstName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int TotalPropertiesOwned { get; set; }
        public int PendingRequests { get; set; }

        public string ProfileImageUrl { get; set; } = string.Empty;
    }
}
