using Ypopup.Core.Models;

namespace Ypopup.Core.Protocol;

public sealed class ProgressThresholdReporter
{
    private const long ReportByteThreshold = 1L * 1024 * 1024;
    private const int ReportPercentThreshold = 5;

    private long _lastReportedBytes;
    private int _lastReportedPercent;

    public ProgressThresholdReporter(long totalBytes)
    {
        TotalBytes = totalBytes;
    }

    public long TotalBytes { get; }

    public bool ShouldReport(long transferredBytes)
    {
        if (TotalBytes <= 0)
        {
            return false;
        }

        var percent = (int)((double)transferredBytes / TotalBytes * 100);
        if (percent - _lastReportedPercent >= ReportPercentThreshold
            && percent is > 0 and < 100)
        {
            return true;
        }

        var bytesSinceLast = transferredBytes - _lastReportedBytes;
        return bytesSinceLast >= ReportByteThreshold
               || transferredBytes >= TotalBytes;
    }

    public TransferProgress Build(long transferredBytes, bool isSending, string? fileName)
    {
        _lastReportedBytes = transferredBytes;
        _lastReportedPercent = (int)((double)transferredBytes / TotalBytes * 100);
        return new TransferProgress(transferredBytes, TotalBytes, isSending, fileName);
    }

    public void ReportIfReady(
        IProgress<TransferProgress>? progress,
        long transferredBytes,
        bool isSending,
        string? fileName)
    {
        if (progress is null)
        {
            return;
        }

        if (!ShouldReport(transferredBytes))
        {
            return;
        }

        progress.Report(Build(transferredBytes, isSending, fileName));
    }
}