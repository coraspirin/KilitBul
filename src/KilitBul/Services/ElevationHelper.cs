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
