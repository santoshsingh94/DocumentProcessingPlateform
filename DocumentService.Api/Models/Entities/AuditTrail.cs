using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentProcessing.Api.Models.Entities
{
    public class AuditTrail
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string EntityName { get; set; }

        [Required]
        public string EntityId { get; set; }

        [Required]
        public string Action { get; set; }

        [Required]
        public string PerformedBy { get; set; }

        [Required]
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    }
}
