using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace DocumentService.Api.Models.DTOs
{
    public class UploadDocumentRequest
    {
        [Required]
        public IFormFile File { get; set; }
    }
}
