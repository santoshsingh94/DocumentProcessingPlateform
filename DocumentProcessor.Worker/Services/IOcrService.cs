namespace DocumentProcessor.Worker.Services
{
    public interface IOcrService
    {
        Task<Dictionary<int, string>> ExtractTextByPageAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
