using System.Threading.Tasks;
using DocumentService.Api.Models.DTOs;

namespace DocumentService.Api.Services
{
    public interface IDocumentService
    {
        Task<UploadDocumentResponse> UploadDocumentAsync(UploadDocumentRequest request);
    }
}
