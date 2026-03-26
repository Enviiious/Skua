using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
        .drop-note { font-size: 11px; color: #888; margin: 0 0 4px 0; font-style: italic; }
        .rate-good   { color: #66bb6a; font-weight: 600; }
        .rate-medium { color: #ffa726; font-weight: 600; }
        .rate-low    { color: #ef5350; font-weight: 600; }
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

        // Append observed drop rate data from the user's farming sessions
        string dropSection = BuildDropRateSection(page.Title);
        if (!string.IsNullOrEmpty(dropSection))
            body += dropSection;

        string html = $"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
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

    // ── Drop Rate injection ───────────────────────────────────────────────────

    // Simple record matching the DropRatePlugin's CumulativeData JSON format
    private sealed class DropEntry
    {
        public int    QuestId   { get; set; }
        public string QuestName { get; set; } = string.Empty;
        public string ItemName  { get; set; } = string.Empty;
        public int    Drops     { get; set; }
        public int    Kills     { get; set; }
    }

    // Cache: normalized item name → rows (refreshed every 60 s)
    private static Dictionary<string, List<DropEntry>>? _dropCache;
    private static DateTime _dropCacheStamp;
    private static readonly object _dropCacheLock = new();
    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private static Dictionary<string, List<DropEntry>> GetDropCache()
    {
        lock (_dropCacheLock)
        {
            if (_dropCache != null && (DateTime.Now - _dropCacheStamp).TotalSeconds < 60)
                return _dropCache;

            var cache = new Dictionary<string, List<DropEntry>>(StringComparer.Ordinal);
            string skuaDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skua");

            if (Directory.Exists(skuaDir))
            {
                foreach (string file in Directory.GetFiles(skuaDir, "DropRates_*.json"))
                {
                    try
                    {
                        var raw = JsonSerializer.Deserialize<Dictionary<string, DropEntry>>(
                            File.ReadAllText(file), _jsonOpts);
                        if (raw == null) continue;

                        foreach (var (_, entry) in raw)
                        {
                            if (string.IsNullOrEmpty(entry.ItemName) || entry.Kills == 0) continue;
                            string key = Norm(entry.ItemName);
                            if (!cache.TryGetValue(key, out var list))
                                cache[key] = list = new List<DropEntry>();
                            list.Add(entry);
                        }
                    }
                    catch { /* skip corrupt file */ }
                }
            }

            _dropCache = cache;
            _dropCacheStamp = DateTime.Now;
            return cache;
        }
    }

    /// <summary>Strips all non-alphanumeric characters and lowercases — used to fuzzy-match
    /// wiki slugs (e.g. "fears-bones") against item names (e.g. "Fear's Bones").</summary>
    private static string Norm(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// <summary>Builds an HTML section showing the user's observed drop rates for this item,
    /// or returns an empty string if no data exists for the current wiki page.</summary>
    private static string BuildDropRateSection(string pageTitle)
    {
        try
        {
            var cache = GetDropCache();
            string key = Norm(pageTitle);
            if (!cache.TryGetValue(key, out var rows) || rows.Count == 0)
                return string.Empty;

            // Group by quest (multiple accounts may contribute data for the same quest)
            var byQuest = rows
                .GroupBy(r => r.QuestId)
                .Select(g => new
                {
                    QuestName = g.First().QuestName,
                    Drops     = g.Sum(r => r.Drops),
                    Kills     = g.Sum(r => r.Kills),
                })
                .OrderByDescending(q => q.Kills)
                .ToList();

            if (byQuest.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<h2>Observed Drop Rates</h2>");
            sb.Append("<p class=\"drop-note\">Based on your farming sessions &mdash; all accounts combined.</p>");
            sb.Append("<table><thead><tr><th>Quest</th><th>Drops / Kills</th><th>Rate</th></tr></thead><tbody>");

            foreach (var q in byQuest)
            {
                if (q.Kills == 0) continue;
                double rate = q.Drops * 100.0 / q.Kills;
                string cls  = rate >= 50 ? "rate-good" : rate >= 20 ? "rate-medium" : "rate-low";
                sb.Append("<tr>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(q.QuestName)}</td>");
                sb.Append($"<td>{q.Drops}&nbsp;/&nbsp;{q.Kills}</td>");
                sb.Append($"<td class=\"{cls}\">{rate:F1}%</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    // ── Plain-text fallback ───────────────────────────────────────────────────

    /// <summary>Fallback when no HTML is available — convert plain text to simple HTML.</summary>
    private static string BuildTextBody(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder();
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

    // ── Navigation ────────────────────────────────────────────────────────────

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
            // If the page isn't in the local wiki (e.g. merge shop, quest, NPC),
            // fall back to opening it in the user's browser.
            bool found = _vm?.NavigateToSlug(slug) ?? false;
            if (!found)
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
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
