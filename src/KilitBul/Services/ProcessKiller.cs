using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KilitBul.Services;

public enum KillResult
{
    Success,
    AlreadyExited,
    AccessDenied,
    Protected,
    NoWindow,
    Failed,
}

/// <summary>İşlemleri nazikçe kapatma ve zorla sonlandırma.</summary>
public static class ProcessKiller
{
    // Sonlandırılması sistemi çökertebilecek veya oturumu kapatacak işlemler.
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "system idle process", "registry", "smss.exe", "csrss.exe", "wininit.exe",
        "winlogon.exe", "services.exe", "lsass.exe", "lsaiso.exe", "fontdrvhost.exe", "dwm.exe",
        "memory compression", "secure system",
    };

    public static bool IsProtected(LockingProcess p)
    {
        if (p.ProcessId is 0 or 4 || p.AppType == LockingAppType.Critical)
            return true;
        var exeName = p.ExecutablePath is not null ? System.IO.Path.GetFileName(p.ExecutablePath) : p.Name;
        return ProtectedNames.Contains(exeName) || ProtectedNames.Contains(p.Name);
    }

    public static bool IsExplorer(LockingProcess p)
        => p.AppType == LockingAppType.Explorer
           || string.Equals(System.IO.Path.GetFileName(p.ExecutablePath ?? p.Name), "explorer.exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>Ana pencereye kapatma isteği gönderir (kaydedilmemiş belgeler için uygulama soru sorabilir).</summary>
    public static KillResult CloseGracefully(LockingProcess target)
    {
        if (target.IsProtected)
            return KillResult.Protected;

        using var proc = Open(target);
        if (proc is null)
            return KillResult.AlreadyExited;

        try
        {
            return proc.CloseMainWindow() ? KillResult.Success : KillResult.NoWindow;
        }
        catch (InvalidOperationException)
        {
            return KillResult.AlreadyExited;
        }
    }

    /// <summary>İşlemi zorla sonlandırır ve kapanmasını kısa süre bekler.</summary>
    public static KillResult Kill(LockingProcess target, bool entireTree = false)
    {
        if (target.IsProtected)
            return KillResult.Protected;

        using var proc = Open(target);
        if (proc is null)
            return KillResult.AlreadyExited;

        try
        {
            proc.Kill(entireTree);
            proc.WaitForExit(3000);
            return KillResult.Success;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            return KillResult.AccessDenied;
        }
        catch (InvalidOperationException)
        {
            return KillResult.AlreadyExited;
        }
        catch (Win32Exception)
        {
            return KillResult.Failed;
        }
    }

    /// <summary>
    /// PID ile işlemi açar; PID başka bir işleme yeniden atanmışsa (başlangıç zamanı farklı) null döner.
    /// </summary>
    private static Process? Open(LockingProcess target)
    {
        Process proc;
        try
        {
            proc = Process.GetProcessById(target.ProcessId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (target.StartTime != DateTime.MinValue)
        {
            try
            {
                if (Math.Abs((proc.StartTime - target.StartTime).TotalSeconds) > 1)
                {
                    proc.Dispose();
                    return null;
                }
            }
            catch
            {
                // Başlangıç zamanı okunamadı (yetki); PID'e güvenerek devam et.
            }
        }
        return proc;
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>
    /// İşlemin exe yolunu döndürür. Process.MainModule'ün aksine yükseltilmiş işlemlerde de
    /// çoğunlukla çalışır (PROCESS_QUERY_LIMITED_INFORMATION yeterli).
    /// </summary>
    public static string? TryGetExecutablePath(int pid)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == IntPtr.Zero)
            return null;
        try
        {
            var sb = new StringBuilder(32768);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(h);
        }
    }
}
