using GreenMeadowsPortal.Models;
using GreenMeadowsPortal.Services;
using GreenMeadowsPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Added missing using
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GreenMeadowsPortal.Controllers
{
    [Authorize(Roles = "Admin")]

    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserService _userService;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            UserService userService,
            IWebHostEnvironment hostEnvironment,
            ILogger<UserManagementController> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: /UserManagement/Index
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string tab = "all-users", int logPage = 1)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                // Add the debugging code here
                var allUsers = await _userManager.Users.ToListAsync();
                foreach (var u in allUsers)
                {
                    _logger.LogInformation($"User {u.Email}: Status = '{u.Status}', Role = '{(await _userManager.GetRolesAsync(u)).FirstOrDefault() ?? "None"}'");
                }

                // Direct check of database users count
                var totalUsersInDb = await _userManager.Users.CountAsync();
                _logger.LogInformation($"Total users in database: {totalUsersInDb}");

                var roles = await _userManager.GetRolesAsync(user);

                // Prepare the view model
                var viewModel = new UserManagementViewModel
                {
                    FirstName = user.FirstName ?? "Admin",
                    Role = roles.FirstOrDefault() ?? "Admin",
                    NotificationCount = 0,
                    CurrentUserProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                    // Initialize required collections
                    Users = new List<UserViewModel>(),
                    PendingUsers = new List<PendingUserViewModel>(),
                    Roles = new List<RoleViewModel>(),
                    ActivityLogs = new List<ActivityLogViewModel>()
                };

                // Load Users
                viewModel.Users = await _userService.GetAllUsersAsync(page, pageSize);

                // Calculate pagination for users
                viewModel.TotalRecords = await _userService.GetTotalUserCountAsync();
                viewModel.TotalPages = (int)Math.Ceiling(viewModel.TotalRecords / (double)pageSize);
                viewModel.PageStartRecord = viewModel.TotalRecords == 0 ? 0 : ((page - 1) * pageSize) + 1;
                viewModel.PageEndRecord = Math.Min(page * pageSize, viewModel.TotalRecords);

                // Load Pending Users
                viewModel.PendingUsers = await _userService.GetPendingUsersAsync();
                viewModel.PendingRequestsCount = viewModel.PendingUsers.Count;
                _logger.LogInformation($"Pending users count: {viewModel.PendingRequestsCount}");

                // Load Roles
                viewModel.Roles = await _userService.GetAllRolesAsync();

                // Load Activity Logs
                viewModel.ActivityLogs = await _userService.GetActivityLogsAsync(logPage, pageSize);

                // Calculate pagination for logs
                viewModel.TotalLogs = await _userService.GetTotalLogsCountAsync();
                viewModel.LogsTotalPages = (int)Math.Ceiling(viewModel.TotalLogs / (double)pageSize);
                viewModel.LogsPageStartRecord = viewModel.TotalLogs == 0 ? 0 : ((logPage - 1) * pageSize) + 1;
                viewModel.LogsPageEndRecord = Math.Min(logPage * pageSize, viewModel.TotalLogs);

                return View("~/Views/Dashboard/UserManagement.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading User Management page");
                throw;
            }
        }

        // GET: /UserManagement/AddUser
        [HttpGet]
        public async Task<IActionResult> AddUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);

            var viewModel = new UserFormViewModel
            {
                CurrentUserName = $"{user.FirstName ?? ""} {user.LastName ?? ""}",
                CurrentRole = roles.FirstOrDefault() ?? "Admin",
                CurrentUserProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                NotificationCount = 0, // Populate from notification service
                AvailableRoles = new List<string> { "Admin", "Staff", "Homeowner" }
            };

            return View("~/Views/Dashboard/AddUser.cshtml", viewModel);
        }

        // POST: /UserManagement/ChangeUserStatus
        [HttpPost]
        public async Task<IActionResult> ChangeUserStatus(string userId, string status)
        {
            // Get client IP address
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Update user status
            user.Status = status;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // Log the activity
                await _userService.LogActivityAsync(
                    user.Id,
                    "status-change",
                    $"User status changed to {status}",
                    ipAddress
                );

                return Json(new { success = true, message = $"User status changed to {status}." });
            }

            return Json(new { success = false, message = "Failed to update user status." });
        }

        // POST: /UserManagement/SaveUser      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUser(UserFormViewModel model)
        {
            try
            {
                // Always clear Id validation since it's empty for new users
                ModelState.Remove("Id");

                // Remove validations for optional fields
                ModelState.Remove("ForcePasswordChange");
                ModelState.Remove("SendCredentials");
                ModelState.Remove("ProfileImage");
                ModelState.Remove("PropertyDocuments");

                _logger.LogInformation($"Received form with Role: {model.Role}");

                // Check role first, then remove appropriate validation fields based on role
                if (!string.IsNullOrEmpty(model.Role))
                {
                    if (model.Role == "Staff")
                    {
                        _logger.LogInformation("Removing Homeowner-specific field validations");
                        ModelState.Remove("EmergencyContactName");
                        ModelState.Remove("EmergencyContactPhone");
                        ModelState.Remove("VehicleInfo");
                        ModelState.Remove("ResidentCount");
                        ModelState.Remove("MoveInDate");
                    }
                    else if (model.Role == "Homeowner")
                    {
                        _logger.LogInformation("Removing Staff-specific field validations");
                        ModelState.Remove("Department");
                        ModelState.Remove("Position");
                        ModelState.Remove("EmployeeId");
                    }
                    else
                    {
                        // For other roles like Admin, remove both sets of fields
                        _logger.LogInformation("Removing all role-specific field validations");
                        ModelState.Remove("EmergencyContactName");
                        ModelState.Remove("EmergencyContactPhone");
                        ModelState.Remove("VehicleInfo");
                        ModelState.Remove("ResidentCount");
                        ModelState.Remove("MoveInDate");
                        ModelState.Remove("Department");
                        ModelState.Remove("Position");
                        ModelState.Remove("EmployeeId");
                    }
                }

                _logger.LogInformation("SaveUser method called");
                _logger.LogInformation($"Model state valid: {ModelState.IsValid}");

                // Log validation errors if any
                if (!ModelState.IsValid)
                {
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            _logger.LogError($"Validation error for {state.Key}: {error.ErrorMessage}");
                        }
                    }

                    // Populate the ViewModel with current user information for display
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        var roles = await _userManager.GetRolesAsync(currentUser);
                        model.CurrentUserName = $"{currentUser.FirstName ?? ""} {currentUser.LastName ?? ""}";
                        model.CurrentRole = roles.FirstOrDefault() ?? "Admin";
                        model.CurrentUserProfileImageUrl = currentUser.ProfileImageUrl ?? "/images/default-avatar.png";
                        model.NotificationCount = 0; // Set notification count
                    }
                    else
                    {
                        model.CurrentUserName = "Admin";
                        model.CurrentRole = "Admin";
                        model.CurrentUserProfileImageUrl = "/images/default-avatar.png";
                    }

                    model.AvailableRoles = new List<string> { "Admin", "Staff", "Homeowner" };

                    // Add an error message to TempData so it's visible to the user
                    TempData["ErrorMessage"] = "Please fix the validation errors.";

                    return View("~/Views/Dashboard/AddUser.cshtml", model);
                }

                // Process profile image if one was uploaded
                string? profileImageUrl = null;
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    profileImageUrl = await ProcessProfileImageUpload(model.ProfileImage);
                }

                if (string.IsNullOrEmpty(model.Id))
                {
                    // Creating a new user
                    _logger.LogInformation("Creating new user with email: " + model.Email);

                    var newUser = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        PhoneNumber = model.PhoneNumber,
                        Address = model.Address ?? string.Empty,
                        Unit = model.Unit ?? string.Empty,
                        PropertyType = model.PropertyType ?? string.Empty,
                        OwnershipStatus = model.OwnershipStatus ?? string.Empty,
                        Department = model.Department ?? string.Empty,
                        Position = model.Position ?? string.Empty,
                        EmployeeId = model.EmployeeId ?? string.Empty,
                        MoveInDate = model.MoveInDate,
                        EmergencyContactName = model.Role != "Homeowner" ? "N/A" : (model.EmergencyContactName ?? "N/A"),
                        EmergencyContactPhone = model.Role != "Homeowner" ? "N/A" : (model.EmergencyContactPhone ?? "N/A"),
                        VehicleInfo = model.Role != "Homeowner" ? "N/A" : (model.VehicleInfo ?? "N/A"), // Add this line
                        ResidentCount = model.ResidentCount ?? 0, // Default to 0 if null
                        Status = model.Status,
                        MemberSince = DateTime.Now,
                        ProfileImageUrl = profileImageUrl ?? "/images/default-avatar.png",
                        ReceiveEmailNotifications = model.ReceiveNotifications,
                        ReceiveSmsNotifications = model.ReceiveSMS,
                        Notes = model.Notes ?? string.Empty, // Add a default empty string
                        ForcePasswordChange = model.ForcePasswordChange,
                        DateOfBirth = model.DateOfBirth
                    };

                    _logger.LogInformation("Attempting to create user in database");
                    var result = await _userManager.CreateAsync(newUser, model.Password);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created successfully, assigning role: " + model.Role);

                        // Assign role
                        await _userManager.AddToRoleAsync(newUser, model.Role);

                        // Process property documents if any
                        if (model.PropertyDocuments != null && model.PropertyDocuments.Count > 0)
                        {
                            await ProcessPropertyDocumentsUpload(newUser.Id, model.PropertyDocuments);
                        }

                        // Send credentials email if requested
                        if (model.SendCredentials)
                        {
                            await SendAccountCredentialsEmail(newUser, model.Password);
                        }

                        TempData["SuccessMessage"] = "User created successfully.";
                        return RedirectToAction("AddUser");


                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            _logger.LogError($"User creation error: {error.Description}");
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        TempData["ErrorMessage"] = "Failed to create user. See error details below.";
                    }
                }
                else
                {
                    // Update existing user logic
                    _logger.LogInformation("Updating existing user with ID: " + model.Id);

                    var existingUser = await _userManager.FindByIdAsync(model.Id);
                    if (existingUser == null)
                    {
                        TempData["ErrorMessage"] = "User not found.";
                        return RedirectToAction("Index");
                    }

                    // Update user properties
                    existingUser.FirstName = model.FirstName;
                    existingUser.LastName = model.LastName;
                    existingUser.PhoneNumber = model.PhoneNumber;
                    existingUser.Address = model.Address;
                    existingUser.Unit = model.Unit;
                    existingUser.PropertyType = model.PropertyType;
                    existingUser.OwnershipStatus = model.OwnershipStatus;
                    existingUser.Department = model.Department ?? string.Empty;
                    existingUser.Position = model.Position ?? string.Empty;
                    existingUser.EmployeeId = model.EmployeeId ?? string.Empty;
                    existingUser.MoveInDate = model.MoveInDate;
                    existingUser.EmergencyContactName = model.EmergencyContactName;
                    existingUser.EmergencyContactPhone = model.EmergencyContactPhone;
                    existingUser.VehicleInfo = model.VehicleInfo;
                    existingUser.ResidentCount = model.ResidentCount;
                    existingUser.Status = model.Status;
                    existingUser.ReceiveEmailNotifications = model.ReceiveNotifications;
                    existingUser.ReceiveSmsNotifications = model.ReceiveSMS;
                    existingUser.Notes = model.Notes;
                    existingUser.DateOfBirth = model.DateOfBirth;
                    existingUser.ForcePasswordChange = model.ForcePasswordChange;

                    // Update profile image if a new one was uploaded
                    if (!string.IsNullOrEmpty(profileImageUrl))
                    {
                        existingUser.ProfileImageUrl = profileImageUrl;
                    }

                    // Update user in the database
                    var updateResult = await _userManager.UpdateAsync(existingUser);
                    if (updateResult.Succeeded)
                    {
                        // Update user's role if it has changed
                        var currentRoles = await _userManager.GetRolesAsync(existingUser);
                        var currentRole = currentRoles.FirstOrDefault();

                        if (currentRole != model.Role)
                        {
                            if (currentRole != null)
                            {
                                await _userManager.RemoveFromRoleAsync(existingUser, currentRole);
                            }
                            await _userManager.AddToRoleAsync(existingUser, model.Role);
                        }

                        // Update password if a new one was provided
                        if (!string.IsNullOrEmpty(model.Password))
                        {
                            var token = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                            var passwordResult = await _userManager.ResetPasswordAsync(existingUser, token, model.Password);

                            if (!passwordResult.Succeeded)
                            {
                                foreach (var error in passwordResult.Errors)
                                {
                                    ModelState.AddModelError(string.Empty, error.Description);
                                }
                            }
                        }

                        // Process property documents if any
                        if (model.PropertyDocuments != null && model.PropertyDocuments.Count > 0)
                        {
                            await ProcessPropertyDocumentsUpload(existingUser.Id, model.PropertyDocuments);
                        }

                        TempData["SuccessMessage"] = "User updated successfully.";
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        foreach (var error in updateResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        TempData["ErrorMessage"] = "Failed to update user.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user");
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
            }

            // If we get here, something went wrong
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                model.CurrentUserName = $"{user.FirstName ?? ""} {user.LastName ?? ""}";
                model.CurrentRole = userRoles.FirstOrDefault() ?? "Admin";
                model.CurrentUserProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png";
            }
            else
            {
                model.CurrentUserName = "Admin";
                model.CurrentRole = "Admin";
                model.CurrentUserProfileImageUrl = "/images/default-avatar.png";
            }

            model.AvailableRoles = new List<string> { "Admin", "Staff", "Homeowner" };
            return View("~/Views/Dashboard/AddUser.cshtml", model);
        }


        // GET: /UserManagement/EditUser/5
        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var userToEdit = await _userManager.FindByIdAsync(id);
            if (userToEdit == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var userRoles = await _userManager.GetRolesAsync(userToEdit);

            var viewModel = new UserFormViewModel
            {
                Id = userToEdit.Id,
                FirstName = userToEdit.FirstName,
                LastName = userToEdit.LastName,
                Email = userToEdit.Email ?? string.Empty,
                PhoneNumber = userToEdit.PhoneNumber ?? string.Empty,
                Address = userToEdit.Address ?? string.Empty,
                Unit = userToEdit.Unit ?? string.Empty,
                PropertyType = userToEdit.PropertyType ?? string.Empty,
                OwnershipStatus = userToEdit.OwnershipStatus ?? string.Empty,
                Role = userRoles.FirstOrDefault() ?? "Homeowner",
                Status = userToEdit.Status ?? "Pending",
                Department = userToEdit.Department ?? string.Empty,
                Position = userToEdit.Position ?? string.Empty,
                EmployeeId = userToEdit.EmployeeId ?? string.Empty,
                MoveInDate = userToEdit.MoveInDate,
                EmergencyContactName = userToEdit.EmergencyContactName ?? string.Empty,
                EmergencyContactPhone = userToEdit.EmergencyContactPhone ?? string.Empty,
                VehicleInfo = userToEdit.VehicleInfo ?? string.Empty,
                ResidentCount = userToEdit.ResidentCount,
                DateOfBirth = userToEdit.DateOfBirth,
                Notes = userToEdit.Notes ?? string.Empty,
                ReceiveNotifications = userToEdit.ReceiveEmailNotifications.HasValue ? userToEdit.ReceiveEmailNotifications.Value : true,
                ReceiveSMS = userToEdit.ReceiveSmsNotifications.HasValue ? userToEdit.ReceiveSmsNotifications.Value : false,
                CurrentUserName = $"{user.FirstName ?? ""} {user.LastName ?? ""}",
                CurrentRole = roles.FirstOrDefault() ?? "Admin",
                AvailableRoles = new List<string> { "Admin", "Staff", "Homeowner" },
                NotificationCount = 0 // Populate from notification service
            };

            return View("~/Views/Dashboard/AddUser.cshtml", viewModel);
        }

        // POST: /UserManagement/DeleteUser/5
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Check if this is the last admin
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                    if (adminUsers.Count <= 1)
                    {
                        return Json(new { success = false, message = "Cannot delete the last admin user." });
                    }
                }

                // Delete user's profile image if it exists
                if (!string.IsNullOrEmpty(user.ProfileImageUrl) &&
                    !user.ProfileImageUrl.Contains("default-avatar") &&
                    System.IO.File.Exists(_hostEnvironment.WebRootPath + user.ProfileImageUrl))
                {
                    System.IO.File.Delete(_hostEnvironment.WebRootPath + user.ProfileImageUrl);
                }

                // Delete user's documents if any
                // This would require access to your document repository
                // await _documentService.DeleteUserDocuments(id);

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    // Log user deletion
                    _logger.LogInformation($"User {user.Email} deleted by admin.");
                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to delete user." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // POST: /UserManagement/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string userId, string newPassword, bool forcePasswordChange, bool sendEmail)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Reset password
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    // Update force password change flag
                    if (forcePasswordChange)
                    {
                        user.ForcePasswordChange = true;
                        await _userManager.UpdateAsync(user);
                    }

                    // Send email with new password if requested
                    if (sendEmail)
                    {
                        await SendPasswordResetEmail(user, newPassword);
                    }

                    // Log password reset
                    _logger.LogInformation($"Password reset for user {user.Email} by admin.");

                    TempData["SuccessMessage"] = "Password reset successfully.";
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    TempData["ErrorMessage"] = "Failed to reset password.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /UserManagement/BulkAction
        [HttpPost]
        public async Task<IActionResult> BulkAction(string action, List<string> userIds)
        {
            if (userIds == null || !userIds.Any())
            {
                return Json(new { success = false, message = "No users selected." });
            }

            try
            {
                int successCount = 0;
                List<string> failedUsers = new List<string>();

                foreach (var userId in userIds)
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        failedUsers.Add($"User ID {userId} not found");
                        continue;
                    }

                    bool success = false;
                    switch (action.ToLower())
                    {
                        case "activate":
                            user.Status = "Active";
                            var activateResult = await _userManager.UpdateAsync(user);
                            success = activateResult.Succeeded;
                            break;

                        case "suspend":
                            user.Status = "Suspended";
                            var suspendResult = await _userManager.UpdateAsync(user);
                            success = suspendResult.Succeeded;
                            break;

                        case "deactivate":
                            user.Status = "Inactive";
                            var deactivateResult = await _userManager.UpdateAsync(user);
                            success = deactivateResult.Succeeded;
                            break;

                        case "delete":
                            // Check if this is the last admin
                            if (await _userManager.IsInRoleAsync(user, "Admin"))
                            {
                                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                                if (adminUsers.Count <= 1)
                                {
                                    failedUsers.Add($"{user.Email} - Cannot delete the last admin user");
                                    continue;
                                }
                            }

                            var deleteResult = await _userManager.DeleteAsync(user);
                            success = deleteResult.Succeeded;
                            break;

                        default:
                            failedUsers.Add($"{user.Email} - Unknown action: {action}");
                            continue;
                    }

                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedUsers.Add($"{user.Email} - Operation failed");
                    }
                }

                if (failedUsers.Any())
                {
                    return Json(new
                    {
                        success = successCount > 0,
                        message = $"Completed {action} for {successCount} users. Failed for {failedUsers.Count} users.",
                        details = failedUsers
                    });
                }

                return Json(new
                {
                    success = true,
                    message = $"Successfully completed {action} for {successCount} users."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error performing bulk action: {action}");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // POST: /UserManagement/ExportUsers
        [HttpPost]
        public async Task<IActionResult> ExportUsers(string format = "csv")
        {
            try
            {
                // Get all users
                var users = await _userService.GetAllUsersAsync(1, int.MaxValue);

                if (format.ToLower() == "csv")
                {
                    // Generate CSV
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("ID,First Name,Last Name,Email,Phone Number,Address,Role,Status,Member Since,Last Login");

                    foreach (var user in users)
                    {
                        csv.AppendLine($"{user.Id}," +
                                       $"\"{user.FirstName.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.LastName.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.Email.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.PhoneNumber.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.Address.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.Role.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.Status.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.MemberSince.Replace("\"", "\"\"")}\"," +
                                       $"\"{user.LastLogin.Replace("\"", "\"\"")}\"");
                    }

                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                    return File(bytes, "text/csv", $"users_export_{DateTime.Now:yyyyMMdd}.csv");
                }
                else if (format.ToLower() == "json")
                {
                    // Return JSON directly
                    return Json(users);
                }
                else
                {
                    return BadRequest($"Unsupported export format: {format}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting users");
                return StatusCode(500, "Error exporting users: " + ex.Message);
            }
        }

        // POST: /UserManagement/ProcessPendingUser
        [HttpPost]
        public async Task<IActionResult> ProcessPendingUser(string userId, string action, string? notes = null)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var currentAdmin = await _userManager.GetUserAsync(User);
                if (currentAdmin == null)
                {
                    return Json(new { success = false, message = "Admin user not found." });
                }

                switch (action.ToLower())
                {
                    case "approve":
                        user.Status = "Active";
                        var approveResult = await _userManager.UpdateAsync(user);

                        if (approveResult.Succeeded)
                        {
                            // Send approval email
                            await SendUserApprovalEmail(user);

                            // Log approval
                            _logger.LogInformation($"User {user.Email} approved by admin {currentAdmin.Email}. Notes: {notes}");

                            return Json(new { success = true, message = "User approved successfully." });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Failed to approve user." });
                        }

                    case "reject":
                        user.Status = "Rejected";
                        var rejectResult = await _userManager.UpdateAsync(user);

                        if (rejectResult.Succeeded)
                        {
                            // Send rejection email
                            await SendUserRejectionEmail(user, notes ?? string.Empty);

                            // Log rejection
                            _logger.LogInformation($"User {user.Email} rejected by admin {currentAdmin.Email}. Notes: {notes}");

                            return Json(new { success = true, message = "User rejected successfully." });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Failed to reject user." });
                        }

                    case "request-more-info":
                        // Send email requesting more information
                        await SendRequestMoreInfoEmail(user, notes ?? string.Empty);

                        // Log request
                        _logger.LogInformation($"More information requested for user {user.Email} by admin {currentAdmin.Email}. Notes: {notes}");

                        return Json(new { success = true, message = "Request for more information sent." });

                    default:
                        return Json(new { success = false, message = $"Unknown action: {action}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing pending user action: {action}");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // POST: /UserManagement/SaveRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRole(string roleName, string? description = null, string? originalRoleName = null, List<string>? permissions = null)
        {
            try
            {
                if (string.IsNullOrEmpty(roleName))
                {
                    TempData["ErrorMessage"] = "Role name is required.";
                    return RedirectToAction("Index");
                }

                // Check if this is an update or new role
                if (!string.IsNullOrEmpty(originalRoleName))
                {
                    // Update existing role
                    var existingRole = await _roleManager.FindByNameAsync(originalRoleName);
                    if (existingRole == null)
                    {
                        TempData["ErrorMessage"] = "Role not found.";
                        return RedirectToAction("Index");
                    }

                    // Check if role name is changing
                    if (originalRoleName != roleName)
                    {
                        // Rename role (delete and recreate because Identity doesn't support renaming)
                        var newRole = new IdentityRole(roleName);
                        var createResult = await _roleManager.CreateAsync(newRole);

                        if (!createResult.Succeeded)
                        {
                            TempData["ErrorMessage"] = "Failed to create new role with updated name.";
                            return RedirectToAction("Index");
                        }

                        // Get users in the old role
                        var usersInRole = await _userManager.GetUsersInRoleAsync(originalRoleName);

                        // Add all users to new role
                        foreach (var roleUser in usersInRole)
                        {
                            await _userManager.AddToRoleAsync(roleUser, roleName);
                            await _userManager.RemoveFromRoleAsync(roleUser, originalRoleName);
                        }

                        // Delete the old role
                        await _roleManager.DeleteAsync(existingRole);

                        // Update permissions for the new role
                        // This would require your custom permission system
                        // await _permissionService.UpdateRolePermissions(roleName, permissions);
                    }
                    else
                    {
                        // Only permissions are changing
                        // await _permissionService.UpdateRolePermissions(roleName, permissions);
                    }

                    TempData["SuccessMessage"] = "Role updated successfully.";
                }
                else
                {
                    // Create new role
                    var newRole = new IdentityRole(roleName);
                    var result = await _roleManager.CreateAsync(newRole);

                    if (result.Succeeded)
                    {
                        // Set permissions for the new role
                        // await _permissionService.UpdateRolePermissions(roleName, permissions);

                        TempData["SuccessMessage"] = "Role created successfully.";
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        TempData["ErrorMessage"] = "Failed to create role.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving role");
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /UserManagement/DeleteRole
        [HttpPost]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            try
            {
                // Check if role is a system role
                if (roleName == "Admin" || roleName == "Staff" || roleName == "Homeowner")
                {
                    return Json(new { success = false, message = "Cannot delete system roles." });
                }

                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    return Json(new { success = false, message = "Role not found." });
                }

                // Check if there are users with this role
                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
                if (usersInRole.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot delete role. There are {usersInRole.Count} users with this role.",
                        userCount = usersInRole.Count
                    });
                }

                var result = await _roleManager.DeleteAsync(role);
                if (result.Succeeded)
                {
                    // Delete role permissions
                    // await _permissionService.DeleteRolePermissions(roleName);

                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to delete role." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // GET: /UserManagement/GetRolePermissions
        [HttpGet]
        public async Task<IActionResult> GetRolePermissions(string roleName)
        {
            try
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    return NotFound("Role not found.");
                }

                // Get users with this role
                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
                var usersList = usersInRole.Take(10).Select(u => new
                {
                    userName = $"{u.FirstName ?? ""} {u.LastName ?? ""}",
                    profileImage = u.ProfileImageUrl ?? "/images/default-avatar.png"
                }).ToList();

                // Get permissions for this role
                // In a real application, you would fetch this from your permission service
                // var permissions = await _permissionService.GetRolePermissions(roleName);

                // For now, we'll return dummy permissions data
                var categoryPermissions = new List<object>
                {
                    new
                    {
                        categoryName = "User Management",
                        permissions = new List<object>
                        {
                            new { permissionName = "View Users", granted = true },
                            new { permissionName = "Create Users", granted = roleName == "Admin" },
                            new { permissionName = "Edit Users", granted = roleName == "Admin" },
                            new { permissionName = "Delete Users", granted = roleName == "Admin" },
                            new { permissionName = "Manage Roles", granted = roleName == "Admin" }
                        }
                    },
                    new
                    {
                        categoryName = "Announcements",
                        permissions = new List<object>
                        {
                            new { permissionName = "View Announcements", granted = true },
                            new { permissionName = "Create Announcements", granted = roleName == "Admin" || roleName == "Staff" },
                            new { permissionName = "Edit Announcements", granted = roleName == "Admin" || roleName == "Staff" },
                            new { permissionName = "Delete Announcements", granted = roleName == "Admin" }
                        }
                    },
                    new
                    {
                        categoryName = "Billing",
                        permissions = new List<object>
                        {
                            new { permissionName = "View Billing", granted = true },
                            new { permissionName = "Create Invoices", granted = roleName == "Admin" },
                            new { permissionName = "Process Payments", granted = roleName == "Admin" },
                            new { permissionName = "Manage Billing Settings", granted = roleName == "Admin" }
                        }
                    },
                    new
                    {
                        categoryName = "Facilities",
                        permissions = new List<object>
                        {
                            new { permissionName = "View Facilities", granted = true },
                            new { permissionName = "Manage Facilities", granted = roleName == "Admin" || roleName == "Staff" },
                            new { permissionName = "Approve Reservations", granted = roleName == "Admin" || roleName == "Staff" }
                        }
                    }
                };

                return Json(new
                {
                    success = true,
                    roleName = roleName,
                    roleDescription = $"{roleName} role description", // This would come from your role description field
                    isSystemRole = roleName == "Admin" || roleName == "Staff" || roleName == "Homeowner",
                    permissionCategories = categoryPermissions,
                    users = usersList,
                    userCount = usersInRole.Count,
                    moreUsers = usersInRole.Count > 10,
                    moreUsersCount = usersInRole.Count > 10 ? usersInRole.Count - 10 : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role permissions");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // POST: /UserManagement/ChangeUserStatus
        [HttpPost]
        // Rename one of the duplicate methods to avoid conflict
        [HttpPost]
        public async Task<IActionResult> UpdateUserStatus(string userId, string status)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Check if trying to suspend or deactivate the last admin
                if ((status == "Suspended" || status == "Inactive") && await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                    if (adminUsers.Count <= 1)
                    {
                        return Json(new { success = false, message = "Cannot modify status of the last admin user." });
                    }
                }

                user.Status = status;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    string adminEmail = currentUser?.Email ?? "Unknown admin";

                    // Log status change
                    _logger.LogInformation($"User {user.Email} status changed to {status} by admin {adminEmail}");

                    return Json(new { success = true, message = $"User status changed to {status} successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to update user status." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user status");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }


        // Updated method to fix CS8625 and IDE0060 issues
        [HttpPost]
        public async Task<IActionResult> ExportActivityLogs(string format = "csv", string? dateFrom = null, string? dateTo = null, string? activityType = "all", string? userId = "all")
        {
            try
            {
                // In a real application, you would filter logs based on the parameters
                var logs = await _userService.GetActivityLogsAsync(1, int.MaxValue);

                if (format.ToLower() == "csv")
                {
                    // Generate CSV
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("ID,User Name,User Role,Activity Type,Details,IP Address,Timestamp");

                    foreach (var log in logs)
                    {
                        csv.AppendLine($"{log.Id}," +
                                       $"\"{log.UserName.Replace("\"", "\"\"")}\"," +
                                       $"\"{log.UserRole.Replace("\"", "\"\"")}\"," +
                                       $"\"{log.ActivityType.Replace("\"", "\"\"")}\"," +
                                       $"\"{log.Details.Replace("\"", "\"\"")}\"," +
                                       $"\"{log.IpAddress.Replace("\"", "\"\"")}\"," +
                                       $"\"{log.Timestamp.Replace("\"", "\"\"")}\"");
                    }

                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                    return File(bytes, "text/csv", $"activity_logs_{DateTime.Now:yyyyMMdd}.csv");
                }
                else if (format.ToLower() == "json")
                {
                    // Return JSON directly
                    return Json(logs);
                }
                else
                {
                    return BadRequest($"Unsupported export format: {format}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting activity logs");
                return StatusCode(500, "Error exporting activity logs: " + ex.Message);
            }
        }

        private async Task<string> ProcessProfileImageUpload(IFormFile profileImage)
        {
            if (profileImage == null || profileImage.Length == 0)
                return string.Empty; // Replace null with an empty string to avoid CS8603

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(profileImage.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new Exception("Only JPG, JPEG, and PNG files are allowed for profile images.");
            }

            string userFolderRelative = "images/users";
            string userFolderAbsolute = Path.Combine(_hostEnvironment.WebRootPath, userFolderRelative);

            if (!Directory.Exists(userFolderAbsolute))
            {
                Directory.CreateDirectory(userFolderAbsolute);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
            string filePath = Path.Combine(userFolderAbsolute, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await profileImage.CopyToAsync(fileStream);
            }

            return $"/{userFolderRelative}/{uniqueFileName}";
        }

        private async Task ProcessPropertyDocumentsUpload(string userId, List<IFormFile> documents)
        {
            if (documents == null || !documents.Any())
                return;

            string docFolderRelative = $"documents/users/{userId}";
            string docFolderAbsolute = Path.Combine(_hostEnvironment.WebRootPath, docFolderRelative);

            if (!Directory.Exists(docFolderAbsolute))
            {
                Directory.CreateDirectory(docFolderAbsolute);
            }

            foreach (var document in documents)
            {
                if (document.Length > 0)
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(document.FileName);
                    string filePath = Path.Combine(docFolderAbsolute, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await document.CopyToAsync(fileStream);
                    }

                    // In a real application, you would save document metadata to the database
                    // await _documentService.SaveDocument(userId, document.FileName, uniqueFileName, filePath);
                }
            }
        }

        private async Task SendAccountCredentialsEmail(ApplicationUser user, string password)
        {
            if (user == null)
            {
                _logger.LogError("Cannot send credentials email to null user");
                return;
            }

            // In a real application, you would use an email service to send emails
            _logger.LogInformation($"Simulating sending credentials email to {user.Email}");

            // Example of what would be sent
            var emailBody = $@"
            Hello {user.FirstName},

            Your account has been created in the Green Meadows Portal.

            Username: {user.Email}
            Password: {password}

            Please log in at https://yourportal.com/ and change your password as soon as possible.

            Regards,
            Green Meadows Management
            ";

            // await _emailService.SendEmail(user.Email, "Your Green Meadows Portal Account", emailBody);
            await Task.CompletedTask; // Placeholder for actual email sending
        }

        private async Task SendPasswordResetEmail(ApplicationUser user, string newPassword)
        {
            if (user == null)
            {
                _logger.LogError("Cannot send password reset email to null user");
                return;
            }

            // In a real application, you would use an email service to send emails
            _logger.LogInformation($"Simulating sending password reset email to {user.Email}");

            // Example of what would be sent
            var emailBody = $@"
            Hello {user.FirstName},

            Your password has been reset by an administrator.

            Username: {user.Email}
            New Password: {newPassword}

            Please log in at https://yourportal.com/ and change your password as soon as possible.

            Regards,
            Green Meadows Management
            ";

            // await _emailService.SendEmail(user.Email, "Green Meadows Portal - Password Reset", emailBody);
            await Task.CompletedTask; // Placeholder for actual email sending
        }

        private async Task SendUserApprovalEmail(ApplicationUser user)
        {
            if (user == null)
            {
                _logger.LogError("Cannot send approval email to null user");
                return;
            }

            // In a real application, you would use an email service to send emails
            _logger.LogInformation($"Simulating sending approval email to {user.Email}");

            // Example of what would be sent
            var emailBody = $@"
            Hello {user.FirstName},

            Your Green Meadows Portal account has been approved. You can now log in with your credentials.

            Regards,
            Green Meadows Management
            ";

            // await _emailService.SendEmail(user.Email, "Green Meadows Portal - Account Approved", emailBody);
            await Task.CompletedTask; // Placeholder for actual email sending
        }

        private async Task SendUserRejectionEmail(ApplicationUser user, string notes)
        {
            if (user == null)
            {
                _logger.LogError("Cannot send rejection email to null user");
                return;
            }

            // In a real application, you would use an email service to send emails
            _logger.LogInformation($"Simulating sending rejection email to {user.Email}");

            // Example of what would be sent
            var emailBody = $@"
            Hello {user.FirstName},

            We regret to inform you that your Green Meadows Portal account registration has been rejected.

            " + (!string.IsNullOrEmpty(notes) ? $"Reason: {notes}\n\n" : "") + @"

            If you believe this is an error, please contact our management office.

            Regards,
            Green Meadows Management
            ";

            // await _emailService.SendEmail(user.Email, "Green Meadows Portal - Account Registration Status", emailBody);
            await Task.CompletedTask; // Placeholder for actual email sending
        }

        private async Task SendRequestMoreInfoEmail(ApplicationUser user, string notes)
        {
            if (user == null)
            {
                _logger.LogError("Cannot send request for more info email to null user");
                return;
            }

            // In a real application, you would use an email service to send emails
            _logger.LogInformation($"Simulating sending request for more info email to {user.Email}");

            // Example of what would be sent
            var emailBody = $@"
            Hello {user.FirstName},

            Thank you for registering with the Green Meadows Portal. We need some additional information to complete your registration:

            " + (!string.IsNullOrEmpty(notes) ? $"{notes}\n\n" : "") + @"

            Please log in to your account and provide the requested information, or reply to this email.

            Regards,
            Green Meadows Management
            ";

            // await _emailService.SendEmail(user.Email, "Green Meadows Portal - Additional Information Required", emailBody);
            await Task.CompletedTask; // Placeholder for actual email sending
        }
    }
}