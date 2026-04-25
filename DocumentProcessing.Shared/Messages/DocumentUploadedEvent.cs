using System;

namespace DocumentProcessing.Shared.Messages
{
    public class DocumentUploadedEvent
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
