using Microsoft.Win32;

namespace KilitBul.Services;

/// <summary>
/// Explorer'ın klasik sağ tık menüsüne (HKCU, yönetici gerekmez) "Kilit Bul" komutunu ekler/kaldırır.
/// Kurulum (installer/KilitBul.nsi) aynı anahtarları tüm kullanıcılar için HKLM'ye yazar; bu sınıf kurulumsuz
/// kullanım (--register) içindir. Windows 11'de "Daha fazla seçenek göster" altında görünür.
/// </summary>
public static class ShellRegistration
{
    public const string DefaultVerbName = "KilitBul";
    public const string MenuText = "Kilit Bul";

    // Dosyalar ve klasörler.
    private static readonly string[] TargetClasses = { "*", "Directory" };

    // Önceki sürümlerin kayıt yaptığı ama artık kullanılmayan sınıflar; kaldırırken temizlenir.
    private static readonly string[] LegacyClasses = { "Drive" };

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
        foreach (var cls in TargetClasses.Concat(LegacyClasses))
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesRoot}\{cls}\shell\{verbName}", throwOnMissingSubKey: false);
    }

    /// <summary>Tüm hedef sınıflarda (dosya, klasör) kayıtlı mı ve komut bu exe'yi mi gösteriyor?</summary>
    public static bool IsRegistered(string exePath, string verbName = DefaultVerbName)
        => TargetClasses.All(cls =>
            GetCommand(cls, verbName) is { } value && value.Contains(exePath, StringComparison.OrdinalIgnoreCase));

    internal static string? GetCommand(string cls, string verbName)
    {
        using var cmd = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\{cls}\shell\{verbName}\command");
        return cmd?.GetValue("") as string;
    }
}
