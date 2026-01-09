using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessing.Api.Models.Entities
{
    public class DocumentProcessingLog
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid DocumentId { get; set; }

        [ForeignKey("DocumentId")]
        public Document Document { get; set; }

        [Required]
        public string StepName { get; set; }

        [Required]
        public string Status { get; set; }

        public string Message { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
