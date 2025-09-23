using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;  // Added for ApplicationData

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.UI.Xaml;

namespace DispensaryLabel
{
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            MainWindow = m_window;

            // Load and apply saved theme
            var settings = ApplicationData.Current.LocalSettings;
            var savedTheme = settings.Values["AppTheme"] as string;
            var root = m_window.Content as FrameworkElement;
            if (root != null)
            {
                if (savedTheme == "Dark")
                {
                    root.RequestedTheme = ElementTheme.Dark;
                }
                else
                {
                    root.RequestedTheme = ElementTheme.Light;  // Default to Light if not set
                }
            }

            m_window.Activate();
        }

        private Window m_window;
    }
}