---
sidebar_position: 2
title: Data Export
---

# Data Export

This page provides comprehensive examples and patterns for implementing data export functionality using AsyncEndpoints, including report generation, CSV/Excel exports, progress tracking, and download mechanisms.

## Overview

Data export is a common requirement in enterprise applications where users need to download large datasets or reports. AsyncEndpoints provides the perfect architecture for handling these long-running operations without blocking the user interface.

## Basic Data Export Example

### Request Model for Data Export

```csharp
public class DataExportRequest
{
    public string ExportFormat { get; set; } = "csv"; // csv, excel, json, pdf
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Dictionary<string, string> Filters { get; set; } = new();
    public List<string> SelectedFields { get; set; } = new();
    public string UserId { get; set; } = string.Empty;
    public string ReportTitle { get; set; } = "Export Report";
    public bool IncludeHeaders { get; set; } = true;
    public string Encoding { get; set; } = "utf-8";
}
```

### Response Model for Data Export

```csharp
public class DataExportResult
{
    public string ExportFileId { get; set; } = string.Empty;
    public string ExportFileName { get; set; } = string.Empty;
    public string ExportFormat { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public int RecordCount { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public TimeSpan ProcessingTime { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
}
```

### Basic Data Export Handler

```csharp
public class DataExportHandler(
    ILogger<DataExportHandler> logger,
    IDataExportService dataExportService,
    IFileStorageService fileStorageService) 
    : IAsyncEndpointRequestHandler<DataExportRequest, DataExportResult>
{
    public async Task<MethodResult<DataExportResult>> HandleAsync(AsyncContext<DataExportRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var startTime = DateTimeOffset.UtcNow;
        
        logger.LogInformation(
            "Starting data export for user {UserId}, format: {Format}, date range: {StartDate} to {EndDate}",
            request.UserId, request.ExportFormat, request.StartDate, request.EndDate);
        
        try
        {
            // Validate export format
            if (!IsValidExportFormat(request.ExportFormat))
            {
                return MethodResult<DataExportResult>.Failure(
                    AsyncEndpointError.FromCode("INVALID_FORMAT", $"Export format '{request.ExportFormat}' is not supported")
                );
            }
            
            // Export data based on format
            var exportData = await dataExportService.ExportDataAsync(request, token);
            
            // Save exported data to storage
            var exportFileId = await fileStorageService.StoreExportAsync(
                exportData, 
                request.ExportFormat, 
                token);
            
            var result = new DataExportResult
            {
                ExportFileId = exportFileId,
                ExportFileName = GenerateFileName(request),
                ExportFormat = request.ExportFormat,
                FileSize = exportData.Data.Length,
                ExportedAt = DateTime.UtcNow,
                RecordCount = exportData.RecordCount,
                DownloadUrl = $"/api/exports/download/{exportFileId}",
                ProcessingTime = DateTimeOffset.UtcNow - startTime,
                Summary = $"Exported {exportData.RecordCount} records in {request.ExportFormat} format"
            };
            
            logger.LogInformation(
                "Data export completed for user {UserId}, {RecordCount} records, {FileSize} bytes",
                request.UserId, result.RecordCount, result.FileSize);
            
            return MethodResult<DataExportResult>.Success(result);
        }
        catch (ExportLimitExceededException ex)
        {
            logger.LogWarning(
                "Export limit exceeded for user {UserId}: {Message}", 
                request.UserId, ex.Message);
            
            return MethodResult<DataExportResult>.Failure(
                AsyncEndpointError.FromCode("EXPORT_LIMIT_EXCEEDED", ex.Message)
            );
        }
        catch (DataAccessExeption ex)
        {
            logger.LogError(
                ex, 
                "Data access error during export for user {UserId}", 
                request.UserId);
            
            return MethodResult<DataExportResult>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, 
                "Unexpected error during data export for user {UserId}", 
                request.UserId);
            
            return MethodResult<DataExportResult>.Failure(ex);
        }
    }
    
    private static bool IsValidExportFormat(string format)
    {
        var supportedFormats = new[] { "csv", "excel", "json", "xml", "pdf" };
        return supportedFormats.Contains(format.ToLowerInvariant());
    }
    
    private static string GenerateFileName(DataExportRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var format = request.ExportFormat.ToLowerInvariant();
        
        return $"{request.ReportTitle.Replace(" ", "_")}_{timestamp}.{format}";
    }
}
```

