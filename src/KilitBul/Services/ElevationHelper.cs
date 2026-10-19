using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace KilitBul.Services;

public static class ElevationHelper
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// KilitBul'un yönetici olarak çalışan ikinci bir kopyasında yalnızca bu işlemi sonlandırır
    /// (<c>--kill &lt;pid&gt; &lt;başlangıç&gt;</c>). Kullanıcı UAC'yi iptal ederse null döner.
    /// </summary>
    public static async Task<KillResult?> KillElevatedAsync(LockingProcess target)
    {
        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
        psi.ArgumentList.Add("--kill");
        psi.ArgumentList.Add(target.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(target.StartTime.ToBinary().ToString(System.Globalization.CultureInfo.InvariantCulture));

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
                return KillResult.Failed;
            await proc.WaitForExitAsync();
            return Enum.IsDefined(typeof(KillResult), proc.ExitCode) ? (KillResult)proc.ExitCode : KillResult.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return null;
        }
    }

    /// <summary>
    /// Uygulamayı aynı argümanlarla yönetici olarak yeniden başlatır.
    /// Kullanıcı UAC penceresini iptal ederse false döner.
    /// </summary>
    public static bool RestartElevated(params string[] args)
    {
        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        try
        {
            Process.Start(psi);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return false;
        }
    }
}
