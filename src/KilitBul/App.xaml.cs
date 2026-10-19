using System.IO;
using System.Windows;
using KilitBul.Services;

namespace KilitBul;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args;
        if (args.Length > 0)
        {
            switch (args[0].ToLowerInvariant())
            {
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

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (args.Length > 0)
        {
            var target = NormalizeArgument(args[0]);
            await window.ViewModel.SetTargetAndScanAsync(target);
        }
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

    private void RunAndExit(Action action, string successMessage)
    {
        try
        {
            action();
            MessageBox.Show(successMessage, "KilitBul", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("İşlem başarısız: " + ex.Message, "KilitBul", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Shutdown();
    }
}
