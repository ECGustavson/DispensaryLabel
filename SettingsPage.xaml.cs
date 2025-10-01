// SettingsPage.xaml.cs

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Drawing.Printing;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Drawing.Printing;
using System.Security.Cryptography.X509Certificates;

namespace DispensaryLabel
{
    public sealed partial class SettingsPage : Page
    {
        public int labelHeight;
        public int labelWidth;
        public int labelGap;
        public int labelXPos;
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadPortsAndPrinters();
            LoadSavedSettings();
        }

        private void LoadPortsAndPrinters()
        {
            var ports = SerialPort.GetPortNames();
            ScaleComPorts.ItemsSource = ports;

            var printers = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
            PrinterList.ItemsSource = printers; // Assuming PrinterComPorts renamed to PrinterList in XAML
        }

        private void LoadSavedSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;
            CompanyName.Text = settings.Values["CompanyName"] as string ?? "";
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

            PrinterList.SelectedItem = settings.Values["PrinterName"] as string;
            ScaleComPorts.SelectedItem = settings.Values["ScaleComPort"] as string;

            // Load dark mode setting
            var savedTheme = settings.Values["AppTheme"] as string;
            DarkModeToggle.IsOn = (savedTheme == "Dark");

            // Load Printer Settings
            labelHeight = settings.Values["LabelHeight"] as int? ?? 75;
            spinboxLabelHeight.Value = labelHeight;
            labelWidth = settings.Values["LabelWidth"] as int? ?? 50;
            spinboxLabelWidth.Value = labelWidth;
            labelGap = settings.Values["LabelGap"] as int? ?? 3;
            spinboxLabelGap.Value = labelGap;
            labelXPos = settings.Values["LabelXPos"] as int? ?? 20;
            spinboxMasterX.Value = labelXPos;
            // Load new fallback URL settings
            FallbackURLPath.Text = settings.Values["FallbackURL"] as string ?? "";
            EnforceFallbackToggle.IsOn = settings.Values["EnforceFallback"] as bool? ?? false;
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
            settings.Values["CompanyName"] = CompanyName.Text;
            settings.Values["CompanyAddress"] = CompanyAddress.Text;
            settings.Values["PrinterName"] = PrinterList.SelectedItem as string;
            settings.Values["ScaleComPort"] = ScaleComPorts.SelectedItem as string;
            settings.Values["LabelHeight"] = (int)spinboxLabelHeight.Value;
            settings.Values["LabelWidth"] = (int)spinboxLabelWidth.Value;
            settings.Values["LabelGap"] = (int)spinboxLabelGap.Value;
            settings.Values["LabelXPos"] = (int)spinboxMasterX.Value;
            // Save new fallback URL settings
            settings.Values["FallbackURL"] = FallbackURLPath.Text;
            settings.Values["EnforceFallback"] = EnforceFallbackToggle.IsOn;
            // Csv token is saved during browse, no need here unless changed
            // Optionally show confirmation
        }

        // Added event handler for dark mode toggle
        private void DarkModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle == null) return;

            var settings = ApplicationData.Current.LocalSettings;
            var theme = toggle.IsOn ? "Dark" : "Light";
            settings.Values["AppTheme"] = theme;

            // Apply theme app-wide
            var root = App.MainWindow.Content as FrameworkElement;
            if (root != null)
            {
                root.RequestedTheme = toggle.IsOn ? ElementTheme.Dark : ElementTheme.Light;
            }
        }

        // New event handler for enforce fallback toggle
        private void EnforceFallbackToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle == null) return;

            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["EnforceFallback"] = toggle.IsOn;
        }

        private async void CalibratePrinter_Click(object sender, RoutedEventArgs e)
        {
            string printerName = PrinterList.SelectedItem as string;
            if (string.IsNullOrEmpty(printerName))
            {
                await ShowDialog("Error", "Please select a printer.");
                return;
            }

            // EZPL command for auto calibration
            string calibrationCommand = "~S,SENSOR\r\n";

            try
            {
                RawPrinterHelper.SendStringToPrinter(printerName, calibrationCommand);
                await ShowDialog("Success", "Calibration command sent successfully! Please check the printer for completion.");
            }
            catch (Exception ex)
            {
                await ShowDialog("Error", $"Calibration failed: {ex.Message}");
            }
        }

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

    public static class RawPrinterHelper
    {
        // Structure and API declarions:
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDataType;
        }
        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        // SendBytesToPrinter()
        // When the function is given a printer name and an unmanaged array
        // of bytes, the function sends those bytes to the print queue.
        // Returns true on success, false on failure.
        public static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, Int32 dwCount)
        {
            Int32 dwError = 0, dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            DOCINFOA di = new DOCINFOA();
            bool bSuccess = false; // Assume failure unless you specifically check for success.

            di.pDocName = "RAW Document";
            di.pDataType = "RAW";

            // Open the printer.
            if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
            {
                // Start a document.
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    // Start a page.
                    if (StartPagePrinter(hPrinter))
                    {
                        // Write your bytes.
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            // If you did not succeed, GetLastError may give more information
            // about why not.
            if (bSuccess == false)
            {
                dwError = Marshal.GetLastWin32Error();
                throw new Exception($"Printing failed with error code: {dwError}");
            }
            return bSuccess;
        }

        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            IntPtr pBytes;
            Int32 dwCount;
            // How many characters are in the string?
            dwCount = szString.Length;
            // Assume that the printer is expecting ANSI text, and then convert
            // the string to ANSI text.
            pBytes = Marshal.StringToCoTaskMemAnsi(szString);
            // Send the converted ANSI string to the printer.
            SendBytesToPrinter(szPrinterName, pBytes, dwCount);
            Marshal.FreeCoTaskMem(pBytes);
            return true;
        }
    }
}