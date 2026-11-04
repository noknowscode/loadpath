using Windows.System;

namespace Loadpath.Controls;

/// <summary>
/// Modifier state for shortcut routing and Shift/Ctrl-aware clicks. WinAppSDK answers from the OS; every Uno head
/// (desktop, WebAssembly, Android, iOS) answers from the key state Uno tracks as key events arrive, so a hardware
/// keyboard on a tablet or in a browser gets the same behaviour. With no keyboard nothing is ever down.
/// </summary>
public static class KeyboardState
{
    public static bool IsDown(VirtualKey key)
    {
        try
        {
            return Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }
        catch (Exception)
        {
            // A head that cannot report key state counts as nothing held.
            return false;
        }
    }
}
