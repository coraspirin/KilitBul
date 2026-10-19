using Microsoft.Win32;

namespace KilitBul.Services;

/// <summary>
/// Explorer'ın klasik sağ tık menüsüne (HKCU, yönetici gerekmez) "Kim kullanıyor?" komutunu ekler/kaldırır.
/// Windows 11'de "Daha fazla seçenek göster" altında görünür.
/// </summary>
public static class ShellRegistration
{
    public const string DefaultVerbName = "KilitBul";
    public const string MenuText = "Kim kullanıyor? (KilitBul)";

    // Dosyalar, klasörler ve sürücüler.
    private static readonly string[] TargetClasses = { "*", "Directory", "Drive" };

    private static string ClassesRoot => @"Software\Classes";

    public static void Register(string exePath, string verbName = DefaultVerbName)
    {
        foreach (var cls in TargetClasses)
        {
            using var verb = Registry.CurrentUser.CreateSubKey($@"{ClassesRoot}\{cls}\shell\{verbName}");
            verb.SetValue("", MenuText);
            verb.SetValue("Icon", $"\"{exePath}\",0");

            using var cmd = verb.CreateSubKey("command");
            cmd.SetValue("", $"\"{exePath}\" \"%1\"");
        }
    }

    public static void Unregister(string verbName = DefaultVerbName)
    {
        foreach (var cls in TargetClasses)
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{cls}\shell\{verbName}", throwOnMissingSubKey: false);
    }

    /// <summary>Kayıtlı mı ve komut bu exe'yi mi gösteriyor?</summary>
    public static bool IsRegistered(string exePath, string verbName = DefaultVerbName)
    {
        using var cmd = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\*\shell\{verbName}\command");
        var value = cmd?.GetValue("") as string;
        return value is not null && value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? GetCommand(string cls, string verbName)
    {
        using var cmd = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{cls}\shell\{verbName}\command");
        return cmd?.GetValue("") as string;
    }
}
