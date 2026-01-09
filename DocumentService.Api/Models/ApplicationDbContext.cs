using Microsoft.EntityFrameworkCore;
using DocumentProcessing.Api.Models.Entities;

namespace DocumentProcessing.Api.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentProcessingLog> DocumentProcessingLogs { get; set; }
        public DbSet<AuditTrail> AuditTrails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Document
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(256);
                entity.Property(e => e.ContentType).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.OwnerUserId).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.OwnerUserId);
                entity.HasMany(e => e.ProcessingLogs)
                      .WithOne(e => e.Document)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // DocumentProcessingLog
            modelBuilder.Entity<DocumentProcessingLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StepName).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // AuditTrail
            modelBuilder.Entity<AuditTrail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityName).IsRequired();
                entity.Property(e => e.EntityId).IsRequired();
                entity.Property(e => e.Action).IsRequired();
                entity.Property(e => e.PerformedBy).IsRequired();
                entity.Property(e => e.PerformedAt).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
