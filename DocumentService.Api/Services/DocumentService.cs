using System;
using System.IO;
using System.Threading.Tasks;
using DocumentProcessing.Api.Models;
using DocumentProcessing.Api.Models.DTOs;
using DocumentProcessing.Api.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace DocumentProcessing.Api.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10 MB, move to config in production
        private readonly string[] _allowedContentTypes = new[] { "application/pdf", "image/png", "image/jpeg" }; // move to config

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

            // In production, save file to storage and set StoragePath accordingly
            var storagePath = Path.Combine("uploads", documentId.ToString());

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

            // File saving to disk/storage is omitted for brevity

            return new UploadDocumentResponse
            {
                DocumentId = documentId,
                Status = document.Status.ToString()
            };
        }
    }
}