## Advanced Export with Progress Tracking

### Progress-Trackable Export Handler

```csharp
public class ProgressTrackableExportHandler(
    ILogger<ProgressTrackableExportHandler> logger,
    IDataExportService dataExportService,
    IFileStorageService fileStorageService,
    IProgressTracker progressTracker) 
    : IAsyncEndpointRequestHandler<DataExportRequest, DataExportResult>
{
    public async Task<MethodResult<DataExportResult>> HandleAsync(AsyncContext<DataExportRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var jobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString());
        var startTime = DateTimeOffset.UtcNow;
        
        logger.LogInformation("Starting progress-trackable export job {JobId} for user {UserId}", jobId, request.UserId);
        
        try
        {
            // Initialize progress tracking
            var progress = new ExportProgress
            {
                JobId = jobId,
                Status = "Initializing",
                TotalRecords = 0, // Will be updated
                ProcessedRecords = 0,
                StartTime = startTime
            };
            
            await progressTracker.UpdateProgressAsync(progress, token);
            
            // Create export with progress updates
            var exportData = await dataExportService.ExportDataWithProgressAsync(
                request,
                async (current, total) =>
                {
                    var currentProgress = new ExportProgress
                    {
                        JobId = jobId,
                        Status = "Processing",
                        TotalRecords = total,
                        ProcessedRecords = current,
                        ProgressPercentage = total > 0 ? (int)((double)current / total * 100) : 0,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    
                    await progressTracker.UpdateProgressAsync(currentProgress, CancellationToken.None);
                },
                token);
            
            // Update to "saving" status
            progress.Status = "Saving";
            progress.TotalRecords = exportData.RecordCount;
            progress.ProcessedRecords = exportData.RecordCount;
            await progressTracker.UpdateProgressAsync(progress, token);
            
            // Save exported data
            var exportFileId = await fileStorageService.StoreExportAsync(
                exportData, 
                request.ExportFormat, 
                token);
            
            // Final progress update
            progress.Status = "Completed";
            progress.ExportFileId = exportFileId;
            progress.CompletionTime = DateTimeOffset.UtcNow;
            await progressTracker.UpdateProgressAsync(progress, token);
            
            var result = new DataExportResult
            {
                ExportFileId = exportFileId,
                ExportFileName = GenerateFileName(request),
                ExportFormat = request.ExportFormat,
                FileSize = exportData.Data.Length,
                ExportedAt = DateTime.UtcNow,
                RecordCount = exportData.RecordCount,
                DownloadUrl = $"/api/exports/download/{exportFileId}",
                ProcessingTime = DateTimeOffset.UtcNow - startTime,
                Summary = $"Exported {exportData.RecordCount} records in {request.ExportFormat} format"
            };
            
            logger.LogInformation(
                "Progress-trackable export completed for job {JobId}, {RecordCount} records",
                jobId, result.RecordCount);
            
            return MethodResult<DataExportResult>.Success(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogInformation("Export was cancelled for job {JobId}", jobId);
            
            await progressTracker.UpdateProgressAsync(new ExportProgress
            {
                JobId = jobId,
                Status = "Cancelled",
                CompletionTime = DateTimeOffset.UtcNow
            }, CancellationToken.None);
            
            return MethodResult<DataExportResult>.Failure(
                AsyncEndpointError.FromCode("OPERATION_CANCELLED", "Export operation was cancelled")
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during progress-trackable export for job {JobId}", jobId);
            
            await progressTracker.UpdateProgressAsync(new ExportProgress
            {
                JobId = jobId,
                Status = "Failed",
                ErrorMessage = ex.Message,
                CompletionTime = DateTimeOffset.UtcNow
            }, CancellationToken.None);
            
            return MethodResult<DataExportResult>.Failure(ex);
        }
    }
    
    private static string GenerateFileName(DataExportRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var format = request.ExportFormat.ToLowerInvariant();
        var cleanTitle = new string(request.ReportTitle.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        
        return $"{cleanTitle}_{timestamp}.{format}";
    }
}
```

