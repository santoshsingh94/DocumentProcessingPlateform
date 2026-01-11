using System;

namespace DocumentProcessing.Api.Models.DTOs
{
    public class DocumentStatusResponse
    {
        public Guid DocumentId { get; set; }
        public string? Status { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
