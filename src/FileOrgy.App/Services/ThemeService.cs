using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfApp = System.Windows.Application;

namespace FileOrgy.App.Services
{
    public static class ThemeService
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

        private static readonly Uri DarkThemeUri = new("pack://application:,,,/FileOrgy;component/Themes/DarkTheme.xaml", UriKind.Absolute);
        private static readonly Uri LightThemeUri = new("pack://application:,,,/FileOrgy;component/Themes/LightTheme.xaml", UriKind.Absolute);

        public static bool IsDark { get; private set; } = true;

        public static void ApplyTheme(bool isDark)
        {
            IsDark = isDark;

            var app = WpfApp.Current;
            if (app == null)
            {
                return;
            }

            var merged = app.Resources.MergedDictionaries;
            var targetUri = isDark ? DarkThemeUri : LightThemeUri;
            var newThemeDict = new ResourceDictionary { Source = targetUri };

            int existingIndex = -1;
            for (int i = 0; i < merged.Count; i++)
            {
                if (IsThemeDictionary(merged[i]))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                merged[existingIndex] = newThemeDict;
            }
            else
            {
                merged.Insert(0, newThemeDict);
            }

            foreach (Window window in app.Windows)
            {
                ApplyWindowDarkMode(window, isDark);
            }
        }

        public static void ApplyWindowDarkMode(Window window, bool isDark)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                int useDarkMode = isDark ? 1 : 0;
                int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                if (hr != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
                }
            }
            catch
            {
                // Ignore on older Windows builds that do not support DWMWA_USE_IMMERSIVE_DARK_MODE.
            }
        }

        private static bool IsThemeDictionary(ResourceDictionary dictionary)
        {
            var source = dictionary.Source?.OriginalString;
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            return source.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase);
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}