## CSV Export Specific Implementation

### CSV Export Handler with Streaming

```csharp
public class CsvExportHandler(
    ILogger<CsvExportHandler> logger,
    ICsvExportService csvExportService,
    IFileStorageService fileStorageService) 
    : IAsyncEndpointRequestHandler<DataExportRequest, DataExportResult>
{
    public async Task<MethodResult<DataExportResult>> HandleAsync(AsyncContext<DataExportRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var startTime = DateTimeOffset.UtcNow;
        
        logger.LogInformation("Starting CSV export for user {UserId}", request.UserId);
        
        try
        {
            // Validate CSV-specific options
            if (request.ExportFormat.ToLower() != "csv")
            {
                return MethodResult<DataExportResult>.Failure(
                    AsyncEndpointError.FromCode("INVALID_FORMAT", "This handler only supports CSV format")
                );
            }
            
            // Stream CSV data
            await using var csvStream = new MemoryStream();
            var recordCount = await csvExportService.ExportToStreamAsync(
                request, 
                csvStream, 
                token);
            
            // Upload to storage
            var fileId = await fileStorageService.StoreFileAsync(
                csvStream.ToArray(), 
                GenerateCsvFileName(request), 
                "text/csv", 
                token);
            
            var result = new DataExportResult
            {
                ExportFileId = fileId,
                ExportFileName = GenerateCsvFileName(request),
                ExportFormat = "csv",
                FileSize = csvStream.Length,
                ExportedAt = DateTime.UtcNow,
                RecordCount = recordCount,
                DownloadUrl = $"/api/exports/download/{fileId}",
                ProcessingTime = DateTimeOffset.UtcNow - startTime,
                Summary = $"Exported {recordCount} records as CSV"
            };
            
            logger.LogInformation(
                "CSV export completed for user {UserId}, {RecordCount} records, {FileSize} bytes",
                request.UserId, recordCount, csvStream.Length);
            
            return MethodResult<DataExportResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during CSV export for user {UserId}", request.UserId);
            return MethodResult<DataExportResult>.Failure(ex);
        }
    }
    
    private static string GenerateCsvFileName(DataExportRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"export_{timestamp}.csv";
    }
}
```

### CSV Export Service Implementation

```csharp
public class CsvExportService : ICsvExportService
{
    private readonly IDbConnection _connection;
    private readonly ILogger<CsvExportService> _logger;
    
    public async Task<int> ExportToStreamAsync(DataExportRequest request, Stream outputStream, CancellationToken token)
    {
        var totalRecordCount = 0;
        
        // Create CSV writer
        using var writer = new StreamWriter(outputStream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);
        var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
        
        // Write headers if requested
        if (request.IncludeHeaders && request.SelectedFields.Any())
        {
            foreach (var field in request.SelectedFields)
            {
                csvWriter.WriteField(field);
            }
            csvWriter.NextRecord();
        }
        
        // Execute query and write data
        var query = BuildQuery(request);
        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.CancellationToken = token;
        
        using var reader = await command.ExecuteReaderAsync(token);
        
        while (await reader.ReadAsync(token))
        {
            if (request.SelectedFields.Any())
            {
                foreach (var field in request.SelectedFields)
                {
                    var value = reader[field]?.ToString() ?? string.Empty;
                    csvWriter.WriteField(value);
                }
            }
            else
            {
                // If no specific fields selected, export all
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i)?.ToString() ?? string.Empty;
                    csvWriter.WriteField(value);
                }
            }
            
            csvWriter.NextRecord();
            totalRecordCount++;
            
            // Check for cancellation periodically
            token.ThrowIfCancellationRequested();
        }
        
        await csvWriter.FlushAsync();
        
        return totalRecordCount;
    }
    
    private static string BuildQuery(DataExportRequest request)
    {
        var whereClauses = new List<string>();
        
        if (request.StartDate.HasValue)
        {
            whereClauses.Add($"created_date >= '{request.StartDate:yyyy-MM-dd}'");
        }
        
        if (request.EndDate.HasValue)
        {
            whereClauses.Add($"created_date <= '{request.EndDate:yyyy-MM-dd}'");
        }
        
        foreach (var filter in request.Filters)
        {
            whereClauses.Add($"{filter.Key} = '{filter.Value}'");
        }
        
        var whereClause = whereClauses.Any() ? $"WHERE {string.Join(" AND ", whereClauses)}" : "";
        var fieldList = request.SelectedFields.Any() 
            ? string.Join(", ", request.SelectedFields) 
            : "*";
        
        return $"SELECT {fieldList} FROM data_table {whereClause}";
    }
}
```

