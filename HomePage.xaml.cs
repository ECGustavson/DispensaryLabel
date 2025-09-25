using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace DispensaryLabel
{
    public sealed partial class HomePage : Page
    {
        private List<Strain> strains = new List<Strain>();
        private SerialPort scalePort;
        private DispatcherTimer scaleTimer;
        private bool isScaleConnected = false;

        public HomePage()
        {
            this.InitializeComponent();
            this.Loaded += HomePage_Loaded;
            this.Unloaded += HomePage_Unloaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadStrainsFromCsv();
            await InitializeScaleAsync();
            if (isScaleConnected)
            {
                WeightTextBox.IsReadOnly = true;
                StartScalePolling();
            }
            else
            {
                WeightTextBox.IsReadOnly = false;
            }
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopScalePolling();
            CloseScalePort();
        }

        private async Task LoadStrainsFromCsv()
        {
            try
            {
                StorageFile file = await Helper.GetCsvFileAsync();

                var lines = await FileIO.ReadLinesAsync(file);

                strains = lines.Select(line =>
                {
                    var parts = line.Split(',');
                    return new Strain
                    {
                        Name = parts[0].Trim(),
                        Type = parts[1].Trim(),
                        Thc = parts[2].Trim(),
                        Hyperlink = parts[3].Trim()
                    };
                }).ToList();

                StrainComboBox.ItemsSource = strains.Select(s => s.Name).ToList();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog { Title = "Error", Content = ex.Message, CloseButtonText = "OK" };
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
        }

        private void StrainComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrainComboBox.SelectedItem is string selectedName)
            {
                var selectedStrain = strains.FirstOrDefault(s => s.Name == selectedName);
                if (selectedStrain != null)
                {
                    StrainType.Text = selectedStrain.Type;
                    ThcPercentage.Text = selectedStrain.Thc;
                    StrainHyperlink.Content = selectedStrain.Hyperlink;
                    try
                    {
                        StrainHyperlink.NavigateUri = new Uri(selectedStrain.Hyperlink);
                    }
                    catch (UriFormatException)
                    {
                        StrainHyperlink.NavigateUri = null;
                        StrainHyperlink.Content = $"{selectedStrain.Hyperlink} (Invalid URL)";
                    }
                }
            }
        }

        private async void PrintLabel_Click(object sender, RoutedEventArgs e)
        {
            if (StrainComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(WeightTextBox.Text))
            {
                await ShowDialog("Error", "Please select a strain and enter weight.");
                return;
            }

            var selectedName = StrainComboBox.SelectedItem as string;
            var selectedStrain = strains.FirstOrDefault(s => s.Name == selectedName);
            var weight = WeightTextBox.Text;
            var settings = ApplicationData.Current.LocalSettings;
            var companyAddress = settings.Values["CompanyAddress"] as string ?? "Default Address";
            var date = DateTime.Now.ToString("yyyy-MM-dd");

            // Build label data
            string strainName = selectedStrain.Name;
            string type = selectedStrain.Type;
            string thc = selectedStrain.Thc;
            string hyperlink = selectedStrain.Hyperlink;

            // Generate EZPL command string (for 2"x1" label at 203 dpi)
            StringBuilder ezpl = new StringBuilder();
            ezpl.AppendLine("^Q203,0,0"); // Label height 203 dots (1 inch), no gap, no offset (adjust if gaps)
            ezpl.AppendLine("^W406"); // Label width 406 dots (2 inches)
            ezpl.AppendLine("^H10"); // Heat 10
            ezpl.AppendLine("^P1"); // Print 1 copy
            ezpl.AppendLine("^S4"); // Speed 4 ips
            ezpl.AppendLine("^L"); // Start format
            ezpl.AppendLine($"A0,10,10,1,1,0,0,{companyAddress}"); // Company address, font A, position (10,10)
            ezpl.AppendLine($"A0,10,50,1,1,0,0,Strain: {strainName}"); // Strain name
            ezpl.AppendLine($"A0,10,80,1,1,0,0,Type: {type}"); // Type
            ezpl.AppendLine($"A0,10,110,1,1,0,0,THC: {thc}%"); // THC
            ezpl.AppendLine($"A0,10,140,1,1,0,0,Weight: {weight}g"); // Weight
            ezpl.AppendLine($"A0,10,170,1,1,0,0,Date: {date}"); // Date
            // QR code for hyperlink (position 250,10, auto mode, M error correction, mul 1)
            ezpl.AppendLine($"W250,10,0,Q,M,0,1,0,{hyperlink.Length},{hyperlink}"); // QR command
            ezpl.AppendLine("E"); // End and print

            string ezplCommand = ezpl.ToString();

            // Attempt to print if printer COM set
            var printerCom = settings.Values["PrinterComPort"] as string;
            bool printed = false;
            if (!string.IsNullOrEmpty(printerCom))
            {
                try
                {
                    using (var printerPort = new SerialPort(printerCom, 9600, Parity.None, 8, StopBits.One))
                    {
                        printerPort.Open();
                        printerPort.Write(ezplCommand);
                        printerPort.Close();
                        printed = true;
                    }
                }
                catch (Exception ex)
                {
                    await ShowDialog("Print Error", $"Failed to print: {ex.Message}");
                }
            }

            // Show mock-up dialog (always, or if not printed)
            var mockContent = new StackPanel { Spacing = 5 };
            mockContent.Children.Add(new TextBlock { Text = companyAddress });
            mockContent.Children.Add(new TextBlock { Text = $"Strain: {strainName}" });
            mockContent.Children.Add(new TextBlock { Text = $"Type: {type}" });
            mockContent.Children.Add(new TextBlock { Text = $"THC: {thc}%" });
            mockContent.Children.Add(new TextBlock { Text = $"Weight: {weight}g" });
            mockContent.Children.Add(new TextBlock { Text = $"Date: {date}" });
            mockContent.Children.Add(new TextBlock { Text = $"Link: {hyperlink}" }); // QR mock as text

            var mockDialog = new ContentDialog
            {
                Title = "Label Mock-Up",
                Content = mockContent,
                CloseButtonText = "OK"
            };
            mockDialog.XamlRoot = this.XamlRoot;
            await mockDialog.ShowAsync();

            if (printed)
            {
                await ShowDialog("Success", "Label printed successfully.");
            }
        }

        private async Task InitializeScaleAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            string comPort = settings.Values["ScaleComPort"] as string;
            if (string.IsNullOrEmpty(comPort))
            {
                return;
            }

            try
            {
                scalePort = new SerialPort(comPort, 9600, Parity.None, 8, StopBits.One);
                scalePort.ReadTimeout = 500;
                scalePort.WriteTimeout = 500;
                scalePort.Open();
                isScaleConnected = true;
            }
            catch (Exception ex)
            {
                isScaleConnected = false;
                await ShowDialog("Scale Connection Error", $"Failed to connect to scale: {ex.Message}");
            }
        }

        private void StartScalePolling()
        {
            if (scaleTimer == null)
            {
                scaleTimer = new DispatcherTimer();
                scaleTimer.Interval = TimeSpan.FromMilliseconds(500); // Poll every 1 second
                scaleTimer.Tick += async (s, e) => await ReadScaleWeightAsync();
            }
            scaleTimer.Start();
        }

        private void StopScalePolling()
        {
            scaleTimer?.Stop();
        }

        private void CloseScalePort()
        {
            if (scalePort != null && scalePort.IsOpen)
            {
                scalePort.Close();
            }
        }

        private async Task ReadScaleWeightAsync()
        {
            if (!isScaleConnected || scalePort == null || !scalePort.IsOpen)
            {
                return;
            }

            try
            {
                scalePort.Write("SI\r\n"); // Send immediate weight command
                string response = scalePort.ReadExisting(); // Use ReadExisting to get all data, as ReadLine may not work if no LF

                if (response.Length >= 19 && response.StartsWith("SI "))
                {
                    char stability = response[3]; // 0-based index 3 (1-based position 4)
                    if (stability == ' ') // Stable
                    {
                        btnPrint.IsEnabled = true;
                        printIcon.Symbol = Symbol.Print;
                        char signChar = response[5]; // index 5 (position 6)
                        string sign = (signChar == '-') ? "-" : "";
                        string massStr = response.Substring(6, 9).Trim(); // index 6-14 (positions 7-15)
                        string unit = response.Substring(15, 4).Trim(); // index 15 is space (16), then 16-18 unit

                        if (double.TryParse(sign + massStr, out double weight))
                        {
                            // Assume unit is 'g'; if 'kg', convert to g if needed
                            if (unit == "kg")
                            {
                                weight *= 1000;
                            }
                            WeightTextBox.Text = weight.ToString("F2"); // Format to 3 decimal places or as needed
                        }
                    }
                    else
                    {
                        btnPrint.IsEnabled = false;
                        printIcon.Symbol = Symbol.Cancel;
                        WeightTextBox.Text = "Weight Unstable - Print Prohibited";
                    }
                    // If unstable ('?'), do not update the TextBox
                }
                else if (response.Contains("SI_I"))
                {
                    // Command not accessible; ignore
                }
            }
            catch (Exception ex)
            {
                isScaleConnected = false;
                WeightTextBox.IsReadOnly = false;
                StopScalePolling();
                await ShowDialog("Scale Read Error", $"Failed to read weight: {ex.Message}");
            }
        }

        private async Task ShowDialog(string title, string content)
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

        private void StrainComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            // Check if the entered text matches any strain Name (case-insensitive for better UX; adjust if needed)
            bool isValid = strains.Any(s => string.Equals(s.Name, args.Text, StringComparison.OrdinalIgnoreCase));

            if (!isValid)
            {
                args.Handled = true;  // Prevent the invalid text from being committed
                sender.Text = sender.SelectedItem?.ToString() ?? string.Empty;
            }
            // If valid, do nothing—filtering auto-selects the match, and SelectionChanged will fire if needed
        }
    }
}