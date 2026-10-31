using System.Diagnostics;
using System.Runtime.InteropServices;
using GSYNC.Core.Updates;
using Microsoft.Win32;

return await Updater.RunAsync(args);

internal static partial class Updater
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static async Task<int> RunAsync(string[] args)
    {
        string? dataRoot = null, installRoot = null, session = null, version = null;
        var safeToStart = false;
        try
        {
            if (args.Length == 2 && args[0] == "--recover")
            {
                session = Path.GetFullPath(args[1]);
                var journal = UpdateTransaction.ReadJournal(session);
                dataRoot = journal.DataRoot; installRoot = journal.InstallRoot; version = journal.Version;
                safeToStart = journal.Committed;
                using (await AcquireAppLockAsync(dataRoot))
                {
                    // A different recovery process may have completed while this one waited for the lock.
                    journal = UpdateTransaction.ReadJournal(session);
                    if (!journal.Committed) UpdateTransaction.Rollback(session, journal);
                    safeToStart = true;
                    UpdateResultStore.Write(dataRoot, new(journal.Committed ? "installed" : "rolled-back", version,
                        journal.Committed ? "The update was installed." : "An interrupted update was rolled back.", DateTimeOffset.UtcNow, session));
                    RemoveRecovery(session);
                }
                StartApp(installRoot, dataRoot);
                return 0;
            }
            if (args.Length != 7 || args[0] != "--apply" || !int.TryParse(args[2], out var pid) || !long.TryParse(args[3], out var startTicks))
                throw new ArgumentException("Invalid update request.");
            session = Path.GetFullPath(args[1]);
            installRoot = Path.GetFullPath(args[4]);
            dataRoot = Path.GetFullPath(args[5]);
            version = args[6];
            UpdateTransaction.ValidateLocations(installRoot, dataRoot, session);
            await WaitForOriginalProcessAsync(pid, startTicks, Path.Combine(installRoot, "GSYNC.App.exe"));
            safeToStart = true;
            using (await AcquireAppLockAsync(dataRoot))
            {
                var journal = await UpdateTransaction.PrepareAsync(installRoot, dataRoot, session, version);
                RegisterRecovery(session);
                try
                {
                    safeToStart = false;
                    UpdateTransaction.Apply(session, journal);
                    await ProbeAsync(journal, session);
                    UpdateTransaction.Commit(session, journal);
                    safeToStart = true;
                    UpdateResultStore.Write(dataRoot, new("installed", version, "The signed update passed its startup check.", DateTimeOffset.UtcNow));
                    RemoveRecovery(session);
                }
                catch (Exception installError)
                {
                    if (UpdateTransaction.ReadJournal(session).Committed)
                    {
                        safeToStart = true;
                        throw new IOException("The update was installed, but finalizing its status failed.", installError);
                    }
                    try
                    {
                        UpdateTransaction.Rollback(session, journal);
                    }
                    catch (Exception rollbackError)
                    {
                        UpdateResultStore.Write(dataRoot, new("recovery-required", version, rollbackError.Message, DateTimeOffset.UtcNow, session));
                        throw new AggregateException("Update failed and recovery needs attention. The backup has been preserved at " + session, installError, rollbackError);
                    }
                    safeToStart = true;
                    UpdateResultStore.Write(dataRoot, new("rolled-back", version, installError.Message, DateTimeOffset.UtcNow, session));
                    RemoveRecovery(session);
                }
            }
            StartApp(installRoot, dataRoot);
            return 0;
        }
        catch (Exception ex)
        {
            if (dataRoot is not null && version is not null)
            {
                try
                {
                    if (UpdateResultStore.Read(dataRoot)?.State != "recovery-required")
                        UpdateResultStore.Write(dataRoot, new("error", version, ex.Message, DateTimeOffset.UtcNow, session));
                }
                catch { }
            }
            if (safeToStart && installRoot is not null && dataRoot is not null) { try { StartApp(installRoot, dataRoot); } catch { } }
            ShowError("GSYNC update could not finish.\n\n" + ex.Message + (session is null ? "" : "\n\nRecovery files: " + session));
            return 1;
        }
    }

    private static async Task WaitForOriginalProcessAsync(int pid, long startTicks, string expectedExe)
    {
        Process process;
        try { process = Process.GetProcessById(pid); }
        catch (ArgumentException) { return; }
        using (process)
        {
            if (process.StartTime.ToUniversalTime().Ticks != startTicks ||
                !string.Equals(process.MainModule?.FileName, expectedExe, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The original application process no longer matches the update request.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await process.WaitForExitAsync(timeout.Token);
        }
    }

    private static async Task<FileStream> AcquireAppLockAsync(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "app.lock");
        for (var attempt = 0; attempt < 120; attempt++)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 119) { await Task.Delay(250); }
        }
        throw new IOException("GSYNC is still running; the update was not applied.");
    }

    private static async Task ProbeAsync(UpdateJournal journal, string session)
    {
        var probeRoot = Path.Combine(session, "probe-data");
        Directory.CreateDirectory(probeRoot);
        foreach (var name in new[] { "data.db", "data.db-wal", "data.db-shm", "ui-settings.json", "sync-targets.json" })
        {
            var source = Path.Combine(journal.DataRoot, name);
            if (!File.Exists(source)) continue;
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("Application data contains an unsupported link.");
            File.Copy(source, Path.Combine(probeRoot, name));
        }
        var token = Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Path.Combine(journal.InstallRoot, "GSYNC.App.exe")) { UseShellExecute = false, WorkingDirectory = journal.InstallRoot };
        start.Environment["GSYNC_DATA_ROOT"] = probeRoot;
        start.Environment["GSYNC_UPDATE_PROBE_TOKEN"] = token;
        start.Environment.Remove("GSYNC_RESTART_HIDDEN");
        start.Environment.Remove("GSYNC_FAIL_PAGE_INIT");
        using var process = Process.Start(start) ?? throw new IOException("Could not start the updated program for verification.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(); await process.WaitForExitAsync(); } catch (InvalidOperationException) { }
            throw new IOException("The updated program did not complete its startup check.");
        }
        var health = Path.Combine(probeRoot, "update-health-" + token);
        if (process.ExitCode != 0 || !File.Exists(health) || new FileInfo(health).Length > 256 ||
            !string.Equals((await File.ReadAllTextAsync(health)).Split('+')[0], journal.Version.Split('+')[0], StringComparison.Ordinal))
            throw new IOException("The updated program failed its startup check.");
    }

    private static void RegisterRecovery(string session)
    {
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot locate the update recovery program.");
        if (!string.Equals(Path.GetDirectoryName(executable), session, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The recovery program is outside the update session.");
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true) ?? throw new IOException("Cannot register update recovery.");
        key.SetValue("GSYNC.UpdateRecovery." + Path.GetFileName(session), $"\"{executable}\" --recover \"{session}\"", RegistryValueKind.String);
    }

    private static void RemoveRecovery(string session)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.DeleteValue("GSYNC.UpdateRecovery." + Path.GetFileName(session), false);
    }

    private static void StartApp(string installRoot, string dataRoot)
    {
        var start = new ProcessStartInfo(Path.Combine(installRoot, "GSYNC.App.exe")) { UseShellExecute = false, WorkingDirectory = installRoot };
        start.Environment["GSYNC_DATA_ROOT"] = dataRoot;
        start.Environment.Remove("GSYNC_UPDATE_PROBE_TOKEN");
        _ = Process.Start(start) ?? throw new IOException("The update finished, but GSYNC could not be started. Open it manually.");
    }

    private static void ShowError(string message)
    {
        if (OperatingSystem.IsWindows()) MessageBoxW(IntPtr.Zero, message, "GSYNC Update", 0x10);
        else Console.Error.WriteLine(message);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