## Excel Export Implementation

### Excel Export Handler

```csharp
public class ExcelExportHandler(
    ILogger<ExcelExportHandler> logger,
    IExcelExportService excelExportService,
    IFileStorageService fileStorageService) 
    : IAsyncEndpointRequestHandler<DataExportRequest, DataExportResult>
{
    public async Task<MethodResult<DataExportResult>> HandleAsync(AsyncContext<DataExportRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var startTime = DateTimeOffset.UtcNow;
        
        logger.LogInformation("Starting Excel export for user {UserId}", request.UserId);
        
        try
        {
            if (request.ExportFormat.ToLower() != "excel")
            {
                return MethodResult<DataExportResult>.Failure(
                    AsyncEndpointError.FromCode("INVALID_FORMAT", "This handler only supports Excel format")
                );
            }
            
            // Export to Excel
            var excelData = await excelExportService.ExportToExcelAsync(request, token);
            
            // Save Excel file
            var fileId = await fileStorageService.StoreFileAsync(
                excelData, 
                GenerateExcelFileName(request), 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                token);
            
            var result = new DataExportResult
            {
                ExportFileId = fileId,
                ExportFileName = GenerateExcelFileName(request),
                ExportFormat = "excel",
                FileSize = excelData.Length,
                ExportedAt = DateTime.UtcNow,
                RecordCount = excelData.RecordCount,
                DownloadUrl = $"/api/exports/download/{fileId}",
                ProcessingTime = DateTimeOffset.UtcNow - startTime,
                Summary = $"Exported {excelData.RecordCount} records as Excel"
            };
            
            logger.LogInformation(
                "Excel export completed for user {UserId}, {RecordCount} records",
                request.UserId, excelData.RecordCount);
            
            return MethodResult<DataExportResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Excel export for user {UserId}", request.UserId);
            return MethodResult<DataExportResult>.Failure(ex);
        }
    }
    
    private static string GenerateExcelFileName(DataExportRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var cleanTitle = new string(request.ReportTitle.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        
        return $"{cleanTitle}_{timestamp}.xlsx";
    }
}
```

### Excel Export Service Implementation

