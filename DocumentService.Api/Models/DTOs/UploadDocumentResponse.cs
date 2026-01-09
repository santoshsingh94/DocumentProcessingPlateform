using System;

namespace DocumentService.Api.Models.DTOs
{
    public class UploadDocumentResponse
    {
        public Guid DocumentId { get; set; }
        public string Status { get; set; }
    }
}
