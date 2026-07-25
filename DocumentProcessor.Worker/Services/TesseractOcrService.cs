using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tesseract;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Converters;
using SkiaSharp;

namespace DocumentProcessor.Worker.Services
{
    public class TesseractOcrService : IOcrService, IDisposable
    {
        private readonly ILogger<TesseractOcrService> _logger;
        private readonly string _tessDataPath;
        private readonly SemaphoreSlim _semaphore;

        public TesseractOcrService(IConfiguration configuration, ILogger<TesseractOcrService> logger)
        {
            _logger = logger;
            _tessDataPath = configuration["Tesseract:DataPath"] ?? "./tessdata";
            _semaphore = new SemaphoreSlim(1, 1);
            
            ValidateTessDataPath();
        }

        private void ValidateTessDataPath()
        {
            if (!Directory.Exists(_tessDataPath))
            {
                _logger.LogWarning("Tesseract data path not found: {Path}. Creating directory.", _tessDataPath);
                Directory.CreateDirectory(_tessDataPath);
            }

            var engDataFile = Path.Combine(_tessDataPath, "eng.traineddata");
            if (!File.Exists(engDataFile))
            {
                _logger.LogWarning(
                    "Tesseract language data file not found. Download 'eng.traineddata' from " +
                    "https://github.com/tesseract-ocr/tessdata and place it in: {Path}", 
                    _tessDataPath);
            }
        }

        public async Task<Dictionary<int, string>> ExtractTextByPageAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<int, string>();

            // Normalize path and validate
            var normalizedPath = Path.GetFullPath(filePath);
            
            if (!File.Exists(normalizedPath))
            {
                _logger.LogWarning("File not found: {FilePath} (normalized: {NormalizedPath})", filePath, normalizedPath);
                return result;
            }

            // Validate file extension for supported image formats
            var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
            var supportedFormats = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".pdf" };
            
            if (!supportedFormats.Contains(extension))
            {
                _logger.LogWarning(
                    "Unsupported file format: {Extension}. File: {FilePath}. Supported formats: {Formats}",
                    extension,
                    normalizedPath,
                    string.Join(", ", supportedFormats));
                return result;
            }

            // Check file can be read
            try
            {
                using var fileStream = File.OpenRead(normalizedPath);
                if (fileStream.Length == 0)
                {
                    _logger.LogWarning("File is empty: {FilePath}", normalizedPath);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot access file: {FilePath}", normalizedPath);
                return result;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting OCR processing. TessData path: {TessDataPath}", _tessDataPath);
                
                // Check if tessdata files exist
                var engDataFile = Path.Combine(_tessDataPath, "eng.traineddata");
                if (!File.Exists(engDataFile))
                {
                    _logger.LogError(
                        "Tesseract language data file NOT FOUND at: {DataFile}. " +
                        "Download 'eng.traineddata' from https://github.com/tesseract-ocr/tessdata " +
                        "and place it in the tessdata folder.", 
                        engDataFile);
                    return result;
                }

                return await Task.Run(() =>
                {
                    _logger.LogInformation("Initializing Tesseract engine...");
                    using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                    
                    // Check if file is PDF
                    if (extension == ".pdf")
                    {
                        return ProcessPdfWithOcr(normalizedPath, engine);
                    }
                    else
                    {
                        return ProcessImageWithOcr(normalizedPath, engine);
                    }
                }, cancellationToken);
            }
            catch (IOException ioEx)
            {
                _logger.LogError(ioEx, 
                    "Failed to load image file: {FilePath}. Check if file is a valid image format and not corrupted.", 
                    normalizedPath);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR extraction failed for {FilePath}", normalizedPath);
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private Dictionary<int, string> ProcessImageWithOcr(string imagePath, TesseractEngine engine)
        {
            var result = new Dictionary<int, string>();
            
            _logger.LogInformation("Loading image: {FilePath}", imagePath);
            using var img = Pix.LoadFromFile(imagePath);
            
            if (img == null)
            {
                _logger.LogError("Failed to load image - Pix returned null: {FilePath}", imagePath);
                return result;
            }

            _logger.LogInformation("Image loaded successfully: {Width}x{Height} pixels. Processing OCR...", img.Width, img.Height);
            using var page = engine.Process(img);
            
            var text = page.GetText()?.Trim() ?? string.Empty;
            var confidence = page.GetMeanConfidence();
            
            // Single page image - page number is 1
            result[1] = text;
            
            _logger.LogInformation(
                "OCR completed for {FileName}. Confidence: {Confidence:P0}, Characters: {CharCount}", 
                Path.GetFileName(imagePath),
                confidence,
                text.Length);

            return result;
        }

        private Dictionary<int, string> ProcessPdfWithOcr(string pdfPath, TesseractEngine engine)
        {
            var result = new Dictionary<int, string>();
            
            _logger.LogInformation("Loading PDF: {FilePath}", pdfPath);
            
            using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(1.5));
            var pageCount = docReader.GetPageCount();
            
            _logger.LogInformation("PDF has {PageCount} page(s). Starting OCR...", pageCount);

            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                try
                {
                    using var pageReader = docReader.GetPageReader(pageIndex);
                    var pageNumber = pageIndex + 1;
                    
                    _logger.LogInformation("Processing PDF page {PageNumber}/{TotalPages}", pageNumber, pageCount);
                    
                    // Render PDF page to raw bytes (RGB format)
                    var rawBytes = pageReader.GetImage(new NaiveTransparencyRemover(0xFF, 0xFF, 0xFF));
                    var width = pageReader.GetPageWidth();
                    var height = pageReader.GetPageHeight();
                    
                    _logger.LogInformation("Page {PageNumber} rendered: {Width}x{Height} pixels", pageNumber, width, height);
                    
                    // Create bitmap from raw pixel data
                    var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var bitmap = new SKBitmap();
                    
                    if (!bitmap.TryAllocPixels(info))
                    {
                        _logger.LogWarning("Failed to allocate bitmap for page {PageNumber}", pageNumber);
                        continue;
                    }
                    
                    // Copy raw bytes to bitmap
                    var pixels = bitmap.GetPixels();
                    System.Runtime.InteropServices.Marshal.Copy(rawBytes, 0, pixels, rawBytes.Length);
                    
                    // Encode bitmap to PNG for Tesseract
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    
                    // Load into Tesseract
                    using var pix = Pix.LoadFromMemory(data.ToArray());
                    if (pix == null)
                    {
                        _logger.LogWarning("Failed to create Pix from page {PageNumber}", pageNumber);
                        continue;
                    }

                    using var page = engine.Process(pix);
                    var text = page.GetText()?.Trim() ?? string.Empty;
                    var confidence = page.GetMeanConfidence();
                    
                    result[pageNumber] = text;
                    
                    _logger.LogInformation(
                        "OCR completed for page {PageNumber}/{TotalPages}. Confidence: {Confidence:P0}, Characters: {CharCount}",
                        pageNumber,
                        pageCount,
                        confidence,
                        text.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process PDF page {PageNumber}", pageIndex + 1);
                }
            }

            return result;
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
