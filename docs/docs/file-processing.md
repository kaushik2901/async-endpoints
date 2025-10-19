---
sidebar_position: 1
title: File Processing
---

# File Processing

This page provides comprehensive examples and patterns for processing large files asynchronously using AsyncEndpoints, including progress tracking, error handling, and memory-efficient processing techniques.

## Overview

File processing is a common use case for asynchronous processing, where large files need to be processed without blocking the main application thread. AsyncEndpoints provides the infrastructure for handling these operations efficiently.

## Basic File Processing Example

### Request Model for File Processing

```csharp
public class FileProcessingRequest
{
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileId { get; set; } = string.Empty; // Reference to stored file
    public string ProcessingOptions { get; set; } = string.Empty; // JSON string for processing options
    public string UserId { get; set; } = string.Empty; // For user tracking
}
```

### Response Model for File Processing

```csharp
public class FileProcessingResult
{
    public string ProcessedFileName { get; set; } = string.Empty;
    public string ProcessedFileId { get; set; } = string.Empty;
    public long ProcessedFileSize { get; set; }
    public string ProcessingSummary { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public int ProcessedRecordCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}
```

### File Processing Handler

```csharp
public class FileProcessingHandler(
    ILogger<FileProcessingHandler> logger,
    IFileStorageService fileStorageService,
    IFileProcessor fileProcessor) 
    : IAsyncEndpointRequestHandler<FileProcessingRequest, FileProcessingResult>
{
    public async Task<MethodResult<FileProcessingResult>> HandleAsync(AsyncContext<FileProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        logger.LogInformation(
            "Starting file processing for file {FileId} (size: {FileSize}) by user {UserId}",
            request.FileId, request.FileSize, request.UserId);
        
        try
        {
            // Validate file exists in storage
            var fileExists = await fileStorageService.FileExistsAsync(request.FileId, token);
            if (!fileExists)
            {
                logger.LogError("File {FileId} does not exist in storage", request.FileId);
                return MethodResult<FileProcessingResult>.Failure(
                    AsyncEndpointError.FromCode("FILE_NOT_FOUND", $"File with ID {request.FileId} not found")
                );
            }
            
            // Get the file stream
            var fileStream = await fileStorageService.GetFileStreamAsync(request.FileId, token);
            
            // Process the file
            var processingResult = await fileProcessor.ProcessFileAsync(
                fileStream, 
                request.ProcessingOptions, 
                token);
            
            // Save the processed result
            var processedFileId = await fileStorageService.StoreProcessedFileAsync(processingResult, token);
            
            var result = new FileProcessingResult
            {
                ProcessedFileName = $"processed_{request.FileName}",
                ProcessedFileId = processedFileId,
                ProcessedFileSize = processingResult.Size,
                ProcessingSummary = processingResult.Summary,
                ProcessedAt = DateTime.UtcNow,
                ProcessedRecordCount = processingResult.ProcessedRecordCount,
                Warnings = processingResult.Warnings
            };
            
            logger.LogInformation(
                "Successfully processed file {FileId}, processed {RecordCount} records, output size: {OutputSize}",
                request.FileId, result.ProcessedRecordCount, result.ProcessedFileSize);
            
            return MethodResult<FileProcessingResult>.Success(result);
        }
        catch (FileProcessingException ex)
        {
            logger.LogError(ex, "File processing failed for file {FileId}", request.FileId);
            return MethodResult<FileProcessingResult>.Failure(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during file processing for file {FileId}", request.FileId);
            return MethodResult<FileProcessingResult>.Failure(ex);
        }
    }
}
```

## Large File Processing with Progress Tracking

### Request Model with Progress Tracking

```csharp
public class LargeFileProcessingRequest
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ProcessingType { get; set; } = string.Empty; // "image", "document", "data", etc.
    public Dictionary<string, object> ProcessingOptions { get; set; } = new();
    public bool TrackProgress { get; set; } = true;
    public string CallbackUrl { get; set; } = string.Empty; // Optional webhook for progress updates
}
```

### Progress Tracking Handler

