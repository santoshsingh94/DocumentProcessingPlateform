using System;
using System.IO;
using System.Threading.Tasks;
using DocumentProcessing.Api.Models;
using DocumentProcessing.Api.Models.DTOs;
using DocumentProcessing.Api.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Api.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10 MB, move to config in production
        private readonly string[] _allowedContentTypes = new[] { "application/pdf", "image/png", "image/jpeg" }; // move to config
        private readonly string _uploadRoot = "uploads";

        public DocumentService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UploadDocumentResponse> UploadDocumentAsync(UploadDocumentRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                throw new ArgumentException("File is required.");

            if (request.File.Length > _maxFileSize)
                throw new ArgumentException("File size exceeds the allowed limit.");

            if (Array.IndexOf(_allowedContentTypes, request.File.ContentType) < 0)
                throw new ArgumentException("Unsupported file type.");

            var documentId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Ensure uploads directory exists
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), _uploadRoot);
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var storagePath = Path.Combine(_uploadRoot, documentId.ToString());
            var fullFilePath = Path.Combine(uploadsPath, documentId.ToString());

            // Save file to disk
            using (var stream = new FileStream(fullFilePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            var document = new Document
            {
                Id = documentId,
                FileName = request.File.FileName,
                ContentType = request.File.ContentType,
                SizeInBytes = request.File.Length,
                Status = DocumentStatus.Uploaded,
                OwnerUserId = "system", // Replace with actual user in real scenario
                StoragePath = storagePath,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();

            return new UploadDocumentResponse
            {
                DocumentId = documentId,
                Status = document.Status.ToString()
            };
        }

        public async Task<(string filePath, string contentType, string fileName)> GetDocumentFileAsync(Guid documentId)
        {
            var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (document == null)
                throw new FileNotFoundException("Document not found.");

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), _uploadRoot);
            var fullFilePath = Path.Combine(uploadsPath, documentId.ToString());
            if (!File.Exists(fullFilePath))
                throw new FileNotFoundException("File not found on disk.");

            return (fullFilePath, document.ContentType, document.FileName);
        }
    }
}
