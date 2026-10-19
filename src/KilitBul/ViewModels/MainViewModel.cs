using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KilitBul.Services;

namespace KilitBul.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<ProcessItemViewModel> Processes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetName), nameof(TargetFolder))]
    private string? _targetPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState), nameof(ShowList))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(RequestStopCommand), nameof(ConfirmStopCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState), nameof(ShowList))]
    private bool _scanCompleted;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestStopCommand))]
    [NotifyPropertyChangedFor(nameof(ConfirmText))]
    private ProcessItemViewModel? _selectedProcess;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmText))]
    private bool _isConfirming;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateText), nameof(ShowUpdateBanner))]
    private UpdateInfo? _availableUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateText))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private bool _isUpdating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateText))]
    private double _updateProgress;

    public bool ShowUpdateBanner => AvailableUpdate is not null;

    public string UpdateText => AvailableUpdate is not { } u ? ""
        : IsUpdating ? $"Kilit Bul {u.Version} indiriliyor… %{UpdateProgress * 100:0}"
        : $"Kilit Bul {u.Version} yayınlandı.";

    public string TargetName => string.IsNullOrEmpty(TargetPath) ? "Kilit Bul" : Path.GetFileName(TargetPath.TrimEnd('\\')) is { Length: > 0 } n ? n : TargetPath;
    public string TargetFolder => string.IsNullOrEmpty(TargetPath) ? "Bir dosya veya klasöre sağ tıklayıp Kilit Bul'u seçin." : Path.GetDirectoryName(TargetPath.TrimEnd('\\')) ?? TargetPath;

    public bool ShowEmptyState => ScanCompleted && !IsBusy && Processes.Count == 0;
    public bool ShowList => Processes.Count > 0;

    public string ConfirmText => SelectedProcess is null ? "" : $"{SelectedProcess.Name} kapatılsın mı? Kaydedilmemiş veriler kaybolabilir.";

    public MainViewModel()
    {
        Processes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(ShowList));
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
        var target = TargetPath;
        if (string.IsNullOrWhiteSpace(target))
            return;

        _scanCts?.Cancel();
        var cts = new CancellationTokenSource();
        _scanCts = cts;

        IsBusy = true;
        IsConfirming = false;
        ScanCompleted = false;
        SetStatus("Aranıyor…");

        var progress = new Progress<ScanProgress>(p =>
        {
            if (_scanCts == cts)
                SetStatus(p.Message);
        });
        try
        {
            var found = await LockFinder.FindAsync(target, progress, cts.Token);
            if (_scanCts != cts)
                return; // bu arada yeni bir tarama başladı

            var previous = SelectedProcess?.ProcessId;
            Processes.Clear();
            foreach (var p in found.Where(p => !p.IsSelf))
                Processes.Add(new ProcessItemViewModel(p));
            SelectedProcess = Processes.FirstOrDefault(p => p.ProcessId == previous && p.CanStop)
                              ?? Processes.FirstOrDefault(p => p.CanStop);

            ScanCompleted = true;
            SetStatus(Processes.Count switch
            {
                0 => "",
                1 => "1 uygulama bu öğeyi kullanıyor.",
                var n => $"{n} uygulama bu öğeyi kullanıyor.",
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_scanCts == cts)
                SetStatus(ex.Message, error: true);
        }
        finally
        {
            if (_scanCts == cts)
                IsBusy = false;
        }
    }

    private bool CanRefresh() => !IsBusy && !string.IsNullOrWhiteSpace(TargetPath);

    [RelayCommand(CanExecute = nameof(CanRequestStop))]
    private void RequestStop() => IsConfirming = true;

    private bool CanRequestStop() => !IsBusy && SelectedProcess is { CanStop: true };

    [RelayCommand]
    private void CancelStop() => IsConfirming = false;

    [RelayCommand(CanExecute = nameof(CanConfirmStop))]
    private async Task ConfirmStopAsync()
    {
        IsConfirming = false;
        if (SelectedProcess is not { CanStop: true } item)
            return;

        IsBusy = true;
        SetStatus($"{item.Name} durduruluyor…");
        KillResult? result;
        try
        {
            result = await Task.Run(() => ProcessKiller.Kill(item.Model));
            if (result == KillResult.AccessDenied && !ElevationHelper.IsAdministrator())
                result = await ElevationHelper.KillElevatedAsync(item.Model);
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync();

        var message = result switch
        {
            null => "Yönetici izni verilmediği için durdurulamadı.",
            KillResult.Success or KillResult.AlreadyExited => null,
            KillResult.AccessDenied => $"{item.Name} durdurulamadı: erişim reddedildi.",
            KillResult.StillRunning => $"{item.Name} hâlâ kapanıyor; birkaç saniye sonra yenileyin (F5).",
            KillResult.Protected => $"{item.Name} bir sistem işlemi; durdurulamaz.",
            _ => $"{item.Name} durdurulamadı.",
        };
        if (message is not null)
            SetStatus(message, error: true);
    }

    private bool CanConfirmStop() => !IsBusy;

    /// <summary>GitHub Releases'te yeni sürüm var mı? Ağ yoksa sessizce geçer.</summary>
    public async Task CheckForUpdatesAsync() => AvailableUpdate = await UpdateService.CheckAsync();

    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private async Task InstallUpdateAsync()
    {
        if (AvailableUpdate is not { } update)
            return;

        IsUpdating = true;
        try
        {
            var progress = new Progress<double>(p => UpdateProgress = p);
            var installer = await UpdateService.DownloadAsync(update, progress);
            if (UpdateService.LaunchInstaller(installer))
                System.Windows.Application.Current.Shutdown(); // kurulum exe'yi değiştirebilsin
        }
        catch (Exception ex)
        {
            SetStatus("Güncelleme indirilemedi: " + ex.Message, error: true);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private bool CanInstallUpdate() => !IsUpdating;

    private void SetStatus(string text, bool error = false)
    {
        StatusText = text;
        StatusIsError = error;
    }
}
