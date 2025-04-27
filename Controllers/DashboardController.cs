using GreenMeadowsPortal.Models;
using GreenMeadowsPortal.Services;
using GreenMeadowsPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GreenMeadowsPortal.Controllers
{
    [Authorize] // Ensures only authenticated users can access
    public class DashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AnnouncementService _announcementService;
        private readonly NotificationService _notificationService;

        public DashboardController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            AnnouncementService announcementService,
            NotificationService notificationService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _announcementService = announcementService;
            _notificationService = notificationService;
        }

        // ✅ Profile View for Dashboard
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);
            var profileModel = new ProfileViewModel
            {
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Address = user.Address ?? "No address available",
                ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                MemberSince = user.MemberSince.ToString("MMMM yyyy"),
                Status = user.Status,
                Role = roles.FirstOrDefault() ?? "User"
            };

            if (profileModel == null)
            {
                TempData["ErrorMessage"] = "Failed to load profile data.";
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Dashboard/Profile.cshtml", profileModel);
        }

        // ✅ Homeowner Dashboard
        [Authorize(Roles = "Homeowner")]
        public async Task<IActionResult> HomeownerDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            var model = new HomeownerDashboardViewModel
            {
                HomeownerUser = user,
                FirstName = user.FirstName ?? "Homeowner",
                Role = roles.FirstOrDefault() ?? "Homeowner",
                TotalPropertiesOwned = 2,
                PendingRequests = 3,
                ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png"
            };

            return View(model);
        }

        // ✅ Admin Dashboard
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            var model = new AdminDashboardViewModel
            {
                AdminUser = user,
                FirstName = user.FirstName ?? "Admin",
                Role = roles.FirstOrDefault() ?? "Admin",
                TotalUsers = 150,
                ActiveReservations = 20
            };

            return View(model);
        }

        // ✅ Staff Dashboard
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> StaffDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            var model = new StaffDashboardViewModel
            {
                StaffUser = user,
                FirstName = user.FirstName ?? "Staff",
                Role = roles.FirstOrDefault() ?? "Staff",
                NotificationCount = 5,
                TotalResidents = 100,
                PendingRequests = 10
            };

            return View(model);
        }

        // ✅ Announcement - Updated to use AnnouncementService
        public async Task<IActionResult> Announcement()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);
            var unreadCount = 0;

            try
            {
                unreadCount = await _notificationService.GetUnreadCountAsync(user.Id);
            }
            catch (Exception ex)
            {
                // Log the error but continue
                Console.WriteLine($"Error getting notification count: {ex.Message}");
            }

            var model = new AnnouncementViewModel
            {
                CurrentUser = user,
                FirstName = user.FirstName ?? "User",
                Role = roles.FirstOrDefault() ?? "User",
                ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                NotificationCount = unreadCount
            };

            // Try to get recent announcements
            try
            {
                var recentAnnouncements = await _announcementService.GetAnnouncementsForHomeownersAsync(filter: "all", page: 1, pageSize: 5);
                model.Announcements = recentAnnouncements.Select(a => new Announcement
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    Type = a.Priority == AnnouncementPriority.Urgent ? "Urgent" :
                           a.Priority == AnnouncementPriority.Important ? "Important" : "General",
                    Date = a.PublishDate ?? a.CreatedDate,
                    PostedBy = a.AuthorName,
                    IsActive = true,
                    ImageUrl = a.ImageUrl
                }).ToList();
            }
            catch (Exception ex)
            {
                // Log the error but continue with empty announcements
                Console.WriteLine($"Error getting announcements: {ex.Message}");
                model.Announcements = new List<Announcement>();
            }

            return View(model);
        }

        // ✅ Billing
        [Authorize]
        public async Task<IActionResult> Billing(int? year)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            var currentYear = DateTime.Now.Year;
            var selectedYear = year ?? currentYear;

            var model = new BillingViewModel
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Role = roles.FirstOrDefault() ?? "User",
                ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                NotificationCount = 0,
                SelectedYear = selectedYear
            };

            if (roles.Contains("Admin"))
            {
                await PopulateAdminBillingData(model, selectedYear);
            }
            else if (roles.Contains("Homeowner"))
            {
                await PopulateHomeownerBillingData(model, user.Id, selectedYear);
            }
            else if (roles.Contains("Staff"))
            {
                await PopulateStaffBillingData(model, user.Id, selectedYear);
            }
            else
            {
                await PopulateDefaultBillingData(model, user.Id, selectedYear);
            }

            return View(model);
        }

        // Helper methods to populate role-specific billing data
        private Task PopulateAdminBillingData(BillingViewModel model, int selectedYear)
        {
            var currentYear = DateTime.Now.Year;
            for (int i = currentYear; i >= currentYear - 3; i--)
            {
                model.YearOptions.Add(new SelectListItem
                {
                    Value = i.ToString(),
                    Text = i.ToString(),
                    Selected = i == selectedYear
                });
            }
            return Task.CompletedTask;
        }

        private Task PopulateHomeownerBillingData(BillingViewModel model, string userId, int selectedYear)
        {
            var currentYear = DateTime.Now.Year;
            for (int i = currentYear; i >= currentYear - 3; i--)
            {
                model.YearOptions.Add(new SelectListItem
                {
                    Value = i.ToString(),
                    Text = i.ToString(),
                    Selected = i == selectedYear
                });
            }

            if (model.CurrentBilling?.DueDate != null && model.CurrentBilling.DueDate != default)
            {
                var daysUntil = (model.CurrentBilling.DueDate - DateTime.Now).Days;
                model.DaysUntilDue = daysUntil > 0 ? daysUntil : 0;
            }
            else
            {
                model.DaysUntilDue = 0;
            }

            return Task.CompletedTask;
        }

        private Task PopulateStaffBillingData(BillingViewModel model, string userId, int selectedYear)
        {
            var currentYear = DateTime.Now.Year;
            for (int i = currentYear; i >= currentYear - 3; i--)
            {
                model.YearOptions.Add(new SelectListItem
                {
                    Value = i.ToString(),
                    Text = i.ToString(),
                    Selected = i == selectedYear
                });
            }
            return Task.CompletedTask;
        }

        private Task PopulateDefaultBillingData(BillingViewModel model, string userId, int selectedYear)
        {
            var currentYear = DateTime.Now.Year;
            for (int i = currentYear; i >= currentYear - 3; i--)
            {
                model.YearOptions.Add(new SelectListItem
                {
                    Value = i.ToString(),
                    Text = i.ToString(),
                    Selected = i == selectedYear
                });
            }
            return Task.CompletedTask;
        }

        // Add this to your DashboardController.cs
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UserManagement()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            var model = new UserManagementViewModel
            {
                FirstName = user.FirstName,
                Role = roles.FirstOrDefault() ?? "Admin",
                Users = new List<UserViewModel>(),
                PendingUsers = new List<PendingUserViewModel>(),
                Roles = new List<RoleViewModel>(),
                ActivityLogs = new List<ActivityLogViewModel>(),
                NotificationCount = 0,
                CurrentPage = 1,
                TotalPages = 1,
                PageStartRecord = 0,
                PageEndRecord = 0,
                TotalRecords = 0,
                PendingRequestsCount = 0,
                LogsCurrentPage = 1,
                LogsTotalPages = 1,
                LogsPageStartRecord = 0,
                LogsPageEndRecord = 0,
                TotalLogs = 0
            };

            // Populate your model with actual user data
            // This would be where you'd query your database

            return View(model);
        }

        // ✅ Logout Method
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}