using System;
using System.Threading.Tasks;
using DocumentProcessing.Api.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessing.Api.Services
{
    public interface IDocumentService
    {
        Task<UploadDocumentResponse> UploadDocumentAsync(UploadDocumentRequest request);
        Task<(string filePath, string contentType, string fileName)> GetDocumentFileAsync(Guid documentId);
    }
}
