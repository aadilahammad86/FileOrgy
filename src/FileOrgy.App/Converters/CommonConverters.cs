using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FileOrgy.App.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool flag && flag;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool b = v == Visibility.Visible;
                return Invert ? !b : b;
            }
            return false;
        }
    }

    public class BoolToStatusBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(16, 185, 129)); // #10B981
        private static readonly SolidColorBrush AmberBrush = new(System.Windows.Media.Color.FromRgb(245, 158, 11)); // #F59E0B

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? GreenBrush : AmberBrush;
            }
            return AmberBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(16, 185, 129));
        private static readonly SolidColorBrush RedBrush = new(System.Windows.Media.Color.FromRgb(239, 68, 68));
        private static readonly SolidColorBrush YellowBrush = new(System.Windows.Media.Color.FromRgb(245, 158, 11));
        private static readonly SolidColorBrush BlueBrush = new(System.Windows.Media.Color.FromRgb(59, 130, 246));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value?.ToString() ?? string.Empty;
            return s switch
            {
                "Success" => GreenBrush,
                "Error" => RedBrush,
                "Warning" => YellowBrush,
                _ => BlueBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
