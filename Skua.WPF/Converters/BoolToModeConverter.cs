using System;
using System.Globalization;
using System.Windows.Data;

namespace Skua.WPF.Converters;

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Outfit Mode (Wardrobe)" : "Classic Mode (Per-item)";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