```csharp
public class LargeFileProcessingHandler(
    ILogger<LargeFileProcessingHandler> logger,
    IFileStorageService fileStorageService,
    ILargeFileProcessor largeFileProcessor,
    IProgressTracker progressTracker) 
    : IAsyncEndpointRequestHandler<LargeFileProcessingRequest, FileProcessingResult>
{
    public async Task<MethodResult<FileProcessingResult>> HandleAsync(AsyncContext<LargeFileProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var jobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString());
        
        logger.LogInformation(
            "Starting large file processing for file {FileId} (size: {FileSize}) with job {JobId}",
            request.FileId, request.FileSize, jobId);
        
        try
        {
            // Initialize progress tracking
            var progress = new ProcessingProgress
            {
                JobId = jobId,
                TotalBytes = request.FileSize,
                ProcessedBytes = 0,
                Status = "Starting",
                StartTime = DateTime.UtcNow
            };
            
            await progressTracker.UpdateProgressAsync(progress, token);
            
            // Get file stream
            var fileStream = await fileStorageService.GetFileStreamAsync(request.FileId, token);
            
            // Process file with progress updates
            var progressCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var progressToken = progressCts.Token;
            
            var progressTask = Task.Run(async () =>
            {
                while (!progressToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, progressToken); // Update every 5 seconds
                    
                    if (largeFileProcessor.CurrentProgress != null)
                    {
                        await progressTracker.UpdateProgressAsync(largeFileProcessor.CurrentProgress, progressToken);
                        
                        // Send webhook if configured
                        if (!string.IsNullOrEmpty(request.CallbackUrl))
                        {
                            await SendProgressWebhookAsync(
                                request.CallbackUrl, 
                                largeFileProcessor.CurrentProgress, 
                                progressToken);
                        }
                    }
                }
            }, progressToken);
            
            // Process the file
            var processingResult = await largeFileProcessor.ProcessFileWithProgressAsync(
                fileStream, 
                request.ProcessingType, 
                request.ProcessingOptions,
                progress => 
                {
                    progress.JobId = jobId; // Ensure correct job ID
                    return progressTracker.UpdateProgressAsync(progress, CancellationToken.None);
                },
                token);
            
            // Stop progress tracking
            progressCts.Cancel();
            try { await progressTask; } catch (OperationCanceledException) { }
            
            // Final progress update
            var finalProgress = new ProcessingProgress
            {
                JobId = jobId,
                TotalBytes = request.FileSize,
                ProcessedBytes = request.FileSize, // Complete
                Status = "Completed",
                CompletionTime = DateTime.UtcNow,
                Percentage = 100
            };
            await progressTracker.UpdateProgressAsync(finalProgress, token);
            
            // Store processed result
            var processedFileId = await fileStorageService.StoreProcessedFileAsync(processingResult, token);
            
            var result = new FileProcessingResult
            {
                ProcessedFileName = $"processed_{request.FileName}",
                ProcessedFileId = processedFileId,
                ProcessedFileSize = processingResult.Size,
                ProcessingSummary = processingResult.Summary,
                ProcessedAt = DateTime.UtcNow,
                ProcessedRecordCount = processingResult.ProcessedRecordCount,
                Warnings = processingResult.Warnings
            };
            
            logger.LogInformation(
                "Successfully completed large file processing for file {FileId}, job {JobId}",
                request.FileId, jobId);
            
            return MethodResult<FileProcessingResult>.Success(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogInformation("File processing was cancelled for job {JobId}", jobId);
            
            // Update progress to cancelled
            await progressTracker.UpdateProgressAsync(new ProcessingProgress
            {
                JobId = jobId,
                Status = "Cancelled",
                CompletionTime = DateTime.UtcNow
            }, CancellationToken.None);
            
            return MethodResult<FileProcessingResult>.Failure(
                AsyncEndpointError.FromCode("OPERATION_CANCELLED", "Operation was cancelled")
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during large file processing for job {JobId}", jobId);
            
            // Update progress to error
            await progressTracker.UpdateProgressAsync(new ProcessingProgress
            {
                JobId = jobId,
                Status = "Error",
                ErrorMessage = ex.Message
            }, CancellationToken.None);
            
            return MethodResult<FileProcessingResult>.Failure(ex);
        }
    }
    
    private async Task SendProgressWebhookAsync(string callbackUrl, ProcessingProgress progress, CancellationToken token)
    {
        try
        {
            using var httpClient = new HttpClient();
            var content = JsonSerializer.Serialize(progress);
            var response = await httpClient.PostAsync(
                callbackUrl, 
                new StringContent(content, Encoding.UTF8, "application/json"), 
                token);
            
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Webhook call failed with status {StatusCode} for job {JobId}", 
                    response.StatusCode, progress.JobId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending progress webhook for job {JobId}", progress.JobId);
        }
    }
}
```

