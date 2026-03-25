using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Skua.Core.ViewModels;

namespace Skua.WPF.UserControls;

public partial class WikiUserControl : UserControl
{
    private WikiViewModel? _vm;

    public WikiUserControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as WikiViewModel;

        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WikiViewModel.ParsedContent))
            RebuildDocument();
    }

    private void RebuildDocument()
    {
        var doc = new FlowDocument
        {
            FontFamily  = new FontFamily("Segoe UI"),
            FontSize    = 13,
            PagePadding = new Thickness(0),
            LineHeight  = double.NaN,
        };

        // Resolve theme brushes at runtime so we respect light/dark theme
        Brush bodyBrush    = TryBrush("MaterialDesignBody",     Brushes.WhiteSmoke);
        Brush headingBrush = TryBrush("PrimaryHueMidBrush",     Brushes.CornflowerBlue);
        Brush linkBrush    = TryBrush("SecondaryHueMidBrush",   Brushes.Gold);
        Brush dimBrush     = TryBrush("MaterialDesignBodyLight", Brushes.Gray);

        if (_vm?.ParsedContent is not { Count: > 0 } sections)
        {
            WikiContent.Document = doc;
            return;
        }

        foreach (var section in sections)
        {
            // ── Section heading ──────────────────────────────────────────────
            if (!string.IsNullOrEmpty(section.Heading))
            {
                var headPara = new Paragraph(new Run(section.Heading))
                {
                    FontWeight  = FontWeights.SemiBold,
                    FontSize    = 12,
                    Foreground  = headingBrush,
                    Margin      = new Thickness(0, 10, 0, 2),
                    TextDecorations = TextDecorations.Underline,
                };
                doc.Blocks.Add(headPara);
            }

            // ── Section items ────────────────────────────────────────────────
            foreach (var item in section.Items)
            {
                var para = new Paragraph { Margin = new Thickness(12, 0, 0, 1) };

                if (item.IsLink)
                {
                    var link = new Hyperlink(new Run(item.Text))
                    {
                        Foreground      = linkBrush,
                        TextDecorations = null,
                        Cursor          = System.Windows.Input.Cursors.Hand,
                    };
                    link.MouseEnter += (s, _) => ((Hyperlink)s!).TextDecorations = TextDecorations.Underline;
                    link.MouseLeave += (s, _) => ((Hyperlink)s!).TextDecorations = null;

                    string captured = item.Text;
                    link.Click += (_, _) => _vm?.NavigateTo(captured);

                    para.Inlines.Add(link);
                }
                else
                {
                    para.Inlines.Add(new Run(item.Text) { Foreground = bodyBrush });
                }

                doc.Blocks.Add(para);
            }
        }

        WikiContent.Document = doc;
    }

    private Brush TryBrush(string key, Brush fallback)
        => Application.Current.TryFindResource(key) as Brush ?? fallback;
}
