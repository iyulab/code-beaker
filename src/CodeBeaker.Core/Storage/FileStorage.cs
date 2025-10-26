using System.Text.Json;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Storage;

/// <summary>
/// 파일시스템 기반 상태 저장소
/// </summary>
public sealed class FileStorage : IStorage
{
    private readonly string _baseDir;
    private readonly string _metricsDir;
    private static readonly SemaphoreSlim _metricsLock = new(1, 1);

    public FileStorage(string baseDir = "data/executions")
    {
        _baseDir = baseDir;
        _metricsDir = Path.Combine(Path.GetDirectoryName(_baseDir) ?? "data", "metrics");

        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(_metricsDir);
    }

    public async Task UpdateStatusAsync(
        string executionId,
        string status,
        int exitCode = 0,
        long durationMs = 0,
        bool timeout = false,
        string? errorType = null,
        CancellationToken cancellationToken = default)
    {
        var execDir = EnsureExecutionDir(executionId);
        var statusFile = Path.Combine(execDir, "status.json");

        var result = new ExecutionResult
        {
            ExecutionId = executionId,
            Status = status,
            ExitCode = exitCode,
            DurationMs = durationMs,
            Timeout = timeout,
            ErrorType = errorType,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = status == "completed" || status == "failed"
                ? DateTime.UtcNow
                : null
        };

        // Atomic write
        var tempFile = statusFile + ".tmp";
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(tempFile, json, cancellationToken);

        if (File.Exists(statusFile))
        {
            File.Delete(statusFile);
        }
        File.Move(tempFile, statusFile);
    }

    public async Task SaveResultAsync(
        string executionId,
        string stdout,
        string stderr,
        int exitCode,
        long durationMs,
        bool timeout = false,
        string? errorType = null,
        CancellationToken cancellationToken = default)
    {
        var execDir = EnsureExecutionDir(executionId);

        // Save stdout and stderr as separate files
        await File.WriteAllTextAsync(
            Path.Combine(execDir, "stdout.txt"),
            stdout,
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(execDir, "stderr.txt"),
            stderr,
            cancellationToken);

        // Update status
        var status = exitCode == 0 && !timeout ? "completed" : "failed";
        await UpdateStatusAsync(
            executionId,
            status,
            exitCode,
            durationMs,
            timeout,
            errorType,
            cancellationToken);

        // Update metrics
        await UpdateMetricsAsync(status, durationMs);
    }

    public async Task<ExecutionResult?> GetResultAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var execDir = Path.Combine(_baseDir, executionId);
        var statusFile = Path.Combine(execDir, "status.json");

        if (!File.Exists(statusFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(statusFile, cancellationToken);
        var result = JsonSerializer.Deserialize<ExecutionResult>(json);

        if (result == null)
        {
            return null;
        }

        // Load stdout and stderr
        var stdoutFile = Path.Combine(execDir, "stdout.txt");
        var stderrFile = Path.Combine(execDir, "stderr.txt");

        if (File.Exists(stdoutFile))
        {
            result.Stdout = await File.ReadAllTextAsync(stdoutFile, cancellationToken);
        }

        if (File.Exists(stderrFile))
        {
            result.Stderr = await File.ReadAllTextAsync(stderrFile, cancellationToken);
        }

        return result;
    }

    public async Task<ExecutionResult?> GetStatusAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var execDir = Path.Combine(_baseDir, executionId);
        var statusFile = Path.Combine(execDir, "status.json");

        if (!File.Exists(statusFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(statusFile, cancellationToken);
        return JsonSerializer.Deserialize<ExecutionResult>(json);
    }

    private string EnsureExecutionDir(string executionId)
    {
        var execDir = Path.Combine(_baseDir, executionId);
        Directory.CreateDirectory(execDir);
        return execDir;
    }

    private async Task UpdateMetricsAsync(string status, long durationMs)
    {
        var countersFile = Path.Combine(_metricsDir, "counters.json");

        await _metricsLock.WaitAsync();
        try
        {
            Dictionary<string, object> counters;

            if (File.Exists(countersFile))
            {
                var json = await File.ReadAllTextAsync(countersFile);
                counters = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                    ?? new Dictionary<string, object>();
            }
            else
            {
                counters = new Dictionary<string, object>();
            }

            // Update counters
            var totalProcessed = counters.ContainsKey("total_processed")
                ? GetLongValue(counters["total_processed"])
                : 0L;
            counters["total_processed"] = totalProcessed + 1;

            var totalDuration = counters.ContainsKey("total_duration_ms")
                ? GetLongValue(counters["total_duration_ms"])
                : 0L;
            counters["total_duration_ms"] = totalDuration + durationMs;

            var statusKey = $"status_{status}";
            var statusCount = counters.ContainsKey(statusKey)
                ? GetLongValue(counters[statusKey])
                : 0L;
            counters[statusKey] = statusCount + 1;

            // Atomic write
            var tempFile = countersFile + ".tmp";
            var updatedJson = JsonSerializer.Serialize(counters, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(tempFile, updatedJson);

            if (File.Exists(countersFile))
            {
                File.Delete(countersFile);
            }
            File.Move(tempFile, countersFile);
        }
        finally
        {
            _metricsLock.Release();
        }
    }

    private static long GetLongValue(object value)
    {
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.GetInt64();
        }
        return Convert.ToInt64(value);
    }
}