## Memory-Efficient File Processing

### Chunked Processing Handler

```csharp
public class ChunkedFileProcessingHandler(
    ILogger<ChunkedFileProcessingHandler> logger,
    IFileStorageService fileStorageService,
    IChunkProcessor chunkProcessor) 
    : IAsyncEndpointRequestHandler<FileProcessingRequest, FileProcessingResult>
{
    private const int CHUNK_SIZE = 1024 * 1024; // 1MB chunks
    
    public async Task<MethodResult<FileProcessingResult>> HandleAsync(AsyncContext<FileProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            logger.LogInformation("Starting chunked file processing for file {FileId}", request.FileId);
            
            using var fileStream = await fileStorageService.GetFileStreamAsync(request.FileId, token);
            
            var totalBytes = request.FileSize;
            var processedBytes = 0L;
            var processedRecords = 0;
            var warnings = new List<string>();
            
            // Process file in chunks
            var buffer = new byte[CHUNK_SIZE];
            int bytesRead;
            
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                // Process the chunk
                var chunkResult = await chunkProcessor.ProcessChunkAsync(
                    new ArraySegment<byte>(buffer, 0, bytesRead), 
                    token);
                
                processedRecords += chunkResult.ProcessedRecordCount;
                processedBytes += bytesRead;
                
                if (chunkResult.Warnings.Any())
                {
                    warnings.AddRange(chunkResult.Warnings);
                }
                
                // Check for cancellation periodically
                token.ThrowIfCancellationRequested();
            }
            
            // Combine results
            var result = new FileProcessingResult
            {
                ProcessedFileName = $"processed_{request.FileName}",
                ProcessedFileId = request.FileId, // Same ID for processed file
                ProcessedFileSize = processedBytes,
                ProcessingSummary = $"Processed {processedRecords} records from {request.FileName}",
                ProcessedAt = DateTime.UtcNow,
                ProcessedRecordCount = processedRecords,
                Warnings = warnings
            };
            
            logger.LogInformation(
                "Completed chunked processing of {ProcessedBytes} bytes, {ProcessedRecords} records",
                processedBytes, processedRecords);
            
            return MethodResult<FileProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during chunked file processing for file {FileId}", request.FileId);
            return MethodResult<FileProcessingResult>.Failure(ex);
        }
    }
}
```

## File Processing with Validation

### Validated File Processing Handler

```csharp
public class ValidatedFileProcessingHandler(
    ILogger<ValidatedFileProcessingHandler> logger,
    IFileStorageService fileStorageService,
    IFileProcessor fileProcessor,
    IFileValidator fileValidator) 
    : IAsyncEndpointRequestHandler<FileProcessingRequest, FileProcessingResult>
{
    public async Task<MethodResult<FileProcessingResult>> HandleAsync(AsyncContext<FileProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            logger.LogInformation("Starting validated file processing for file {FileId}", request.FileId);
            
            // Validate file exists and is accessible
            var fileExists = await fileStorageService.FileExistsAsync(request.FileId, token);
            if (!fileExists)
            {
                return MethodResult<FileProcessingResult>.Failure(
                    AsyncEndpointError.FromCode("FILE_NOT_FOUND", $"File {request.FileId} not found")
                );
            }
            
            // Get file stream for validation
            using var validationStream = await fileStorageService.GetFileStreamAsync(request.FileId, token);
            
            // Validate file format and content
            var validationResult = await fileValidator.ValidateFileAsync(validationStream, request.FileType, token);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("File validation failed for {FileId}: {Errors}", 
                    request.FileId, string.Join(", ", validationResult.Errors));
                
                return MethodResult<FileProcessingResult>.Failure(
                    AsyncEndpointError.FromCode("FILE_VALIDATION_FAILED", 
                        $"File validation failed: {string.Join(", ", validationResult.Errors)}")
                );
            }
            
            // Reset stream position for actual processing
            validationStream.Seek(0, SeekOrigin.Begin);
            
            // Process the validated file
            var processingResult = await fileProcessor.ProcessFileAsync(
                validationStream, 
                request.ProcessingOptions, 
                token);
            
            // Store processed result
            var processedFileId = await fileStorageService.StoreProcessedFileAsync(processingResult, token);
            
            var result = new FileProcessingResult
            {
                ProcessedFileName = $"processed_{request.FileName}",
                ProcessedFileId = processedFileId,
                ProcessedFileSize = processingResult.Size,
                ProcessingSummary = processingResult.Summary,
                ProcessedAt = DateTime.UtcNow,
                ProcessedRecordCount = processingResult.ProcessedRecordCount,
                Warnings = processingResult.Warnings
            };
            
            logger.LogInformation("Successfully processed validated file {FileId}", request.FileId);
            
            return MethodResult<FileProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during validated file processing for {FileId}", request.FileId);
            return MethodResult<FileProcessingResult>.Failure(ex);
        }
    }
}
```

