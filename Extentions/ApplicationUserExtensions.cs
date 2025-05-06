using GreenMeadowsPortal.Models;

namespace GreenMeadowsPortal.Extensions
{
    public static class ApplicationUserExtensions
    {
        // Helper extension method for determining if email should be shown
        public static bool ShowEmail(this ApplicationUser user)
        {
            // For staff and admins, always show email
            // For residents, depends on their preferences
            return true;
        }

        // Helper extension method for determining if phone should be shown
        public static bool ShowPhoneNumber(this ApplicationUser user)
        {
            // Simplified logic
            return !string.IsNullOrEmpty(user.PhoneNumber);
        }
    }
}