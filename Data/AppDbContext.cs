using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GreenMeadowsPortal.Models;

namespace GreenMeadowsPortal.Data
{
    // Change from DbContext to IdentityDbContext<ApplicationUser>
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Add the missing DbSet for Announcements  
        public DbSet<AdminAnnouncement> Announcements { get; set; }
        public DbSet<AnnouncementReadReceipt> AnnouncementReadReceipts { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<DocumentModel> Documents { get; set; }

        // Contact Directory Models
        public DbSet<ContactCategory> ContactCategories { get; set; }
        public DbSet<DepartmentContact> DepartmentContacts { get; set; }
        public DbSet<EmergencyContact> EmergencyContacts { get; set; }
        public DbSet<VendorContact> VendorContacts { get; set; }
        public DbSet<CommunityContact> CommunityContacts { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }

        // Don't need this line anymore since IdentityDbContext already includes Users
        // public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<DocumentModel>()
                 .HasOne(d => d.UploadedBy)
                 .WithMany()
                 .HasForeignKey(d => d.UploadedById)
                 .OnDelete(DeleteBehavior.Restrict);

            // Set up indexes
            builder.Entity<DocumentModel>()
                .HasIndex(d => d.Category);

            builder.Entity<DocumentModel>()
                .HasIndex(d => d.VisibleTo);
            // Contact Message Soft Delete Behavior
            builder.Entity<ContactMessage>()
                .HasQueryFilter(m =>
                    (!m.DeletedBySender && !m.DeletedByRecipient) ||
                    (m.DeletedBySender != m.DeletedByRecipient));
        }
    }
}