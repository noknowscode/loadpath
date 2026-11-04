using Loadpath.Presentation;

namespace Loadpath.Views;

/// <summary>The unsaved-changes question as a dialog over the window: Save, Discard or Cancel.</summary>
public sealed class UnsavedChangesDialog(Window window) : IUnsavedPrompt
{
    public async Task<UnsavedChoice> AskAsync(string documentName)
    {
        var root = window.Content as FrameworkElement;
        var dialog = new ContentDialog
        {
            Title = "Unsaved changes",
            Content = $"“{documentName}” has changes that are not saved.",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Discard",
            CloseButtonText = "Cancel",
            XamlRoot = root?.XamlRoot,
            // A dialog opens in the popup layer, outside the window content: hand it the content's theme.
            RequestedTheme = root?.ActualTheme ?? ElementTheme.Default,
        };
        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => UnsavedChoice.Save,
            ContentDialogResult.Secondary => UnsavedChoice.Discard,
            _ => UnsavedChoice.Cancel,
        };
    }
}