## File Processing Configuration

### Service Registration

```csharp
// In Program.cs
builder.Services.AddScoped<IFileStorageService, AzureBlobFileStorageService>();
builder.Services.AddScoped<IFileProcessor, DocumentProcessorService>();
builder.Services.AddScoped<IChunkProcessor, CsvChunkProcessor>();
builder.Services.AddScoped<IFileValidator, FileValidationService>();
builder.Services.AddScoped<IProgressTracker, RedisProgressTracker>();

// Register the handlers
builder.Services.AddAsyncEndpointHandler<LargeFileProcessingHandler, LargeFileProcessingRequest, FileProcessingResult>("LargeFileProcess");
builder.Services.AddAsyncEndpointHandler<ChunkedFileProcessingHandler, FileProcessingRequest, FileProcessingResult>("ChunkedFileProcess");
builder.Services.AddAsyncEndpointHandler<ValidatedFileProcessingHandler, FileProcessingRequest, FileProcessingResult>("ValidatedFileProcess");

// Configure AsyncEndpoints
builder.Services.AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(builder.Configuration.GetConnectionString("Redis"))
    .AddAsyncEndpointsWorker();
```

### Endpoint Mapping

```csharp
// Map endpoints for different processing types
app.MapAsyncPost<LargeFileProcessingRequest>("LargeFileProcess", "/api/files/process-large");
app.MapAsyncPost<FileProcessingRequest>("ChunkedFileProcess", "/api/files/process-chunked");
app.MapAsyncPost<FileProcessingRequest>("ValidatedFileProcess", "/api/files/process-validated");

// Job status endpoint
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");

// Progress tracking endpoint (if needed separately)
app.MapGet("/files/progress/{jobId:guid}", async (string jobId, IProgressTracker progressTracker) =>
{
    var progress = await progressTracker.GetProgressAsync(jobId, CancellationToken.None);
    return progress is not null ? Results.Ok(progress) : Results.NotFound();
});
```

## Error Handling and Resilience

### File Processing with Retry Logic

```csharp
public class ResilientFileProcessingHandler(
    ILogger<ResilientFileProcessingHandler> logger,
    IFileStorageService fileStorageService,
    IFileProcessor fileProcessor,
    IResiliencePipelineProvider<string> resilienceProvider) 
    : IAsyncEndpointRequestHandler<FileProcessingRequest, FileProcessingResult>
{
    public async Task<MethodResult<FileProcessingResult>> HandleAsync(AsyncContext<FileProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var pipeline = resilienceProvider.GetPipeline("file-processing");
        
        try
        {
            var result = await pipeline.ExecuteAsync(async (cancellationToken) =>
            {
                logger.LogInformation("Processing file {FileId} with resilience", request.FileId);
                
                using var fileStream = await fileStorageService.GetFileStreamAsync(request.FileId, cancellationToken);
                var processingResult = await fileProcessor.ProcessFileAsync(
                    fileStream, 
                    request.ProcessingOptions, 
                    cancellationToken);
                
                var processedFileId = await fileStorageService.StoreProcessedFileAsync(processingResult, cancellationToken);
                
                return new FileProcessingResult
                {
                    ProcessedFileName = $"processed_{request.FileName}",
                    ProcessedFileId = processedFileId,
                    ProcessedFileSize = processingResult.Size,
                    ProcessingSummary = processingResult.Summary,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessedRecordCount = processingResult.ProcessedRecordCount,
                    Warnings = processingResult.Warnings
                };
            }, token);
            
            return MethodResult<FileProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resilient processing failed for file {FileId}", request.FileId);
            return MethodResult<FileProcessingResult>.Failure(ex);
        }
    }
}
```

The file processing examples demonstrate how to handle various scenarios from simple file processing to complex, memory-efficient chunked processing with progress tracking and validation. These patterns can be adapted for different types of files and processing requirements.