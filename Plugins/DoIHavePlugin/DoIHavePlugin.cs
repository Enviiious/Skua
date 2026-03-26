using Microsoft.Extensions.DependencyInjection;
using Skua.Core.Interfaces;
using System.Windows;

/// <summary>
/// Do I Have? Plugin
///
/// Integrates the AqwDoIHave item database into Skua with live inventory checking.
///
///   Search Tab    — find any item by name; see if you own it (inventory count + bank).
///   Merge Shops   — browse all 685 merge shops; see ingredient ownership at a glance.
///   In Bank       — load your bank and browse / search its contents.
///
/// Data files (WikiItems.json, merge_shops.json) must be placed in a "data\" folder
/// next to the plugin DLL.  Copy them from the AqwDoIhave-main\Extension\data\ folder.
///
/// Accessible via the Plugins menu → "Do I Have?".
/// </summary>
public class DoIHavePlugin : ISkuaPlugin
{
    public string Name        => "Do I Have?";
    public string Author      => "Ty";
    public string Description => "Search the AQW item database and check your live inventory & bank.";
    public List<IOption>? Options => null;

    private IServiceProvider? _provider;
    private Window?           _window;

    public void Load(IServiceProvider provider, IPluginHelper helper)
    {
        _provider = provider;
        helper.AddMenuButton("Do I Have?", OpenWindow);
    }

    public void Unload()
    {
        Application.Current?.Dispatcher.Invoke(() => _window?.Close());
        _window   = null;
        _provider = null;
    }

    private void OpenWindow()
    {
        if (_window?.IsLoaded == true)
        {
            _window.Activate();
            return;
        }

        if (_provider is null) return;

        var bot = _provider.GetRequiredService<IScriptInterface>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new DoIHavePlugin_NS.DoIHaveWindow(bot);
            win.Closed += (_, _) => _window = null;
            win.Show();
            _window = win;
        });
    }
}
