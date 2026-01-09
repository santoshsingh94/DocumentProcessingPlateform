using System;
using System.Threading.Tasks;
using DocumentProcessing.Api.Models.DTOs;
using DocumentProcessing.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessing.Api.Controllers
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

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(Guid id)
        {
            try
            {
                var (filePath, contentType, fileName) = await _documentService.GetDocumentFileAsync(id);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, contentType, fileName);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception)
            {
                // Log exception
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }
    }
}
