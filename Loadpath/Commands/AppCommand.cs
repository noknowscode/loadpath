namespace Loadpath.Commands;

/// <summary>
/// One named action. Menus, the palette, the floating bar and shortcuts all invoke through this. It is an
/// <see cref="ICommand"/> so a menu item can bind it; menus are built when they open, so they read
/// <see cref="CanExecute"/> then and nothing listens for changes.
/// </summary>
public sealed class AppCommand : ICommand
{
    public AppCommand(string id, string title, string category, string? shortcut, Action execute, Func<bool>? canExecute = null, string? icon = null)
    {
        Id = id;
        Title = title;
        Category = category;
        Shortcut = shortcut;
        Icon = icon;
        _execute = execute;
        _canExecute = canExecute;
    }

    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public string Id { get; }
    public string Title { get; }
    public string Category { get; }
    public string? Shortcut { get; }
    public string? Icon { get; }
    public bool CanExecute => _canExecute?.Invoke() ?? true;
    public string ShortcutDisplay => Shortcut ?? "";

    public bool TryExecute()
    {
        if (!CanExecute) return false;
        _execute();
        return true;
    }

    bool ICommand.CanExecute(object? parameter) => CanExecute;

    void ICommand.Execute(object? parameter) => TryExecute();

    /// <summary>Never raised: see the class summary.</summary>
    event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }
}
