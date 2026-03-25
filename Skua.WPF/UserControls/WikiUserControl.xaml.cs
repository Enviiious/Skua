using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Skua.Core.ViewModels;

namespace Skua.WPF.UserControls;

public partial class WikiUserControl : UserControl
{
    private WikiViewModel? _vm;

    // Dark Material Design CSS matching Skua's theme
    private const string Css = """
        body {
            font-family: 'Segoe UI', Roboto, sans-serif;
            font-size: 13px;
            color: #e0e0e0;
            background: #303030;
            margin: 12px 16px;
            line-height: 1.6;
        }
        h1 { font-size: 17px; color: #ffffff; margin: 0 0 8px 0;
             border-bottom: 1px solid #555; padding-bottom: 4px; }
        h2 { font-size: 14px; color: #ef9a9a; margin: 12px 0 4px 0; font-weight: 600; }
        h3 { font-size: 13px; color: #ef9a9a; margin: 8px 0 2px 0; }
        a  { color: #ef5350; text-decoration: none; }
        a:hover { text-decoration: underline; color: #ff8a80; }
        b, strong { color: #ffffff; }
        ul, ol { margin: 2px 0 6px 0; padding-left: 22px; }
        li { margin: 2px 0; }
        table { border-collapse: collapse; width: 100%; margin: 8px 0; }
        th { background: #424242; color: #ef9a9a; font-weight: 600;
             text-align: left; border-bottom: 2px solid #ef5350; }
        td, th { border: 1px solid #555; padding: 5px 10px; }
        tr:nth-child(even) td { background: #3a3a3a; }
        img { max-width: 120px; max-height: 120px; }
        .item-title { font-size: 18px; font-weight: 700; color: #ef5350; margin-bottom: 10px; }
        p { margin: 4px 0 8px 0; }
        """;

    // Strips <script>, <link>, <style>, and inline on* handlers that cause IE engine errors
    private static readonly Regex ScriptTagRx  = new(@"<script\b[^>]*>[\s\S]*?</script>",          RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LinkTagRx    = new(@"<link\b[^>]*/?>",                            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleTagRx   = new(@"<style\b[^>]*>[\s\S]*?</style>",            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OnHandlerRx  = new(@"\s+on\w+=""[^""]*""",                        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public WikiUserControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Suppress IE script-error dialogs by setting the underlying ActiveX "Silent" flag
        WikiContent.LoadCompleted += (_, _) => SuppressScriptErrors(WikiContent);
    }

    private static void SuppressScriptErrors(WebBrowser browser)
    {
        try
        {
            var field = typeof(WebBrowser).GetField(
                "_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            object? ax = field?.GetValue(browser);
            ax?.GetType().InvokeMember(
                "Silent", BindingFlags.SetProperty, null, ax, new object[] { true });
        }
        catch { /* best-effort */ }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            WikiContent.Navigating -= OnNavigating;
        }

        _vm = e.NewValue as WikiViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            WikiContent.Navigating += OnNavigating;
        }

        RenderPage();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WikiViewModel.SelectedPage))
            RenderPage();
    }

    private void RenderPage()
    {
        if (_vm?.SelectedPage is not { } page)
        {
            WikiContent.NavigateToString("<html><body style='background:#303030;margin:0'></body></html>");
            return;
        }

        string rawHtml = !string.IsNullOrWhiteSpace(page.Html)
            ? page.Html
            : BuildTextBody(page.Text);

        // Strip external JS, link tags, style overrides, and inline event handlers
        // so the old IE engine doesn't throw script errors or load external resources
        string body = OnHandlerRx.Replace(
                      StyleTagRx.Replace(
                      LinkTagRx.Replace(
                      ScriptTagRx.Replace(rawHtml, string.Empty),
                      string.Empty),
                      string.Empty),
                      string.Empty);

        string html = $"""
            <!DOCTYPE html>
            <html>
            <head>
            <base href="http://aqwwiki.wikidot.com/" />
            <meta http-equiv="X-UA-Compatible" content="IE=edge">
            <style>{Css}</style>
            </head>
            <body>
            <div class="item-title">{System.Net.WebUtility.HtmlEncode(page.Title)}</div>
            {body}
            </body>
            </html>
            """;

        WikiContent.NavigateToString(html);
    }

    /// <summary>Fallback when no HTML is available — convert plain text to simple HTML.</summary>
    private static string BuildTextBody(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new System.Text.StringBuilder();
        bool inList = false;

        foreach (var rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line) || line == "-")
            {
                if (inList) { sb.AppendLine("</ul>"); inList = false; }
                continue;
            }

            if (line.EndsWith(':') && line.Length > 1 && !line.StartsWith(' '))
            {
                if (inList) { sb.AppendLine("</ul>"); inList = false; }
                sb.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(line[..^1])}</h2>");
            }
            else
            {
                if (!inList) { sb.AppendLine("<ul>"); inList = true; }
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line.TrimStart())}</li>");
            }
        }

        if (inList) sb.AppendLine("</ul>");
        return sb.ToString();
    }

    /// <summary>Intercept navigation so wiki-internal links open inside the browser panel.</summary>
    private void OnNavigating(object sender, NavigatingCancelEventArgs e)
    {
        if (e.Uri == null) return;

        e.Cancel = true;
        string url = e.Uri.AbsoluteUri;

        // With <base href="http://aqwwiki.wikidot.com/">, relative links like /diamond-of-nulgath
        // resolve to http://aqwwiki.wikidot.com/diamond-of-nulgath
        if (url.Contains("aqwwiki.wikidot.com", StringComparison.OrdinalIgnoreCase))
        {
            // Slug is the last non-empty path segment
            string slug = url.TrimEnd('/')
                            .Split('/', StringSplitOptions.RemoveEmptyEntries)
                            .Last();
            _vm?.NavigateToSlug(slug);
        }
        else if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            // External link — open in the user's default browser
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        // about://, res://, etc. — silently ignored
    }
}