```csharp
public class ExcelExportService : IExcelExportService
{
    public async Task<ExcelExportResult> ExportToExcelAsync(DataExportRequest request, CancellationToken token)
    {
        using var package = new ExcelPackage();
        
        var worksheet = package.Workbook.Worksheets.Add("Export Data");
        
        // Add headers
        if (request.IncludeHeaders && request.SelectedFields.Any())
        {
            for (var i = 0; i < request.SelectedFields.Count; i++)
            {
                worksheet.Cells[1, i + 1].Value = request.SelectedFields[i];
            }
        }
        
        // Fetch and write data
        // This would typically involve fetching data from a database
        var data = await GetDataForExport(request, token);
        
        for (var row = 0; row < data.Count; row++)
        {
            for (var col = 0; col < request.SelectedFields.Count; col++)
            {
                var field = request.SelectedFields[col];
                var value = data[row].GetValueOrDefault(field, "");
                worksheet.Cells[row + (request.IncludeHeaders ? 2 : 1), col + 1].Value = value;
            }
        }
        
        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();
        
        // Return as byte array
        var bytes = await package.GetAsByteArrayAsync();
        
        return new ExcelExportResult
        {
            Data = bytes,
            RecordCount = data.Count
        };
    }
    
    private async Task<List<Dictionary<string, object>>> GetDataForExport(DataExportRequest request, CancellationToken token)
    {
        // Implementation to fetch data based on request parameters
        // This would typically query a database
        var results = new List<Dictionary<string, object>>();
        
        // Add data fetching logic here
        // For example:
        // var query = BuildQuery(request);
        // var data = await database.QueryAsync<Dictionary<string, object>>(query);
        
        return results;
    }
}
```

## Export Download Endpoint

### Download Controller/Endpoint

```csharp
app.MapGet("/api/exports/download/{fileId}", async (
    string fileId, 
    IFileStorageService fileStorageService, 
    HttpContext context) =>
{
    try
    {
        var fileData = await fileStorageService.GetFileAsync(fileId, context.RequestAborted);
        
        if (fileData == null)
        {
            return Results.NotFound("Export file not found");
        }
        
        // Set appropriate content type
        var contentType = GetContentTypeFromExtension(Path.GetExtension(fileData.FileName));
        
        // Set headers for download
        context.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileData.FileName}\"";
        context.Response.Headers.ContentType = contentType;
        context.Response.Headers.ContentLength = fileData.Data.Length;
        
        await context.Response.Body.WriteAsync(fileData.Data);
        
        return Results.Ok();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbidden();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error downloading file: {ex.Message}");
    }
});

static string GetContentTypeFromExtension(string extension)
{
    return extension?.ToLower() switch
    {
        ".csv" => "text/csv",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls" => "application/vnd.ms-excel",
        ".json" => "application/json",
        ".pdf" => "application/pdf",
        ".xml" => "application/xml",
        _ => "application/octet-stream"
    };
}
```

## Export Configuration and Setup

### Service Registration

```csharp
// In Program.cs
builder.Services.AddScoped<IDataExportService, DatabaseExportService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();
builder.Services.AddScoped<IFileStorageService, AzureBlobFileStorageService>();
builder.Services.AddScoped<IProgressTracker, RedisProgressTracker>();

// Register export handlers
builder.Services.AddAsyncEndpointHandler<ProgressTrackableExportHandler, DataExportRequest, DataExportResult>("DataExport");
builder.Services.AddAsyncEndpointHandler<CsvExportHandler, DataExportRequest, DataExportResult>("CsvExport");
builder.Services.AddAsyncEndpointHandler<ExcelExportHandler, DataExportRequest, DataExportResult>("ExcelExport");

// Configure AsyncEndpoints
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.WorkerConfigurations.MaximumQueueSize = 100;
    options.JobManagerConfiguration.DefaultMaxRetries = 2;
})
.AddAsyncEndpointsRedisStore(builder.Configuration.GetConnectionString("Redis"))
.AddAsyncEndpointsWorker();
```

### Endpoint Mappings

```csharp
// Export endpoints
app.MapAsyncPost<DataExportRequest>("DataExport", "/api/exports/create");
app.MapAsyncPost<DataExportRequest>("CsvExport", "/api/exports/csv");
app.MapAsyncPost<DataExportRequest>("ExcelExport", "/api/exports/excel");

// Export status endpoint
app.MapAsyncGetJobDetails("/api/exports/status/{jobId:guid}");

// Progress tracking endpoint
app.MapGet("/api/exports/progress/{jobId:guid}", async (string jobId, IProgressTracker progressTracker) =>
{
    var progress = await progressTracker.GetProgressAsync(jobId, CancellationToken.None);
    return progress is not null ? Results.Ok(progress) : Results.NotFound();
});

// Download endpoints are configured separately above
```

