using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class TemperatureHistoryWriter : ITemperatureHistoryWriter, IAsyncDisposable
{
    private const string Header = "Timestamp,Index,Value";

    private readonly ConcurrentQueue<TemperatureSample> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _idleGate = new();
    private readonly string _rootDirectory;
    private readonly Task _workerTask;
    private TaskCompletionSource _idleSource = CreateCompletedIdleSource();
    private int _pendingCount;
    private volatile bool _disposed;

    public TemperatureHistoryWriter()
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "Temperature"))
    {
    }

    public TemperatureHistoryWriter(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public void Enqueue(TemperatureSample sample)
    {
        if (_disposed)
        {
            return;
        }

        lock (_idleGate)
        {
            if (_pendingCount == 0)
            {
                _idleSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _pendingCount++;
        }

        _queue.Enqueue(sample);
        _signal.Release();
    }

    public async Task FlushAsync()
    {
        while (true)
        {
            Task idleTask;
            lock (_idleGate)
            {
                if (_pendingCount == 0)
                {
                    return;
                }

                idleTask = _idleSource.Task;
            }

            await idleTask.ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await FlushAsync().ConfigureAwait(false);
        _signal.Release();
        await _workerTask.ConfigureAwait(false);
        _signal.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            await _signal.WaitAsync().ConfigureAwait(false);

            if (_disposed && _queue.IsEmpty)
            {
                return;
            }

            while (_queue.TryDequeue(out var sample))
            {
                await WriteSampleSafelyAsync(sample).ConfigureAwait(false);
                MarkSampleFinished();
            }
        }
    }

    private async Task WriteSampleSafelyAsync(TemperatureSample sample)
    {
        try
        {
            Directory.CreateDirectory(_rootDirectory);
            await WriteSampleAsync(sample).ConfigureAwait(false);
        }
        catch
        {
            // 历史写入失败不能阻塞采集线程，后续可接入日志系统记录异常。
        }
    }

    private void MarkSampleFinished()
    {
        lock (_idleGate)
        {
            _pendingCount--;
            if (_pendingCount == 0)
            {
                _idleSource.TrySetResult();
            }
        }
    }

    private async Task WriteSampleAsync(TemperatureSample sample)
    {
        var filePath = Path.Combine(_rootDirectory, $"{sample.Timestamp:yyyy-MM-dd}.csv");
        var shouldWriteHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

        await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        if (shouldWriteHeader)
        {
            await writer.WriteLineAsync(Header).ConfigureAwait(false);
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{sample.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{sample.Index},{sample.Value:F1}");
        await writer.WriteLineAsync(line).ConfigureAwait(false);
    }

    private static TaskCompletionSource CreateCompletedIdleSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
