using System.Windows.Input;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// A minimal async parameterized <see cref="ICommand"/> for view models — the typed counterpart to
/// <see cref="RelayCommand"/>. Plain .NET (no MAUI dependency) so the view models that use it remain
/// unit-testable. Re-entrancy is blocked while the async action runs; a parameter of the wrong type
/// (or <c>null</c> for a reference type) is ignored.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Func<T, Task> _execute;
    private readonly Func<T, bool>? _canExecute;
    private bool _isRunning;

    public RelayCommand(Func<T, Task> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isRunning && parameter is T value && (_canExecute?.Invoke(value) ?? true);

    public async void Execute(object? parameter)
    {
        if (parameter is not T value || !CanExecute(parameter))
            return;

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(value);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
