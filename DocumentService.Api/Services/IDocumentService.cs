using System.Threading.Tasks;
using DocumentProcessing.Api.Models.DTOs;

namespace DocumentProcessing.Api.Services
{
    public interface IDocumentService
    {
        Task<UploadDocumentResponse> UploadDocumentAsync(UploadDocumentRequest request);
    }
}
