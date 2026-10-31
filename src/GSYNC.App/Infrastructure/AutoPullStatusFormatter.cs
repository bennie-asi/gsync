using GSYNC.Core.Services.Sync;

namespace GSYNC.App.Infrastructure;

internal static class AutoPullStatusFormatter
{
    public static string Format(AutoPullGameStatus status, bool chinese)
    {
        var label = status.State switch
        {
            "ready" => chinese ? "已确认共同版本，等待远端变化" : "Common version confirmed; waiting for remote changes",
            "baseline-required" => chinese ? "需要先在游戏详情比较并确认共同版本" : "Compare and confirm a common version in game details first",
            "queued" => chinese ? "自动拉取已排队或正在执行" : "Automatic pull queued or running",
            "busy" => chinese ? "该游戏有其他同步任务，稍后再检查" : "Another sync is running for this game; will check later",
            "no-games" => chinese ? "游戏库暂无游戏" : "No games in the library",
            "review" => chinese ? "文件或配置有变化，请在游戏详情比较并确认" : "Files or configuration changed; compare and confirm in game details",
            "error" => chinese ? "检查失败，请查看同步历史或日志" : "Check failed; see sync history or logs",
            _ => chinese ? "等待检查" : "Waiting to check",
        };
        return status.State == "error" && !string.IsNullOrWhiteSpace(status.Detail) ? $"{label}: {status.Detail}" : label;
    }
}
