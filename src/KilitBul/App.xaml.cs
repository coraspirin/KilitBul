using System.Globalization;
using System.IO;
using System.Windows;
using KilitBul.Services;

namespace KilitBul;

public partial class App : Application
{
    /// <summary>Başlangıçta okunan Windows uygulama teması.</summary>
    public static bool IsDarkTheme { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args;
        if (args.Length > 0)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--kill":
                    // Yükseltilmiş kopya: yalnızca verilen işlemi sonlandırıp sonucu çıkış koduyla bildirir.
                    Shutdown((int)KillFromArguments(args));
                    return;

                case "--register":
                case "/register":
                    RunAndExit(() => ShellRegistration.Register(Environment.ProcessPath!),
                        "Kilit Bul sağ tık menüsüne eklendi.\nWindows 11'de \"Daha fazla seçenek göster\" altında görünür.");
                    return;

                case "--unregister":
                case "/unregister":
                    RunAndExit(() => ShellRegistration.Unregister(), "Kilit Bul sağ tık menüsünden kaldırıldı.");
                    return;
            }
        }

        IsDarkTheme = WindowEffects.IsDarkTheme();
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(IsDarkTheme ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative),
        });

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        var updateCheck = window.ViewModel.CheckForUpdatesAsync();
        if (args.Length > 0)
        {
            var target = NormalizeArgument(args[0]);
            await window.ViewModel.SetTargetAndScanAsync(target);
        }
        await updateCheck;
    }

    /// <summary>
    /// Explorer sürücüler için "%1" yerine `"C:\"` verir; komut satırı ayrıştırıcısı \" dizisini tırnak
    /// olarak yorumladığından argüman `C:"` gelir. Bunu düzeltir.
    /// </summary>
    internal static string NormalizeArgument(string arg)
    {
        var s = arg.Trim().Trim('"');
        if (s.Length == 2 && s[1] == ':')
            s += Path.DirectorySeparatorChar;
        return s;
    }

    /// <summary><c>--kill &lt;pid&gt; &lt;başlangıç zamanı (DateTime.ToBinary)&gt;</c></summary>
    internal static KillResult KillFromArguments(string[] args)
    {
        if (args.Length < 3
            || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
            || !long.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
            return KillResult.Failed;

        var exe = ProcessKiller.TryGetExecutablePath(pid);
        var target = new LockingProcess
        {
            ProcessId = pid,
            Name = exe is null ? "" : Path.GetFileName(exe),
            ExecutablePath = exe,
            StartTime = DateTime.FromBinary(start),
        };
        return ProcessKiller.Kill(target);
    }

    private void RunAndExit(Action action, string successMessage)
    {
        try
        {
            action();
            MessageBox.Show(successMessage, "Kilit Bul", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("İşlem başarısız: " + ex.Message, "Kilit Bul", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Shutdown();
    }
}
