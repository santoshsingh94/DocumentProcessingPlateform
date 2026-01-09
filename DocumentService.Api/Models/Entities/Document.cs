using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessing.Api.Models.Entities
{
    public class Document
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string FileName { get; set; }

        [Required]
        public string ContentType { get; set; }

        public long SizeInBytes { get; set; }

        [Required]
        public DocumentStatus Status { get; set; }

        [Required]
        public string OwnerUserId { get; set; }

        public string StoragePath { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DocumentProcessingLog> ProcessingLogs { get; set; }
    }
}
