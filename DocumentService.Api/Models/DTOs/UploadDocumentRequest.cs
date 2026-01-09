using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace DocumentProcessing.Api.Models.DTOs
{
    public class UploadDocumentRequest
    {
        [Required]
        public IFormFile File { get; set; }
    }
}
