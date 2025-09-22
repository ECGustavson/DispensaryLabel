using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO.Ports;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DispensaryLabel
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadComPorts();
            LoadSavedSettings();
        }

        private void LoadComPorts()
        {
            var ports = SerialPort.GetPortNames();
            PrinterComPorts.ItemsSource = ports;
            ScaleComPorts.ItemsSource = ports;
        }

        private void LoadSavedSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;
            CompanyAddress.Text = settings.Values["CompanyAddress"] as string ?? "";

            // Load CSV path if token exists
            var csvToken = settings.Values["CsvFileToken"] as string;
            if (!string.IsNullOrEmpty(csvToken) && StorageApplicationPermissions.FutureAccessList.ContainsItem(csvToken))
            {
                try
                {
                    var file = StorageApplicationPermissions.FutureAccessList.GetFileAsync(csvToken).AsTask().Result;
                    CsvFilePath.Text = file.Path;
                }
                catch
                {
                    CsvFilePath.Text = "Invalid file path";
                }
            }
            else
            {
                CsvFilePath.Text = "No file selected (using default)";
            }

            PrinterComPorts.SelectedItem = settings.Values["PrinterComPort"] as string;
            ScaleComPorts.SelectedItem = settings.Values["ScaleComPort"] as string;
        }

        private async void BrowseCsv_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".csv");

            // Initialize picker (for WinUI 3, need window handle)
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow); // Assume App has static MainWindow
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                // Add to FutureAccessList and save token
                var token = StorageApplicationPermissions.FutureAccessList.Add(file);
                var settings = ApplicationData.Current.LocalSettings;
                settings.Values["CsvFileToken"] = token;
                CsvFilePath.Text = file.Path;
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["CompanyAddress"] = CompanyAddress.Text;
            settings.Values["PrinterComPort"] = PrinterComPorts.SelectedItem as string;
            settings.Values["ScaleComPort"] = ScaleComPorts.SelectedItem as string;
            // Csv token is saved during browse, no need here unless changed
            // Optionally show confirmation
        }

        private async void TestPrinter_Click(object sender, RoutedEventArgs e)
        {
            string comPort = PrinterComPorts.SelectedItem as string;
            if (string.IsNullOrEmpty(comPort))
            {
                await ShowDialog("Error", "Please select a COM port for the printer.");
                return;
            }

            try
            {
                using (var serialPort = new SerialPort(comPort, 9600)) // Adjust baud rate as needed
                {
                    serialPort.Open();
                    // Send a test command if known, e.g., for label printer, perhaps a simple text
                    serialPort.Write("Test print from app\r\n");
                    serialPort.Close();
                }
                await ShowDialog("Success", "Printer test successful!");
            }
            catch (Exception ex)
            {
                await ShowDialog("Error", $"Printer test failed: {ex.Message}");
            }
        }

        // Update TestScale_Click in SettingsPage.xaml.cs to match parsing

        private async void TestScale_Click(object sender, RoutedEventArgs e)
        {
            string comPort = ScaleComPorts.SelectedItem as string;
            if (string.IsNullOrEmpty(comPort))
            {
                await ShowDialog("Error", "Please select a COM port for the scale.");
                return;
            }

            try
            {
                using (var serialPort = new SerialPort(comPort, 9600, Parity.None, 8, StopBits.One))
                {
                    serialPort.Open();
                    serialPort.Write("SI\r\n");
                    string response = serialPort.ReadExisting();
                    serialPort.Close();

                    // Parse for test
                    if (response.Length >= 19 && response.StartsWith("SI "))
                    {
                        char stability = response[3];
                        string status = (stability == ' ') ? "Stable" : "Unstable";
                        char signChar = response[5];
                        string sign = (signChar == '-') ? "-" : "";
                        string massStr = response.Substring(6, 9).Trim();
                        string unit = response.Substring(15, 4).Trim();
                        await ShowDialog("Success", $"Scale test successful! {status} weight: {sign + massStr} {unit}");
                    }
                    else
                    {
                        await ShowDialog("Success", $"Scale response: {response}");
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowDialog("Error", $"Scale test failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ShowDialog(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK"
            };
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }
    }
}