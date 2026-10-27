FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DocumentService.Api/DocumentProcessing.Api.csproj", "DocumentService.Api/"]
COPY ["DocumentProcessing.Shared/DocumentProcessing.Shared.csproj", "DocumentProcessing.Shared/"]
RUN dotnet restore "DocumentService.Api/DocumentProcessing.Api.csproj"
COPY ["DocumentService.Api/", "DocumentService.Api/"]
COPY ["DocumentProcessing.Shared/", "DocumentProcessing.Shared/"]
WORKDIR "/src/DocumentService.Api"
RUN dotnet build "DocumentProcessing.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DocumentProcessing.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DocumentProcessing.Api.dll"]