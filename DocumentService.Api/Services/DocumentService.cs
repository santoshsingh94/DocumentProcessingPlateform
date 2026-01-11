using DocumentProcessing.Api.Models;
using DocumentProcessing.Api.Models.DTOs;
using DocumentProcessing.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace DocumentProcessing.Api.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IDistributedCache _cache;
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10 MB, move to config in production
        private readonly string[] _allowedContentTypes = new[] { "application/pdf", "image/png", "image/jpeg" }; // move to config
        private readonly string _uploadRoot = "uploads";
        private readonly DistributedCacheEntryOptions _cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

        public DocumentService(ApplicationDbContext dbContext, IDistributedCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
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

        public async Task<DocumentStatusResponse> GetDocumentStatusAsync(Guid documentId)
        {
            var cacheKey = $"document:status:{documentId}";
            try
            {
                var cached = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cached))
                {
                    return JsonSerializer.Deserialize<DocumentStatusResponse>(cached);
                }
            }
            catch
            {
                // Redis unavailable, fallback to DB
            }

            var document = await _dbContext.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId);
            if (document == null)
                return null;

            var response = new DocumentStatusResponse
            {
                DocumentId = document.Id,
                Status = document.Status.ToString(),
                LastUpdatedAt = document.UpdatedAt
            };

            try
            {
                var serialized = JsonSerializer.Serialize(response);
                await _cache.SetStringAsync(cacheKey, serialized, _cacheOptions);
            }
            catch
            {
                // Redis unavailable, skip caching
            }

            return response;
        }
    }
}
