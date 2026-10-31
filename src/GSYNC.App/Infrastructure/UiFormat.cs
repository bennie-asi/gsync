using GSYNC.Core.Models;

namespace GSYNC.App.Infrastructure;

/// <summary>
/// Shared presentation formatting for sync domain values (times, sizes, status badges).
/// </summary>
public static class UiFormat
{
    public static string RecordError(SyncRecord record, bool isChinese)
    {
        var error = record.ErrorMessage;
        if (string.IsNullOrWhiteSpace(error)) return string.Empty;
        if (isChinese && error == "Remote saves differ or contain additional files. Compare the versions and choose which to keep before uploading.")
            return "远端存档有差异或多余文件。请比较存档，选择保留的版本；旧目录文件可选择移除。";
        return error;
    }

    public static string RecordSummary(SyncRecord record, bool isChinese)
    {
        if (!isChinese || string.IsNullOrWhiteSpace(record.Summary)) return record.Summary ?? "--";
        if (record.Summary.StartsWith("Uploaded ", StringComparison.Ordinal)) return $"已上传 {record.ProcessedFiles} 个文件。";
        if (record.Summary.StartsWith("Downloaded ", StringComparison.Ordinal)) return $"已下载 {record.ProcessedFiles} 个文件。";
        if (record.Summary.StartsWith("Resolved ", StringComparison.Ordinal) && record.Summary.EndsWith(" conflict file decisions.", StringComparison.Ordinal))
            return $"已处理 {record.ProcessedFiles} 个文件。";
        if (record.Summary.StartsWith("Compared ", StringComparison.Ordinal)) return $"已比较 {record.ProcessedFiles} 个文件。";
        return record.Summary;
    }

    public static string RecordMessage(SyncRecord record, bool isChinese) =>
        string.IsNullOrWhiteSpace(record.ErrorMessage) ? RecordSummary(record, isChinese) : RecordError(record, isChinese);

    public static string RelativeTime(DateTimeOffset? timestampUtc, bool isChinese)
    {
        if (timestampUtc is null)
        {
            return isChinese ? "从未" : "Never";
        }

        var elapsed = DateTimeOffset.UtcNow - timestampUtc.Value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalMinutes < 1)
        {
            return isChinese ? "刚刚" : "Just now";
        }

        if (elapsed.TotalHours < 1)
        {
            var minutes = (int)elapsed.TotalMinutes;
            return isChinese ? $"{minutes} 分钟前" : $"{minutes}m ago";
        }

        if (elapsed.TotalDays < 1)
        {
            var hours = (int)elapsed.TotalHours;
            return isChinese ? $"{hours} 小时前" : $"{hours}h ago";
        }

        if (elapsed.TotalDays < 2)
        {
            return isChinese ? "昨天" : "Yesterday";
        }

        if (elapsed.TotalDays < 30)
        {
            var days = (int)elapsed.TotalDays;
            return isChinese ? $"{days} 天前" : $"{days} days ago";
        }

        return timestampUtc.Value.ToLocalTime().ToString("yyyy-MM-dd");
    }

    public static string TimeOfRecord(DateTimeOffset timestampUtc, bool isChinese)
    {
        var local = timestampUtc.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        if (local.Date == today)
        {
            return isChinese ? $"今天 {local:HH:mm}" : $"Today {local:HH:mm}";
        }

        if (local.Date == today.AddDays(-1))
        {
            return isChinese ? $"昨天 {local:HH:mm}" : $"Yesterday {local:HH:mm}";
        }

        return local.ToString("MM-dd HH:mm");
    }

    public static string Bytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024):0.#} MB";
        }

        return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";
    }

    public static string DirectionText(SyncDirection direction, bool isChinese)
    {
        return direction switch
        {
            SyncDirection.Upload => isChinese ? "上传" : "Upload",
            SyncDirection.Download => isChinese ? "下载" : "Download",
            SyncDirection.Compare => isChinese ? "比较" : "Compare",
            SyncDirection.ResolveConflict => isChinese ? "处理存档" : "Resolve saves",
            _ => direction.ToString(),
        };
    }

    public static bool IsSnapshotRestore(SyncRecord record) => record.Direction == SyncDirection.Download &&
        (record.Summary?.StartsWith("Snapshot restore ", StringComparison.Ordinal) == true ||
         (record.Summary?.StartsWith("Restored ", StringComparison.Ordinal) == true &&
          record.Summary.Contains(" paths from snapshot ", StringComparison.Ordinal)));

    public static string DirectionText(SyncRecord record, bool isChinese) => IsSnapshotRestore(record)
        ? (isChinese ? "恢复快照" : "Restore snapshot")
        : DirectionText(record.Direction, isChinese);

    public static string RestoreConfirmationText(string gameName, string snapshotName, string timestamp, int fileCount, bool isChinese)
        => isChinese
            ? $"游戏：{gameName}\n快照：{snapshotName}\n时间：{timestamp}\n文件数：{fileCount}\n\n恢复会替换受影响的本地存档文件。当前文件会先保存为新的快照。"
            : $"Game: {gameName}\nSnapshot: {snapshotName}\nTime: {timestamp}\nFiles: {fileCount}\n\nRestoring replaces affected local save files. Their current contents are backed up as a new snapshot first.";

    public static string DownloadConfirmationText(bool autoSnapshot, bool isChinese) => (autoSnapshot, isChinese) switch
    {
        (true, true) => "远端数据将覆盖本地文件。覆盖前会自动创建本地快照。是否继续？",
        (true, false) => "Remote data will overwrite local files. A local snapshot will be created first. Continue?",
        (false, true) => "远端数据将覆盖本地文件。自动快照已关闭，覆盖前不会创建快照。是否继续？",
        _ => "Remote data will overwrite local files. Automatic snapshots are disabled, so no snapshot will be created first. Continue?",
    };

    public static string StatusText(SyncJobStatus status, bool isChinese)
    {
        return status switch
        {
            SyncJobStatus.Completed => isChinese ? "成功" : "Success",
            SyncJobStatus.Failed => isChinese ? "失败" : "Failed",
            SyncJobStatus.Cancelled => isChinese ? "已取消" : "Cancelled",
            _ => status.ToString(),
        };
    }

    public static string StatusVariant(SyncJobStatus status)
    {
        return status switch
        {
            SyncJobStatus.Completed => "synced",
            SyncJobStatus.Failed => "conflict",
            SyncJobStatus.Cancelled => "disabled",
            _ => "pending",
        };
    }
}
