using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Runtimes;

namespace CodeBeaker.Worker;

/// <summary>
/// 백그라운드 워커 - 큐에서 작업을 가져와 코드를 실행하고 결과를 저장
/// </summary>
public class Worker : BackgroundService
{
    private readonly IQueue _queue;
    private readonly IStorage _storage;
    private readonly ILogger<Worker> _logger;
    private readonly WorkerOptions _options;

    public Worker(
        IQueue queue,
        IStorage storage,
        ILogger<Worker> logger,
        IConfiguration configuration)
    {
        _queue = queue;
        _storage = storage;
        _logger = logger;
        _options = configuration.GetSection("Worker").Get<WorkerOptions>() ?? new WorkerOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CodeBeaker Worker starting...");
        _logger.LogInformation("Max concurrency: {MaxConcurrency}", _options.MaxConcurrency);
        _logger.LogInformation("Poll interval: {PollInterval}s", _options.PollIntervalSeconds);

        // SemaphoreSlim으로 동시 실행 제어
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 큐에서 작업 가져오기
                var task = await _queue.GetTaskAsync(timeout: _options.PollIntervalSeconds);

                if (task == null)
                {
                    // 작업이 없으면 짧은 대기 후 재시도
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                _logger.LogInformation(
                    "Task received: {ExecutionId}, Language: {Language}",
                    task.ExecutionId,
                    task.Language);

                // 동시성 제어: 슬롯이 사용 가능할 때까지 대기
                await semaphore.WaitAsync(stoppingToken);

                // 백그라운드에서 작업 처리 (Fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessTaskWithRetryAsync(task, stoppingToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("CodeBeaker Worker stopped");
    }

    /// <summary>
    /// 재시도 로직을 포함한 작업 처리
    /// </summary>
    private async Task ProcessTaskWithRetryAsync(TaskItem task, CancellationToken cancellationToken)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= _options.MaxRetries)
        {
            try
            {
                await ProcessTaskAsync(task, cancellationToken);
                return; // 성공
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;

                _logger.LogWarning(
                    ex,
                    "Execution failed (attempt {Retry}/{Max}): {ExecutionId}",
                    retryCount,
                    _options.MaxRetries,
                    task.ExecutionId);

                if (retryCount <= _options.MaxRetries)
                {
                    // 지수 백오프로 재시도
                    var delaySeconds = Math.Pow(2, retryCount);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
        }

        // 최종 실패 처리
        _logger.LogError(
            lastException,
            "Execution failed after {MaxRetries} retries: {ExecutionId}",
            _options.MaxRetries,
            task.ExecutionId);

        await _storage.UpdateStatusAsync(
            task.ExecutionId,
            "failed",
            exitCode: -1,
            errorType: "worker_error",
            cancellationToken: cancellationToken);

        await _queue.CompleteTaskAsync(task.ExecutionId);
    }

    /// <summary>
    /// 단일 작업 처리
    /// </summary>
    private async Task ProcessTaskAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. 상태를 'running'으로 업데이트
            await _storage.UpdateStatusAsync(
                task.ExecutionId,
                "running",
                cancellationToken: cancellationToken);

            _logger.LogInformation("Executing: {ExecutionId}", task.ExecutionId);

            // 2. 런타임 가져오기
            var runtime = RuntimeRegistry.Get(task.Language);

            // 3. 코드 실행
            var result = await runtime.ExecuteAsync(
                task.Code,
                task.Config,
                cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Execution completed: {ExecutionId}, ExitCode: {ExitCode}, Duration: {Duration}ms",
                task.ExecutionId,
                result.ExitCode,
                stopwatch.ElapsedMilliseconds);

            // 4. 결과 저장
            await _storage.SaveResultAsync(
                task.ExecutionId,
                result.Stdout,
                result.Stderr,
                result.ExitCode,
                stopwatch.ElapsedMilliseconds,
                result.Timeout,
                result.ErrorType,
                cancellationToken);

            // 5. 작업 완료 처리
            await _queue.CompleteTaskAsync(task.ExecutionId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Failed to process task: {ExecutionId}",
                task.ExecutionId);

            // 재시도를 위해 예외를 다시 던짐
            throw;
        }
    }
}

/// <summary>
/// Worker 설정 옵션
/// </summary>
public class WorkerOptions
{
    /// <summary>
    /// 최대 동시 실행 수
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// 큐 폴링 간격 (초)
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// 최대 재시도 횟수
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
