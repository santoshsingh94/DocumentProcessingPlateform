# OCR Implementation Notes

## What Was Done

Added Tesseract 5.2 OCR to the Worker service with a normalized database design for storing extracted text.

## Changes

### Database

Created `DocumentContent` table:
- Id, DocumentId, PageNumber, Content, CreatedAt
- Unique index on (DocumentId, PageNumber)
- Cascade delete from Document table

Updated `Document` entity:
- Added DocumentContents navigation property
- Removed the single ExtractedText field

### Services

**IOcrService** - Simple interface returning Dictionary<int, string> for page → text mapping

**TesseractOcrService** - Main OCR implementation
- Thread-safe with SemaphoreSlim
- Configurable tessdata path
- Handles images and PDFs
- PDFs are converted to images using Docnet.Core before processing
- Logs confidence scores and errors

**DocumentUploadedConsumer** - Updated consumer
- Gets document from RabbitMQ queue
- Sets status to Processing
- Calls OCR service
- Saves each page to DocumentContent table
- Sets status to Completed

### Configuration

Added to Program.cs:
```csharp
builder.Services.AddSingleton<IOcrService, TesseractOcrService>();
```

Added to appsettings.json:
```json
"Tesseract": {
  "DataPath": "./tessdata"
}
```

### Packages

- Tesseract 5.2.0
- Docnet.Core 2.6.0
- SkiaSharp 2.88.8

## Setup

1. Stop running Worker if needed
2. Download `eng.traineddata` and place in `DocumentProcessor.Worker/tessdata/`
3. Run migration: `dotnet ef migrations add AddDocumentContentTable`
4. Update database: `dotnet ef database update`

## Notes

PDFs work by:
1. Opening PDF with Docnet
2. Rendering each page to raw pixel data
3. Creating bitmap from pixels
4. Encoding to PNG
5. Running OCR on the image
6. Storing results per page

Images are processed directly without conversion.

## Queries

Get all pages:
```sql
SELECT PageNumber, LEFT(Content, 100) as Preview, LEN(Content) as Length
FROM DocumentContents
WHERE DocumentId = 'guid'
ORDER BY PageNumber;
```

Count pages:
```sql
SELECT d.FileName, COUNT(dc.Id) as Pages
FROM Documents d
LEFT JOIN DocumentContents dc ON d.Id = dc.DocumentId
GROUP BY d.Id, d.FileName;
```
