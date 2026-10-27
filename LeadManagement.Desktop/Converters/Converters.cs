using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LeadManagement.Desktop.Converters
{
    /// <summary>
    /// Converts a boolean to Visibility (true → Visible, false → Collapsed).
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility.Visible;
        }
    }

    /// <summary>
    /// Inverts a boolean value (true → false, false → true).
    /// Useful for disabling controls while IsLoggingIn = true.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }
    }
}
