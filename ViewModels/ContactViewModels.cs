using GreenMeadowsPortal.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GreenMeadowsPortal.ViewModels
{
    // Main view model for contact directory
    public class ContactDirectoryViewModel
    {
        // User information for displaying in the layout
        public ApplicationUser CurrentUser { get; set; } = new ApplicationUser();
        public string FirstName { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int NotificationCount { get; set; }

        // Contact directory data
        public List<ContactCategory> ContactCategories { get; set; } = new List<ContactCategory>();
        public List<DepartmentContactViewModel> DepartmentContacts { get; set; } = new List<DepartmentContactViewModel>();
        public List<EmergencyContactViewModel> EmergencyContacts { get; set; } = new List<EmergencyContactViewModel>();
        public List<VendorContactViewModel> VendorContacts { get; set; } = new List<VendorContactViewModel>();
        public List<CommunityContactViewModel> StaffContacts { get; set; } = new List<CommunityContactViewModel>();
        public List<CommunityContactViewModel> CommunityContacts { get; set; } = new List<CommunityContactViewModel>();
    }

    // Department contact view model
    public class DepartmentContactViewModel
    {
        public int Id { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string OfficeHours { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
    }

    // Emergency contact view model
    public class EmergencyContactViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    // Vendor contact view model
    public class VendorContactViewModel
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool IsPreferred { get; set; }
    }

    // Community contact view model
    public class CommunityContactViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public bool ShowEmail { get; set; }
        public bool ShowPhoneNumber { get; set; }
        public bool ShowAddress { get; set; }
    }

    // View model for sending a message
    public class ContactMessageViewModel
    {
        // User information for displaying in the layout
        public ApplicationUser CurrentUser { get; set; } = new ApplicationUser();
        public ApplicationUser ContactUser { get; set; } = new ApplicationUser();
        public string FirstName { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int NotificationCount { get; set; }

        // Contact information
        public string ContactId { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactRole { get; set; } = string.Empty;
        public string ContactDepartment { get; set; } = string.Empty;
        public string ContactImageUrl { get; set; } = string.Empty;

        // Message details
        [Required(ErrorMessage = "Subject is required")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message content is required")]
        public string Message { get; set; } = string.Empty;
    }

    // View model for inbox
    public class ContactInboxViewModel
    {
        // User information for displaying in the layout
        public ApplicationUser CurrentUser { get; set; } = new ApplicationUser();
        public string FirstName { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int NotificationCount { get; set; }

        // Message listings
        public List<ContactMessageListingViewModel> Messages { get; set; } = new List<ContactMessageListingViewModel>();
    }

    // View model for message listing in inbox
    public class ContactMessageListingViewModel
    {
        public int MessageId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime SentDate { get; set; }
        public bool IsRead { get; set; }
        public bool IsFromCurrentUser { get; set; }

        // Sender details
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderProfileImage { get; set; } = string.Empty;

        // Recipient details
        public string RecipientId { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientProfileImage { get; set; } = string.Empty;
    }

    // View model for viewing a message
    public class ViewMessageViewModel
    {
        // User information for displaying in the layout
        public ApplicationUser CurrentUser { get; set; } = new ApplicationUser();
        public string FirstName { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int NotificationCount { get; set; }

        // Message details
        public int MessageId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string MessageContent { get; set; } = string.Empty;
        public DateTime SentDate { get; set; }
        public bool IsRead { get; set; }

        // Sender details
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty;
        public string SenderImageUrl { get; set; } = string.Empty;

        // Recipient details
        public string RecipientId { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
