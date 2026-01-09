using System;
using System.Threading.Tasks;
using DocumentService.Api.Models.DTOs;
using DocumentService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentsController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request)
        {
            try
            {
                var result = await _documentService.UploadDocumentAsync(request);
                return CreatedAtAction(nameof(Upload), new { id = result.DocumentId }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception)
            {
                // Log exception
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }
    }
}
