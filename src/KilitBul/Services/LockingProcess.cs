namespace KilitBul.Services;

/// <summary>Hedefi kilitleyen tek bir işlem ve kilitlediği dosyalar.</summary>
public sealed class LockingProcess
{
    public required int ProcessId { get; init; }
    public required string Name { get; init; }
    public string? ExecutablePath { get; init; }
    public DateTime StartTime { get; init; }
    public LockingAppType AppType { get; init; }
    public string ServiceName { get; init; } = "";

    /// <summary>İşlemin kilitlediği dosyalar (klasör taramasında birden fazla olabilir).</summary>
    public List<string> LockedPaths { get; } = new();

    /// <summary>İşlem hedef klasörden bir exe/dll yüklediği için mi listelendi?</summary>
    public bool LoadedModuleFromTarget { get; set; }

    public bool IsProtected => ProcessKiller.IsProtected(this);

    /// <summary>KilitBul'un kendi işlemi mi? (Restart Manager onu RmCritical olarak raporlar.)</summary>
    public bool IsSelf => ProcessId == Environment.ProcessId;

    public string TypeDescription => IsSelf ? "KilitBul (bu uygulama)" : AppType switch
    {
        LockingAppType.MainWindow => "Uygulama",
        LockingAppType.OtherWindow => "Pencere",
        LockingAppType.Service => string.IsNullOrEmpty(ServiceName) ? "Servis" : $"Servis ({ServiceName})",
        LockingAppType.Explorer => "Windows Gezgini",
        LockingAppType.Console => "Konsol",
        LockingAppType.Critical => "Kritik sistem işlemi",
        _ => LoadedModuleFromTarget ? "Modül yüklü" : "Bilinmiyor",
    };

    public string LockedPathsSummary => LockedPaths.Count switch
    {
        0 => LoadedModuleFromTarget ? "(klasörden exe/dll yüklemiş)" : "",
        1 => LockedPaths[0],
        _ => $"{LockedPaths[0]}  (+{LockedPaths.Count - 1} dosya daha)",
    };

    public string LockedPathsTooltip => string.Join(Environment.NewLine, LockedPaths.Take(50))
        + (LockedPaths.Count > 50 ? $"{Environment.NewLine}… ({LockedPaths.Count - 50} dosya daha)" : "");
}
