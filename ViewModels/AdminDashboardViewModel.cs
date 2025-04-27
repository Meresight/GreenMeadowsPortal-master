using GreenMeadowsPortal.Models;

namespace GreenMeadowsPortal.ViewModels
{
    public class AdminDashboardViewModel
    {
        public ApplicationUser AdminUser { get; set; } = new ApplicationUser();  // ✅ Add this property
        public string FirstName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // ✅ Add Role
        public int TotalUsers { get; set; }
        public int ActiveReservations { get; set; }
    }
}
