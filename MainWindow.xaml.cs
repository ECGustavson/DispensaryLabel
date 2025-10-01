using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;  // Added for AppWindow
using Microsoft.UI;  // Added for Win32Interop if needed, but mainly for Colors
using WinRT.Interop;  // Already present, but ensuring

namespace DispensaryLabel
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            //ContentFrame.Navigate(typeof(SettingsPage));
            ContentFrame.Navigate(typeof(HomePage)); // Default to Home

            // Customize title bar with fixed color (independent of theme)
            CustomizeTitleBar();
        }

        private void CustomizeTitleBar()
        {
            if (AppWindowTitleBar.IsCustomizationSupported())  // Check support (Win11+)
            {
                var titleBar = this.AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = false;  // Keep standard title bar (no extension needed)

                // Fixed background color: Dark gray (#212121) - looks good with both light/dark modes
                var backgroundColor = Windows.UI.Color.FromArgb(255, 33, 33, 33);

                titleBar.BackgroundColor = backgroundColor;
                titleBar.ForegroundColor = Colors.White;
                titleBar.InactiveBackgroundColor = backgroundColor;
                titleBar.InactiveForegroundColor = Colors.LightGray;

                // Button states (for close/minimize/maximize)
                titleBar.ButtonBackgroundColor = backgroundColor;
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonInactiveBackgroundColor = backgroundColor;
                titleBar.ButtonInactiveForegroundColor = Colors.LightGray;
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 50, 50, 50);  // Slightly lighter on hover
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 20, 20, 20);  // Darker on press
                titleBar.ButtonPressedForegroundColor = Colors.White;
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked) return;

            var item = args.InvokedItemContainer as NavigationViewItem;
            if (item == null) return;

            switch (item.Tag.ToString())
            {
                case "Home":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case "EditStrains":
                    ContentFrame.Navigate(typeof(EditStrainsPage));
                    break;
                case "Settings":
                    ContentFrame.Navigate(typeof(SettingsPage));
                    break;
                case "Login":
                    ContentFrame.Navigate(typeof(LoginPage));
                    break;
            }
        }
    }
}