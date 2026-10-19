using System.Diagnostics;
using System.IO;

namespace KilitBul.Services;

public sealed record ScanProgress(string Message, int FilesScanned);

/// <summary>Dosya veya klasörü kilitleyen işlemleri bulur.</summary>
public static class LockFinder
{
    public static Task<IReadOnlyList<LockingProcess>> FindAsync(
        string targetPath, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Find(targetPath, progress, ct), ct);

    public static IReadOnlyList<LockingProcess> Find(
        string targetPath, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        var path = Path.GetFullPath(targetPath);
        var byPid = new Dictionary<int, LockingProcess>();

        if (File.Exists(path))
        {
            progress?.Report(new ScanProgress("Dosya kontrol ediliyor…", 1));
            Attribute(new[] { path }, byPid, ct);
        }
        else if (Directory.Exists(path))
        {
            ScanDirectory(path, byPid, progress, ct);
            progress?.Report(new ScanProgress("Yüklü modüller kontrol ediliyor…", 0));
            FindModuleUsers(path, byPid, ct);
        }
        else
        {
            throw new FileNotFoundException("Dosya veya klasör bulunamadı.", path);
        }

        return byPid.Values
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.ProcessId)
            .ToList();
    }

    /// <summary>Yol bir sürücü/paylaşım kökü mü (ör. <c>C:\</c>, <c>\\sunucu\paylaşım</c>)?</summary>
    public static bool IsRootPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            return root is not null && string.Equals(
                full.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static void ScanDirectory(string root, Dictionary<int, LockingProcess> byPid,
        IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint, // junction/symlink döngülerinden kaçın
            ReturnSpecialDirectories = false,
        };

        var batch = new List<string>(RestartManager.BatchSize);
        int scanned = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", options))
        {
            ct.ThrowIfCancellationRequested();
            batch.Add(file);
            scanned++;
            if (batch.Count == RestartManager.BatchSize)
            {
                progress?.Report(new ScanProgress($"Taranan dosya: {scanned:N0}", scanned));
                Attribute(batch, byPid, ct);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            progress?.Report(new ScanProgress($"Taranan dosya: {scanned:N0}", scanned));
            Attribute(batch, byPid, ct);
        }
    }

    /// <summary>
    /// Bir dosya grubunu Restart Manager ile sorgular. Restart Manager hangi dosyanın hangi işlem tarafından
    /// kilitlendiğini söylemediği için, sonuç dönen grupları ikiye bölerek tek tek dosyalara kadar iner.
    /// </summary>
    internal static void Attribute(IReadOnlyList<string> files, Dictionary<int, LockingProcess> byPid, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<RmLockInfo> lockers;
        bool mustSplit = false;
        try
        {
            lockers = RestartManager.GetLockingProcesses(files);
        }
        catch (System.ComponentModel.Win32Exception) when (files.Count > 1)
        {
            // Gruptaki bir dosya sorun çıkarıyorsa (ör. geçersiz yol) grubu bölerek devam et.
            lockers = Array.Empty<RmLockInfo>();
            mustSplit = true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return; // tek dosya sorgulanamıyor; atla
        }

        if (lockers.Count == 0 && !mustSplit)
            return;

        if (files.Count == 1)
        {
            foreach (var info in lockers)
                GetOrAdd(byPid, info).LockedPaths.Add(files[0]);
            return;
        }

        int half = files.Count / 2;
        Attribute(Slice(files, 0, half), byPid, ct);
        Attribute(Slice(files, half, files.Count - half), byPid, ct);
    }

    private static IReadOnlyList<string> Slice(IReadOnlyList<string> list, int start, int count)
    {
        var arr = new string[count];
        for (int i = 0; i < count; i++)
            arr[i] = list[start + i];
        return arr;
    }

    private static LockingProcess GetOrAdd(Dictionary<int, LockingProcess> byPid, RmLockInfo info)
    {
        if (byPid.TryGetValue(info.ProcessId, out var existing))
            return existing;

        var exe = ProcessKiller.TryGetExecutablePath(info.ProcessId);
        var name = !string.IsNullOrWhiteSpace(info.AppName) ? info.AppName
            : exe is not null ? Path.GetFileName(exe)
            : $"PID {info.ProcessId}";

        var lp = new LockingProcess
        {
            ProcessId = info.ProcessId,
            Name = name,
            ExecutablePath = exe,
            StartTime = info.StartTime,
            AppType = info.AppType,
            ServiceName = info.ServiceName,
        };
        byPid[info.ProcessId] = lp;
        return lp;
    }

    /// <summary>Hedef klasörün altından exe veya dll yüklemiş işlemleri ekler.</summary>
    private static void FindModuleUsers(string root, Dictionary<int, LockingProcess> byPid, CancellationToken ct)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                ct.ThrowIfCancellationRequested();
                if (proc.Id is 0 or 4 || proc.Id == Environment.ProcessId)
                    continue;

                List<string> hits;
                try
                {
                    hits = proc.Modules.Cast<ProcessModule>()
                        .Select(m => m.FileName)
                        .Where(f => !string.IsNullOrEmpty(f) && f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                catch
                {
                    continue; // erişim reddedildi / işlem kapandı / 32-64 bit uyumsuzluğu
                }
                if (hits.Count == 0)
                    continue;

                if (!byPid.TryGetValue(proc.Id, out var lp))
                {
                    DateTime start = DateTime.MinValue;
                    try { start = proc.StartTime; } catch { }
                    lp = new LockingProcess
                    {
                        ProcessId = proc.Id,
                        Name = proc.ProcessName + ".exe",
                        ExecutablePath = ProcessKiller.TryGetExecutablePath(proc.Id),
                        StartTime = start,
                        AppType = LockingAppType.Unknown,
                    };
                    byPid[proc.Id] = lp;
                }
                lp.LoadedModuleFromTarget = true;
                foreach (var h in hits)
                    if (!lp.LockedPaths.Contains(h, StringComparer.OrdinalIgnoreCase))
                        lp.LockedPaths.Add(h);
            }
        }
    }
}
