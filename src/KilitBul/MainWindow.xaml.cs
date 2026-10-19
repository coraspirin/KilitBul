using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KilitBul.Services;
using KilitBul.ViewModels;

namespace KilitBul;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (WindowEffects.Apply(this, App.IsDarkTheme))
            Background = Brushes.Transparent; // Mica görünsün
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Esc: önce onayı kapat, sonra pencereyi.
        if (e.Key == Key.Escape)
        {
            if (ViewModel.IsConfirming)
                ViewModel.CancelStopCommand.Execute(null);
            else
                Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsConfirming) && ViewModel.IsConfirming)
            Dispatcher.BeginInvoke(() => ConfirmButton.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }
}
