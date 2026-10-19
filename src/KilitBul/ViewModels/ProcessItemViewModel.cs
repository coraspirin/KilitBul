using CommunityToolkit.Mvvm.ComponentModel;
using KilitBul.Services;

namespace KilitBul.ViewModels;

public partial class ProcessItemViewModel : ObservableObject
{
    public ProcessItemViewModel(LockingProcess model) => Model = model;

    public LockingProcess Model { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _status = "";

    public int ProcessId => Model.ProcessId;
    public string Name => Model.Name;
    public string ExecutablePath => Model.ExecutablePath ?? "(erişilemedi)";
    public string StartTime => Model.StartTime == DateTime.MinValue ? "" : Model.StartTime.ToString("g");
    public string TypeDescription => Model.TypeDescription;
    public string LockedPaths => Model.LockedPathsSummary;
    public string LockedPathsTooltip => Model.LockedPathsTooltip;
    public bool IsProtected => Model.IsProtected;
    public bool CanTerminate => !Model.IsProtected;
}
