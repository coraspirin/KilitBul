using System.Windows.Media;
using KilitBul.Services;

namespace KilitBul.ViewModels;

public sealed class ProcessItemViewModel
{
    public ProcessItemViewModel(LockingProcess model) => Model = model;

    public LockingProcess Model { get; }

    public int ProcessId => Model.ProcessId;

    /// <summary>".exe" uzantısı olmadan, okunaklı ad.</summary>
    public string Name => Model.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Model.Name[..^4] : Model.Name;

    public string Details => $"PID {Model.ProcessId} · {Model.TypeDescription}";

    public string Tooltip => string.Join(Environment.NewLine,
        new[] { Model.ExecutablePath, Model.LockedPathsTooltip }.Where(s => !string.IsNullOrEmpty(s)));

    public ImageSource? Icon => IconCache.Get(Model.ExecutablePath);

    public bool CanStop => !Model.IsProtected;
}
