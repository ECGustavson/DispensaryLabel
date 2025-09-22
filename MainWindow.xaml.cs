using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DispensaryLabel
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Navigate(typeof(HomePage)); // Default to Home
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