using GSYNC.Core.Models;

namespace GSYNC.App.Infrastructure;

/// <summary>
/// Shared presentation formatting for sync domain values (times, sizes, status badges).
/// </summary>
public static class UiFormat
{
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
            _ => direction.ToString(),
        };
    }

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