## Export Validation and Limits

### Export Validation Handler

```csharp
public class ValidatedExportHandler(
    ILogger<ValidatedExportHandler> logger,
    IDataExportService dataExportService,
    IFileStorageService fileStorageService,
    IExportValidator exportValidator) 
    : IAsyncEndpointRequestHandler<DataExportRequest, DataExportResult>
{
    private static readonly int MAX_RECORDS_PER_EXPORT = 100000;
    private static readonly int MAX_FILE_SIZE_MB = 100; // 100MB limit
    
    public async Task<MethodResult<DataExportResult>> HandleAsync(AsyncContext<DataExportRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            // Validate request parameters
            var validationErrors = await exportValidator.ValidateExportRequestAsync(request, token);
            
            if (validationErrors.Any())
            {
                logger.LogWarning(
                    "Export validation failed for user {UserId}: {Errors}", 
                    request.UserId, string.Join(", ", validationErrors));
                
                return MethodResult<DataExportResult>.Failure(
                    AsyncEndpointError.FromCode(
                        "VALIDATION_ERROR", 
                        $"Export validation failed: {string.Join(", ", validationErrors)}"
                    )
                );
            }
            
            // Check record count before proceeding
            var estimatedRecordCount = await dataExportService.GetEstimatedRecordCountAsync(request, token);
            
            if (estimatedRecordCount > MAX_RECORDS_PER_EXPORT)
            {
                return MethodResult<DataExportResult>.Failure(
                    AsyncEndpointError.FromCode(
                        "EXPORT_LIMIT_EXCEEDED", 
                        $"Export would exceed record limit of {MAX_RECORDS_PER_EXPORT} (estimated {estimatedRecordCount} records)"
                    )
                );
            }
            
            // Check if user has exceeded their export quota
            var userExportCount = await dataExportService.GetUserExportCountAsync(request.UserId, DateTime.UtcNow.AddDays(-1), token);
            
            if (userExportCount >= 10) // 10 exports per day limit
            {
                return MethodResult<DataExportResult>.Failure(
                    AsyncEndpointError.FromCode(
                        "QUOTA_EXCEEDED", 
                        "Daily export quota exceeded (10 exports per day)"
                    )
                );
            }
            
            // Proceed with export
            return await ExecuteExportAsync(request, token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during validated export for user {UserId}", request.UserId);
            return MethodResult<DataExportResult>.Failure(ex);
        }
    }
    
    private async Task<MethodResult<DataExportResult>> ExecuteExportAsync(DataExportRequest request, CancellationToken token)
    {
        var startTime = DateTimeOffset.UtcNow;
        
        var exportData = await dataExportService.ExportDataAsync(request, token);
        
        var exportFileId = await fileStorageService.StoreExportAsync(exportData, request.ExportFormat, token);
        
        var result = new DataExportResult
        {
            ExportFileId = exportFileId,
            ExportFileName = GenerateFileName(request),
            ExportFormat = request.ExportFormat,
            FileSize = exportData.Data.Length,
            ExportedAt = DateTime.UtcNow,
            RecordCount = exportData.RecordCount,
            DownloadUrl = $"/api/exports/download/{exportFileId}",
            ProcessingTime = DateTimeOffset.UtcNow - startTime,
            Summary = $"Exported {exportData.RecordCount} records in {request.ExportFormat} format"
        };
        
        // Log export for quota tracking
        await dataExportService.LogExportAsync(request.UserId, result, token);
        
        return MethodResult<DataExportResult>.Success(result);
    }
    
    private static string GenerateFileName(DataExportRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var format = request.ExportFormat.ToLowerInvariant();
        var cleanTitle = new string(request.ReportTitle.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        
        return $"{cleanTitle}_{timestamp}.{format}";
    }
}
```

The data export examples demonstrate how to implement various export types (CSV, Excel, etc.) with validation, progress tracking, and proper error handling. These patterns can be adapted for different data sources and export requirements while maintaining the AsyncEndpoints architecture for asynchronous processing.