using System.Text.Json;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Queue;

/// <summary>
/// 파일시스템 기반 작업 큐
/// </summary>
public sealed class FileQueue : IQueue
{
    private readonly string _baseDir;
    private readonly string _pendingDir;
    private readonly string _processingDir;
    private readonly string _dataRoot;

    public FileQueue(string baseDir = "data/queue")
    {
        _baseDir = baseDir;
        _pendingDir = Path.Combine(_baseDir, "pending");
        _processingDir = Path.Combine(_baseDir, "processing");
        _dataRoot = Path.GetDirectoryName(_baseDir) ?? "data";

        // 디렉토리 생성
        Directory.CreateDirectory(_pendingDir);
        Directory.CreateDirectory(_processingDir);
    }

    public async Task<string> SubmitTaskAsync(
        string code,
        string language,
        ExecutionConfig config,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffffff");
        var fileName = $"{timestamp}_{executionId}.json";

        var task = new TaskItem
        {
            ExecutionId = executionId,
            Code = code,
            Language = language,
            Config = config,
            CreatedAt = DateTime.UtcNow,
            FileName = fileName
        };

        // Atomic write: temp file → rename
        var tempFile = Path.Combine(_pendingDir, $".tmp_{fileName}");
        var targetFile = Path.Combine(_pendingDir, fileName);

        var json = JsonSerializer.Serialize(task, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(tempFile, json, cancellationToken);

        // Atomic rename
        if (File.Exists(targetFile))
        {
            File.Delete(targetFile);
        }
        File.Move(tempFile, targetFile);

        return executionId;
    }

    public async Task<TaskItem?> GetTaskAsync(
        int timeout = 1,
        CancellationToken cancellationToken = default)
    {
        var endTime = DateTime.UtcNow.AddSeconds(timeout);

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            // Get oldest task (sorted by timestamp in filename)
            var taskFiles = Directory.GetFiles(_pendingDir, "*.json")
                .OrderBy(f => f)
                .ToArray();

            if (taskFiles.Length > 0)
            {
                // Try each file in order until we successfully claim one
                foreach (var taskFile in taskFiles)
                {
                    // Use unique processing filename to avoid collisions
                    var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
                    var fileName = Path.GetFileName(taskFile);
                    var processingFile = Path.Combine(_processingDir, $"{uniqueId}_{fileName}");

                    try
                    {
                        // Atomic move - will fail if source doesn't exist
                        File.Move(taskFile, processingFile);

                        var json = await File.ReadAllTextAsync(processingFile, cancellationToken);
                        var task = JsonSerializer.Deserialize<TaskItem>(json);

                        // Store processing filename for cleanup
                        if (task != null)
                        {
                            task.FileName = Path.GetFileName(processingFile);
                        }

                        return task;
                    }
                    catch (FileNotFoundException)
                    {
                        // File was moved by another worker, try next file
                        continue;
                    }
                    catch (IOException)
                    {
                        // File moved by another worker or other IO error, try next file
                        continue;
                    }
                }
            }

            // Wait before retrying
            await Task.Delay(100, cancellationToken);
        }

        return null;
    }

    public Task CompleteTaskAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        // Find and remove processing file
        var files = Directory.GetFiles(_processingDir, $"*{executionId}.json");
        foreach (var file in files)
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }
}
