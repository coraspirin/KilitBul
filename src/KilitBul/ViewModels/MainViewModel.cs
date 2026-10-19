using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KilitBul.Services;
using Microsoft.Win32;

namespace KilitBul.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<ProcessItemViewModel> Processes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTarget))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(DeleteTargetCommand))]
    private string? _targetPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(CloseSelectedCommand), nameof(KillSelectedCommand),
        nameof(DeleteTargetCommand), nameof(CancelScanCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Bir dosya veya klasör seçin ya da pencereye sürükleyin.";

    [ObservableProperty]
    private bool _scanCompleted;

    [ObservableProperty]
    private bool _isContextMenuRegistered;

    public bool HasTarget => !string.IsNullOrWhiteSpace(TargetPath);
    public bool IsIdle => !IsBusy;
    public bool IsAdministrator { get; } = ElevationHelper.IsAdministrator();
    public bool ShowElevateButton => !IsAdministrator;
    public bool IsTargetFree => ScanCompleted && Processes.Count == 0;

    public string WindowTitle => IsAdministrator ? "KilitBul (Yönetici)" : "KilitBul";

    private static string ExePath => Environment.ProcessPath!;

    public MainViewModel()
    {
        IsContextMenuRegistered = SafeIsRegistered();
        Processes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsTargetFree));
            CloseSelectedCommand.NotifyCanExecuteChanged();
            KillSelectedCommand.NotifyCanExecuteChanged();
        };
    }

    public async Task SetTargetAndScanAsync(string path)
    {
        TargetPath = path;
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetPath))
            return;

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsBusy = true;
        ScanCompleted = false;
        Processes.Clear();
        StatusText = "Taranıyor…";

        var progress = new Progress<ScanProgress>(p => StatusText = p.Message);
        try
        {
            var found = await LockFinder.FindAsync(TargetPath, progress, ct);
            foreach (var p in found)
                Processes.Add(new ProcessItemViewModel(p));

            ScanCompleted = true;
            StatusText = found.Count == 0
                ? "Bu öğeyi kullanan bir işlem bulunamadı. Silebilirsiniz."
                : $"{found.Count} işlem bu öğeyi kullanıyor.";
            if (found.Count > 0 && !IsAdministrator)
                StatusText += " Bazı işlemler listede görünmüyorsa yönetici olarak deneyin.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Tarama iptal edildi.";
        }
        catch (Exception ex)
        {
            StatusText = "Hata: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsTargetFree));
        }
    }

    private bool CanRefresh() => HasTarget && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancelScan))]
    private void CancelScan() => _scanCts?.Cancel();

    private bool CanCancelScan() => IsBusy;

    [RelayCommand]
    private async Task BrowseFileAsync()
    {
        var dlg = new OpenFileDialog { Title = "Kilitli dosyayı seçin", CheckFileExists = true };
        if (dlg.ShowDialog() == true)
            await SetTargetAndScanAsync(dlg.FileName);
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var dlg = new OpenFolderDialog { Title = "Kilitli klasörü seçin" };
        if (dlg.ShowDialog() == true)
            await SetTargetAndScanAsync(dlg.FolderName);
    }

    private IReadOnlyList<ProcessItemViewModel> SelectedOrAll(System.Collections.IList? selected)
    {
        var items = selected?.OfType<ProcessItemViewModel>().ToList() ?? new List<ProcessItemViewModel>();
        return items.Count > 0 ? items : Processes.ToList();
    }

    [RelayCommand(CanExecute = nameof(CanActOnProcesses))]
    private async Task CloseSelectedAsync(System.Collections.IList? selected)
    {
        var targets = SelectedOrAll(selected).Where(p => p.CanTerminate).ToList();
        if (targets.Count == 0)
            return;

        int noWindow = 0;
        foreach (var t in targets)
        {
            var r = ProcessKiller.CloseGracefully(t.Model);
            t.Status = Describe(r);
            if (r == KillResult.NoWindow)
                noWindow++;
        }

        StatusText = "Kapatma isteği gönderildi, bekleniyor…";
        await Task.Delay(1500);
        await RefreshAsync();
        if (noWindow > 0 && Processes.Count > 0)
            StatusText += $" {noWindow} işlemin penceresi yok; 'Zorla sonlandır' kullanın.";
    }

    [RelayCommand(CanExecute = nameof(CanActOnProcesses))]
    private async Task KillSelectedAsync(System.Collections.IList? selected)
    {
        var all = SelectedOrAll(selected);
        var targets = all.Where(p => p.CanTerminate).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("Seçili işlemler korumalı sistem işlemleri olduğu için sonlandırılamaz.", "KilitBul",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = string.Join(Environment.NewLine, targets.Select(t => $"  • {t.Name} (PID {t.ProcessId})"));
        var msg = $"Aşağıdaki işlemler zorla sonlandırılacak. Kaydedilmemiş veriler kaybolabilir.{Environment.NewLine}{Environment.NewLine}{names}";
        if (targets.Any(t => ProcessKiller.IsExplorer(t.Model)))
            msg += $"{Environment.NewLine}{Environment.NewLine}Uyarı: Windows Gezgini (explorer.exe) sonlandırılırsa görev çubuğu ve masaüstü kısa süreliğine kaybolur; Windows genellikle onu otomatik yeniden başlatır.";
        msg += $"{Environment.NewLine}{Environment.NewLine}Devam edilsin mi?";

        if (MessageBox.Show(msg, "Zorla sonlandır", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        var results = await Task.Run(() => targets.Select(t => (t, r: ProcessKiller.Kill(t.Model))).ToList());
        IsBusy = false;

        foreach (var (t, r) in results)
            t.Status = Describe(r);

        int denied = results.Count(x => x.r == KillResult.AccessDenied);
        await RefreshAsync();

        if (denied > 0)
        {
            StatusText = $"{denied} işlem için erişim reddedildi.";
            if (!IsAdministrator &&
                MessageBox.Show($"{denied} işlem sonlandırılamadı (erişim reddedildi). KilitBul yönetici olarak yeniden açılsın mı?",
                    "Yetki gerekli", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RestartElevated();
            }
        }
    }

    private bool CanActOnProcesses(System.Collections.IList? _) => !IsBusy && Processes.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteTarget))]
    private async Task DeleteTargetAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetPath))
            return;

        var path = TargetPath;
        if (MessageBox.Show($"\"{path}\" Geri Dönüşüm Kutusu'na taşınsın mı?", "Sil",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            if (Directory.Exists(path))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

            StatusText = "Silindi (Geri Dönüşüm Kutusu'na taşındı).";
            Processes.Clear();
            ScanCompleted = false;
            TargetPath = null;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Silme iptal edildi.";
        }
        catch (Exception ex)
        {
            StatusText = "Silinemedi: " + ex.Message;
            await RefreshAsync();
        }
    }

    private bool CanDeleteTarget() => HasTarget && !IsBusy;

    [RelayCommand]
    private void RestartElevated()
    {
        var args = string.IsNullOrWhiteSpace(TargetPath) ? Array.Empty<string>() : new[] { TargetPath };
        if (ElevationHelper.RestartElevated(args))
            Application.Current.Shutdown();
    }

    [RelayCommand]
    private void ToggleContextMenu()
    {
        try
        {
            if (IsContextMenuRegistered)
                ShellRegistration.Unregister();
            else
                ShellRegistration.Register(ExePath);
            IsContextMenuRegistered = SafeIsRegistered();
            StatusText = IsContextMenuRegistered
                ? "Sağ tık menüsüne eklendi. Windows 11'de \"Daha fazla seçenek göster\" altında görünür."
                : "Sağ tık menüsünden kaldırıldı.";
        }
        catch (Exception ex)
        {
            StatusText = "Kayıt defteri hatası: " + ex.Message;
        }
    }

    private static bool SafeIsRegistered()
    {
        try { return ShellRegistration.IsRegistered(ExePath); }
        catch { return false; }
    }

    private static string Describe(KillResult r) => r switch
    {
        KillResult.Success => "Sonlandırıldı",
        KillResult.AlreadyExited => "Zaten kapanmış",
        KillResult.AccessDenied => "Erişim reddedildi",
        KillResult.Protected => "Korumalı",
        KillResult.NoWindow => "Penceresi yok",
        _ => "Başarısız",
    };
}
