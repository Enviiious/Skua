using Microsoft.Extensions.DependencyInjection;
using Skua.Core.Interfaces;
using System.Windows;
using UltraPlugin;

/// <summary>
/// Ultra Boss Manager Plugin
///
/// Provides a UI window to:
///   • Select which of the 17 Ultra bosses you are running
///   • Enter your class name
///   • Toggle the Taunter role on or off
///   • Start / Stop Auto Attack directly from the window
///
/// Role descriptions, taunting requirements, mechanics notes, and
/// enhancement tips are driven by UltraInfo.All for each boss.
///
/// Accessible via the Plugins menu → "Ultra Boss Manager".
/// </summary>
public class UltraBossPlugin : ISkuaPlugin
{
    public string Name        => "Ultra Boss Manager";
    public string Author      => "Ty";
    public string Description => "Select an Ultra, pick your role (DPS or Taunter), and start Auto Attack.";
    public List<IOption>? Options => null;

    private IServiceProvider? _provider;
    private Window?           _window;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Load(IServiceProvider provider, IPluginHelper helper)
    {
        _provider = provider;
        helper.AddMenuButton("Ultra Boss Manager", OpenWindow);
    }

    public void Unload()
    {
        Application.Current?.Dispatcher.Invoke(() => _window?.Close());
        _window   = null;
        _provider = null;
    }

    // ── Window management ────────────────────────────────────────────────────

    private void OpenWindow()
    {
        // Bring existing window to front instead of opening a second copy
        if (_window?.IsLoaded == true)
        {
            _window.Activate();
            return;
        }

        if (_provider is null) return;

        var bot = _provider.GetRequiredService<IScriptInterface>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new UltraBossWindow(bot);
            win.Closed += (_, _) => _window = null;
            win.Show();
            _window = win;
        });
    }
}
