using GreenMeadowsPortal.Models;

namespace GreenMeadowsPortal.ViewModels
{
    public class StaffDashboardViewModel
    {
        public ApplicationUser StaffUser { get; set; } = new ApplicationUser();
        public string FirstName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;

        public int NotificationCount { get; set; }
        public int TotalResidents { get; set; }
        public int PendingRequests { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string StaffRole { get; set; } = string.Empty;
        public decimal DuePaymentsTotal { get; set; }
        public int UpcomingEvents { get; set; }
        public List<ServiceRequest> RecentServiceRequests { get; set; } = new List<ServiceRequest>();
        public List<DashboardUser> RecentUsers { get; set; } = new List<DashboardUser>(); // Changed from User to DashboardUser
        public List<Event> UpcomingEventsList { get; set; } = new List<Event>();
        public decimal TotalBilledAmount { get; set; }
        public decimal TotalCollectedAmount { get; set; }
        public double CollectionRate { get; set; }
        public int OverdueAccountsCount { get; set; }
        public int DailyActiveUsers { get; set; }
        public int NewUsersToday { get; set; }
        public int TotalLoginToday { get; set; }
        public int RequestsCreatedToday { get; set; }
        public int RequestsCompletedToday { get; set; }
        public double AverageResolutionTime { get; set; }
        public int FacilityBookingsToday { get; set; }
        public string MostBookedFacility { get; set; } = string.Empty;
        public int UpcomingBookings { get; set; }
    }

    // Keep the existing classes unchanged
    public class ServiceRequest
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public DateTime DateSubmitted { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string PriorityClass { get; set; } = string.Empty;
    }

    public class Event
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}