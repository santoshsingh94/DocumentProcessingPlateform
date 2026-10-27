FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Install Tesseract OCR dependencies
RUN apt-get update && apt-get install -y \
    tesseract-ocr \
    libtesseract-dev \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DocumentProcessor.Worker/DocumentProcessor.Worker.csproj", "DocumentProcessor.Worker/"]
COPY ["DocumentService.Api/DocumentProcessing.Api.csproj", "DocumentService.Api/"]
COPY ["DocumentProcessing.Shared/DocumentProcessing.Shared.csproj", "DocumentProcessing.Shared/"]
RUN dotnet restore "DocumentProcessor.Worker/DocumentProcessor.Worker.csproj"
COPY ["DocumentProcessor.Worker/", "DocumentProcessor.Worker/"]
COPY ["DocumentService.Api/", "DocumentService.Api/"]
COPY ["DocumentProcessing.Shared/", "DocumentProcessing.Shared/"]
WORKDIR "/src/DocumentProcessor.Worker"
RUN dotnet build "DocumentProcessor.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DocumentProcessor.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy tessdata files
COPY ["DocumentProcessor.Worker/tessdata", "/app/tessdata"]

ENTRYPOINT ["dotnet", "DocumentProcessor.Worker.dll"]