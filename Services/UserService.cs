﻿// File: Services/UserService.cs

using GreenMeadowsPortal.Models;
using GreenMeadowsPortal.ViewModels;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using GreenMeadowsPortal.Data; // Add this for AppDbContext



namespace GreenMeadowsPortal.Services
{
    public class UserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserService> _logger;
        private readonly AppDbContext _context; // Add this field


        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserService> logger,
            AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
        }

        // In UserService.cs, update the GetAllUsersAsync method:

        public async Task<List<UserViewModel>> GetAllUsersAsync(int page, int pageSize)
        {
            try
            {
                _logger.LogInformation($"GetAllUsersAsync called with page={page}, pageSize={pageSize}");

                // First, log the raw count
                var totalCount = await _userManager.Users.CountAsync();
                _logger.LogInformation($"Total raw user count in database: {totalCount}");

                // Get the users with pagination
                var users = await _userManager.Users
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {users.Count} users from database");

                if (users.Count == 0)
                {
                    _logger.LogWarning("No users found in the database query result");
                    return new List<UserViewModel>();
                }

                var userViewModels = new List<UserViewModel>();

                foreach (var user in users)
                {
                    try
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        _logger.LogInformation($"Processing user: {user.Id}, Email: {user.Email}");

                        userViewModels.Add(new UserViewModel
                        {
                            Id = user.Id,
                            FirstName = user.FirstName ?? "",
                            LastName = user.LastName ?? "",
                            Email = user.Email ?? "",
                            PhoneNumber = user.PhoneNumber ?? "",
                            Address = user.Address ?? "",
                            Role = roles.FirstOrDefault() ?? "User",
                            Status = user.Status ?? "Active",
                            MemberSince = user.MemberSince.ToString("MMM dd, yyyy"),
                            LastLogin = user.LastLoginDate.HasValue ? user.LastLoginDate.Value.ToString("MMM dd, yyyy HH:mm") : "Never",
                            ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                            PropertyType = user.PropertyType ?? "N/A",
                            OwnershipStatus = user.OwnershipStatus ?? "N/A"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error mapping user {user.Id} to view model");
                    }
                }

                _logger.LogInformation($"Returning {userViewModels.Count} view models");
                return userViewModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllUsersAsync");
                // Return empty list instead of throwing to prevent page crash
                return new List<UserViewModel>();
            }
        }
        // Add this to UserService.cs
        public async Task LogActivityAsync(string userId, string activityType, string details, string ipAddress)
        {
            try
            {
                var activityLog = new ActivityLog
                {
                    UserId = userId,
                    ActivityType = activityType,
                    Details = details,
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow
                };

                _context.ActivityLogs.Add(activityLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log user activity");
            }
        }


        public async Task<List<PendingUserViewModel>> GetPendingUsersAsync()
        {
            // Use async methods and proper filtering
            var pendingUsers = await _userManager.Users
                .Where(u => u.Status == "Pending")
                .ToListAsync();  // Add ToListAsync() to execute query asynchronously

            var pendingUserViewModels = new List<PendingUserViewModel>();

            foreach (var user in pendingUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                // Log for debugging
                _logger.LogInformation($"Found pending user: {user.Email}, Status: {user.Status}");

                pendingUserViewModels.Add(new PendingUserViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName ?? "",
                    LastName = user.LastName ?? "",
                    Email = user.Email ?? "",
                    PhoneNumber = user.PhoneNumber ?? "",
                    Address = user.Address ?? "",
                    Role = roles.FirstOrDefault() ?? "Homeowner",
                    Status = "Pending",
                    MemberSince = user.MemberSince.ToString("MMM dd, yyyy"),
                    LastLogin = "Never",
                    ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                    PropertyType = user.PropertyType ?? "N/A",
                    OwnershipStatus = user.OwnershipStatus ?? "N/A",
                    Documents = new List<DocumentViewModel>()  // Fetch actual documents if needed
                });
            }

            return pendingUserViewModels;
        }

        public async Task<List<RoleViewModel>> GetAllRolesAsync()
        {
            var roles = _roleManager.Roles.ToList();
            var roleViewModels = new List<RoleViewModel>();

            foreach (var role in roles)
            {
                if (role.Name != null) // Ensure role.Name is not null
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);

                    // Get permissions for this role
                    // This would typically come from your database
                    var permissions = new List<PermissionViewModel>();

                    roleViewModels.Add(new RoleViewModel
                    {
                        Name = role.Name,
                        UserCount = usersInRole.Count,
                        Permissions = permissions
                    });
                }
            }

            return roleViewModels;
        }

        public async Task<List<ActivityLogViewModel>> GetActivityLogsAsync(int page, int pageSize)
        {
            try
            {
                // Check if ActivityLogs table exists
                if (_context.ActivityLogs == null)
                {
                    _logger.LogWarning("ActivityLogs DbSet is null");
                    return new List<ActivityLogViewModel>();
                }

                var logs = await _context.ActivityLogs
                    .Include(l => l.User)
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(log => new ActivityLogViewModel
                    {
                        Id = log.Id,
                        UserName = $"{log.User.FirstName} {log.User.LastName}",
                        UserRole = _userManager.GetRolesAsync(log.User).Result.FirstOrDefault() ?? "User",
                        UserProfileImage = log.User.ProfileImageUrl ?? "/images/default-avatar.png",
                        ActivityType = log.ActivityType,
                        Details = log.Details,
                        IpAddress = log.IpAddress,
                        Timestamp = log.Timestamp.ToString("MMM dd, yyyy HH:mm:ss")
                    })
                    .ToListAsync();

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activity logs");
                return new List<ActivityLogViewModel>();
            }
        }

        public async Task<int> GetTotalUserCountAsync()
        {
            // Use Task.FromResult to explicitly return a completed task with the result
            return await Task.FromResult(_userManager.Users.Count());
        }


        public async Task<int> GetTotalLogsCountAsync()
        {
            try
            {
                // Check if ActivityLogs table exists
                if (_context.ActivityLogs == null)
                {
                    return 0;
                }
                return await _context.ActivityLogs.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting activity logs");
                return 0;
            }
        }


        private string GetActivityDetails(string activityType)
        {
            switch (activityType)
            {
                case "login":
                    return "User logged into the system";
                case "logout":
                    return "User logged out of the system";
                case "profile-update":
                    return "User updated their profile information";
                case "password-change":
                    return "User changed their password";
                case "account-created":
                    return "New user account was created";
                case "status-change":
                    return "User status was changed";
                default:
                    return "Unknown activity";
            }
        }
    }
}