using GreenMeadowsPortal.Models;
using GreenMeadowsPortal.Services;
using GreenMeadowsPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GreenMeadowsPortal.Controllers
{
    [Authorize]
    public class DocumentController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly NotificationService _notificationService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment hostEnvironment,
            NotificationService notificationService,
            ILogger<DocumentController> logger)
        {
            _userManager = userManager;
            _hostEnvironment = hostEnvironment;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: /Document/
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = roles.Contains("Admin");
            var isStaff = roles.Contains("Staff");

            // Create view model with user info for layout
            var viewModel = new DocumentLibraryViewModel
            {
                FirstName = user.FirstName,
                ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                CurrentUserId = user.Id,
                CurrentUserRole = roles.FirstOrDefault() ?? "User",
                NotificationCount = await _notificationService.GetUnreadCountAsync(user.Id)
            };

            // Get appropriate documents based on role
            viewModel.DocumentCategories = GetDocumentCategories(isAdmin, isStaff);

            return View(viewModel);
        }

        // GET: /Document/Upload
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Upload()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);

            var viewModel = new DocumentUploadViewModel
            {
                FirstName = user.FirstName,
                ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png",
                Role = roles.FirstOrDefault() ?? "User",
                NotificationCount = await _notificationService.GetUnreadCountAsync(user.Id),
                Categories = GetCategorySelectList()
            };

            return View(viewModel);
        }

        // POST: /Document/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Upload(DocumentUploadViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                if (model.DocumentFile != null && model.DocumentFile.Length > 0)
                {
                    try
                    {
                        // Save document file
                        string documentUrl = await SaveDocumentFileAsync(model.DocumentFile, model.Category);

                        // Create document model to save in database
                        var document = new DocumentModel
                        {
                            Name = model.Name,
                            Description = model.Description,
                            Category = model.Category,
                            VisibleTo = model.VisibleTo,
                            FileUrl = documentUrl,
                            UploadedById = user.Id,
                            UploadDate = DateTime.Now
                        };

                        // In a real implementation, save to database
                        // await _documentService.CreateDocumentAsync(document);

                        TempData["SuccessMessage"] = "Document uploaded successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error uploading document: {ex.Message}");
                        ModelState.AddModelError(string.Empty, $"Error uploading document: {ex.Message}");
                    }
                }
                else
                {
                    ModelState.AddModelError("DocumentFile", "Please select a file to upload.");
                }
            }

            // If we get here, something failed, redisplay form
            var roles = await _userManager.GetRolesAsync(user);
            model.FirstName = user.FirstName;
            model.ProfileImageUrl = user.ProfileImageUrl ?? "/images/default-avatar.png";
            model.Role = roles.FirstOrDefault() ?? "User";
            model.NotificationCount = await _notificationService.GetUnreadCountAsync(user.Id);
            model.Categories = GetCategorySelectList();

            return View(model);
        }

        // GET: /Document/Download/5
        public async Task<IActionResult> Download(int id)
        {
            // In a real implementation, retrieve from database
            // var document = await _documentService.GetDocumentByIdAsync(id);

            // For demo purposes
            var document = GetSampleDocumentById(id);

            if (document == null)
                return NotFound();

            // Check if user has access to this document
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = roles.Contains("Admin");
            var isStaff = roles.Contains("Staff");

            if (!isAdmin && document.VisibleTo == "Admin" ||
                !isAdmin && !isStaff && document.VisibleTo == "Staff")
            {
                return Forbid();
            }

            // Get file path
            string filePath = Path.Combine(_hostEnvironment.WebRootPath, document.FileUrl.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                // For demo, redirect to a sample PDF
                return Redirect("/sample-documents/document.pdf");
            }

            // Return file
            return PhysicalFile(filePath, GetContentType(filePath), document.Name);
        }

        // GET: /Document/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            // In a real implementation, retrieve from database
            // var document = await _documentService.GetDocumentByIdAsync(id);

            // For demo purposes
            var document = GetSampleDocumentById(id);

            if (document == null)
                return NotFound();

            return View(document);
        }

        // POST: /Document/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // In a real implementation, retrieve from database
            // var document = await _documentService.GetDocumentByIdAsync(id);

            // For demo purposes
            var document = GetSampleDocumentById(id);

            if (document == null)
                return NotFound();

            // Delete file
            string filePath = Path.Combine(_hostEnvironment.WebRootPath, document.FileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            // Delete from database
            // await _documentService.DeleteDocumentAsync(id);

            TempData["SuccessMessage"] = "Document deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // Helper methods
        private async Task<string> SaveDocumentFileAsync(IFormFile file, string category)
        {
            string subfolderPath = Path.Combine("documents", category.ToLower().Replace(" ", "-"));
            string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, subfolderPath);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Clean the filename to prevent any path traversal attacks
            string fileName = Path.GetFileName(file.FileName);

            // Generate unique filename
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/{subfolderPath}/{uniqueFileName}";
        }

        private List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GetCategorySelectList()
        {
            return new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
            {
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Community Guidelines", Text = "Community Guidelines" },
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Financial Reports", Text = "Financial Reports" },
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Meeting Minutes", Text = "Meeting Minutes" },
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Forms", Text = "Forms" },
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Newsletters", Text = "Newsletters" },
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Property Maps", Text = "Property Maps" },
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = "Other", Text = "Other" }
            };
        }

        private List<DocumentCategoryViewModel> GetDocumentCategories(bool isAdmin, bool isStaff)
        {
            var categories = new List<DocumentCategoryViewModel>();

            // Community Guidelines (visible to all)
            categories.Add(new DocumentCategoryViewModel
            {
                Name = "Community Guidelines",
                Icon = "fa-book",
                Description = "Rules and guidelines for community residents",
                Documents = GetSampleDocumentsByCategory("Community Guidelines")
            });

            // Forms (visible to all)
            categories.Add(new DocumentCategoryViewModel
            {
                Name = "Forms",
                Icon = "fa-file-alt",
                Description = "Downloadable forms for various community requests",
                Documents = GetSampleDocumentsByCategory("Forms")
            });

            // Meeting Minutes (visible to all)
            categories.Add(new DocumentCategoryViewModel
            {
                Name = "Meeting Minutes",
                Icon = "fa-clipboard",
                Description = "Minutes from community meetings and board meetings",
                Documents = GetSampleDocumentsByCategory("Meeting Minutes")
            });

            // Financial Reports (Admin/Staff only)
            if (isAdmin || isStaff)
            {
                categories.Add(new DocumentCategoryViewModel
                {
                    Name = "Financial Reports",
                    Icon = "fa-chart-pie",
                    Description = "Financial statements and budgets",
                    Documents = GetSampleDocumentsByCategory("Financial Reports")
                });
            }

            // Newsletters
            categories.Add(new DocumentCategoryViewModel
            {
                Name = "Newsletters",
                Icon = "fa-newspaper",
                Description = "Community newsletters and announcements",
                Documents = GetSampleDocumentsByCategory("Newsletters")
            });

            // Property Maps
            categories.Add(new DocumentCategoryViewModel
            {
                Name = "Property Maps",
                Icon = "fa-map",
                Description = "Maps of the community and property layouts",
                Documents = GetSampleDocumentsByCategory("Property Maps")
            });

            // Admin Documents (Admin only)
            if (isAdmin)
            {
                categories.Add(new DocumentCategoryViewModel
                {
                    Name = "Administrative",
                    Icon = "fa-user-shield",
                    Description = "Administrative documents for management",
                    Documents = GetSampleDocumentsByCategory("Administrative")
                });
            }

            return categories;
        }

        private List<DocumentViewModel> GetSampleDocumentsByCategory(string category)
        {
            var documents = new List<DocumentViewModel>();

            switch (category)
            {
                case "Community Guidelines":
                    documents.Add(CreateSampleDocument(1, "Resident Handbook", "Comprehensive guide for all residents", "PDF", "2MB", "Admin", "Community Guidelines", DateTime.Now.AddMonths(-6)));
                    documents.Add(CreateSampleDocument(2, "Pet Policy", "Rules regarding pets in the community", "PDF", "1MB", "All", "Community Guidelines", DateTime.Now.AddMonths(-3)));
                    documents.Add(CreateSampleDocument(3, "Architectural Guidelines", "Standards for property modifications", "PDF", "3MB", "All", "Community Guidelines", DateTime.Now.AddMonths(-1)));
                    break;
                case "Forms":
                    documents.Add(CreateSampleDocument(4, "Maintenance Request Form", "Form to request maintenance services", "PDF", "1MB", "All", "Forms", DateTime.Now.AddMonths(-2)));
                    documents.Add(CreateSampleDocument(5, "Architectural Change Request", "Form for requesting property modifications", "DOCX", "500KB", "All", "Forms", DateTime.Now.AddDays(-15)));
                    documents.Add(CreateSampleDocument(6, "Amenity Reservation Form", "Form to reserve community facilities", "PDF", "700KB", "All", "Forms", DateTime.Now.AddDays(-7)));
                    break;
                case "Meeting Minutes":
                    documents.Add(CreateSampleDocument(7, "January Board Meeting Minutes", "Minutes from the January board meeting", "PDF", "1MB", "All", "Meeting Minutes", DateTime.Now.AddMonths(-4)));
                    documents.Add(CreateSampleDocument(8, "February Board Meeting Minutes", "Minutes from the February board meeting", "PDF", "1.2MB", "All", "Meeting Minutes", DateTime.Now.AddMonths(-3)));
                    documents.Add(CreateSampleDocument(9, "Annual HOA Meeting Minutes", "Minutes from the annual homeowners meeting", "PDF", "2MB", "All", "Meeting Minutes", DateTime.Now.AddMonths(-2)));
                    break;
                case "Financial Reports":
                    documents.Add(CreateSampleDocument(10, "Q1 Financial Statement", "First quarter financial report", "PDF", "1.5MB", "Staff", "Financial Reports", DateTime.Now.AddMonths(-3)));
                    documents.Add(CreateSampleDocument(11, "Q2 Financial Statement", "Second quarter financial report", "PDF", "1.6MB", "Staff", "Financial Reports", DateTime.Now.AddMonths(-1)));
                    documents.Add(CreateSampleDocument(12, "Annual Budget", "Current year budget", "PDF", "2.5MB", "Staff", "Financial Reports", DateTime.Now.AddDays(-14)));
                    break;
                case "Newsletters":
                    documents.Add(CreateSampleDocument(13, "January Newsletter", "Community updates for January", "PDF", "2MB", "All", "Newsletters", DateTime.Now.AddMonths(-4)));
                    documents.Add(CreateSampleDocument(14, "February Newsletter", "Community updates for February", "PDF", "2.1MB", "All", "Newsletters", DateTime.Now.AddMonths(-3)));
                    documents.Add(CreateSampleDocument(15, "March Newsletter", "Community updates for March", "PDF", "1.9MB", "All", "Newsletters", DateTime.Now.AddMonths(-2)));
                    break;
                case "Property Maps":
                    documents.Add(CreateSampleDocument(16, "Community Master Plan", "Complete layout of the community", "PDF", "5MB", "All", "Property Maps", DateTime.Now.AddMonths(-12)));
                    documents.Add(CreateSampleDocument(17, "Amenities Map", "Map showing locations of all community amenities", "PDF", "3MB", "All", "Property Maps", DateTime.Now.AddMonths(-6)));
                    break;
                case "Administrative":
                    documents.Add(CreateSampleDocument(18, "Vendor Contracts", "Current vendor service agreements", "PDF", "4MB", "Admin", "Administrative", DateTime.Now.AddMonths(-5)));
                    documents.Add(CreateSampleDocument(19, "Insurance Policies", "Community insurance documentation", "PDF", "3MB", "Admin", "Administrative", DateTime.Now.AddMonths(-2)));
                    documents.Add(CreateSampleDocument(20, "Employee Handbook", "Guidelines for staff members", "PDF", "2.5MB", "Admin", "Administrative", DateTime.Now.AddDays(-20)));
                    break;
            }

            return documents;
        }

        private DocumentViewModel CreateSampleDocument(int id, string name, string description, string fileType, string fileSize, string visibleTo, string category, DateTime uploadDate)
        {
            return new DocumentViewModel
            {
                Id = id,
                Name = name,
                Description = description,
                FileType = fileType,
                FileSize = fileSize,
                VisibleTo = visibleTo,
                Category = category,
                UploadDate = uploadDate,
                FileUrl = $"/sample-documents/{fileType.ToLower()}-icon.png" // Just a placeholder URL
            };
        }

        private DocumentViewModel GetSampleDocumentById(int id)
        {
            // Flatten all sample documents and find by ID
            var allDocuments = new List<DocumentViewModel>();

            var categories = new[]
            {
                "Community Guidelines", "Forms", "Meeting Minutes",
                "Financial Reports", "Newsletters", "Property Maps", "Administrative"
            };

            foreach (var category in categories)
            {
                allDocuments.AddRange(GetSampleDocumentsByCategory(category));
            }

            return allDocuments.FirstOrDefault(d => d.Id == id);
        }

        private string GetContentType(string path)
        {
            var extension = Path.GetExtension(path).ToLower();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "application/octet-stream",
            };
        }
    }
}