# OCR Setup Guide

## Overview

The Worker service uses Tesseract 5.2 for text extraction from documents.

## Supported Formats

- Images: JPG, JPEG, PNG, BMP, TIFF
- PDF: Single and multi-page documents

## Setup

### 1. Download Language Data

Get the `eng.traineddata` file from the [tessdata repository](https://github.com/tesseract-ocr/tessdata).

### 2. Create Folder Structure

```
DocumentProcessor.Worker/
├── tessdata/
│   └── eng.traineddata
├── Services/
└── Program.cs
```

### 3. Optional: Add More Languages

Download additional `.traineddata` files and place them in the `tessdata` folder. Update the language parameter in `TesseractOcrService.cs` if needed.

## Database Setup

Create and apply the migration:

```bash
cd DocumentService.Api
dotnet ef migrations add AddDocumentContentTable
dotnet ef database update
```

## How It Works

- Text is extracted page-by-page
- Each page is stored as a separate record in the DocumentContent table
- The table structure:
  - DocumentId: Links to the parent document
  - PageNumber: 1-indexed page number
  - Content: Extracted text
  - CreatedAt: Timestamp

For PDFs, each page is converted to an image before OCR processing.

## Testing

Upload a document through the API and check:
- Document status transitions: Uploaded → Processing → Completed
- DocumentContent table has records for each page
- Worker logs show confidence scores

Query example:
```sql
SELECT PageNumber, LEN(Content) as ContentLength
FROM DocumentContents
WHERE DocumentId = 'your-document-guid'
ORDER BY PageNumber;
```
