// HomePage.xaml.cs

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
            var companyName = settings.Values["CompanyName"] as string ?? "Company Name";
            var date = DateTime.Now.ToString("yyyy-MM-dd");

            // Load label settings (with defaults)
            int labelHeight = (int)(settings.Values["LabelHeight"] ?? 75);
            int labelWidth = (int)(settings.Values["LabelWidth"] ?? 50);
            int labelGap = (int)(settings.Values["LabelGap"] ?? 3);
            int labelXPos = (int)(settings.Values["LabelXPos"] ?? 18);

            // Load new fallback URL settings
            string fallbackUrl = settings.Values["FallbackURL"] as string ?? "";
            bool enforceFallback = settings.Values["EnforceFallback"] as bool? ?? false;

            // Build label data
            string strainName = selectedStrain.Name;
            if (strainName.Length > 18)
            { strainName = strainName.Substring(0, 15) + "..."; }
            string type = selectedStrain.Type;
            string thc = selectedStrain.Thc;
            string hyperlink = selectedStrain.Hyperlink;

            // Apply fallback logic
            bool isInvalid = string.IsNullOrWhiteSpace(hyperlink) || !Uri.TryCreate(hyperlink, UriKind.Absolute, out _);
            if (enforceFallback || (!enforceFallback && isInvalid))
            {
                hyperlink = fallbackUrl;
            }

            // Generate EZPL command string (for 2"x1" label at 203 dpi, in mm)
            StringBuilder ezpl = new StringBuilder();
            ezpl.AppendLine($"^Q{labelHeight},{labelGap}"); // Label height, gap (adjust gap if no gaps/black marks: use 0 for continuous)
            ezpl.AppendLine($"^W{labelWidth}"); // Label width
            ezpl.AppendLine("^H5"); // Heat  (adjust 1-30 if print is faint/blank)
            ezpl.AppendLine("^P1"); // Print 1 copy
            ezpl.AppendLine("^S2"); // Speed in ips
            ezpl.AppendLine("^AD"); // Set direct thermal mode (no ribbon); use ^AT if using ribbon/thermal transfer
            ezpl.AppendLine("^C1"); //Number of copies per label
            ezpl.AppendLine("^R0"); // Row column adjustment
            ezpl.AppendLine("~Q+0"); // Row column adjustment
            ezpl.AppendLine("^O0"); // Disable the peel off dispenser
            ezpl.AppendLine("^D0"); // Number of labels per cut (0 to disable)
            ezpl.AppendLine("^E18"); // Stop position setting (in mm)
            ezpl.AppendLine("~R255"); // Rotates and returns label position. Not clear on how this works. See EZPL manual
            ezpl.AppendLine("^XSET,ROTATION,0"); // Row Offset Adjustment
            ezpl.AppendLine("^L"); // Start format
            ezpl.AppendLine($"AD,385,{labelXPos},1,1,0,1E,THC-A HEMP FLOWER");
            ezpl.AppendLine($"AB,339,{labelXPos},1,1,0,1E,CONTAINS <0.3% DELTA-9 THC");

            ezpl.AppendLine($"AD,257,{labelXPos},1,1,0,1E,{strainName.ToUpper()}"); // Strain name (positions in dots; scale from mm: e.g., 10 mm = 80 dots)
            ezpl.AppendLine($"AC,208,{labelXPos},1,1,0,1E,{type.ToUpper()} - {thc}% THC");
            ezpl.AppendLine($"AC,160,{labelXPos},1,1,0,1E,NET WEIGHT: {weight}g");
            ezpl.AppendLine($"AB,77,{labelXPos},1,1,0,1E,PACKAGED ON: {date} by {companyName}");
            ezpl.AppendLine($"AB,37,{labelXPos},1,1,0,1E,{companyAddress}");
            // QR code for hyperlink (position in dots; syntax corrected: mode=0 (normal QR), error correction=M (medium), mask=0 (auto), mul=1, rotate=0)
            ezpl.AppendLine($"W310,402,5,2,M5,8,6,{hyperlink.Length},1"); // x=200 dots (~25 mm), y=80 dots; adjust position to fit
            ezpl.AppendLine($"{hyperlink}");
            ezpl.AppendLine("E"); // End and print

            string ezplCommand = ezpl.ToString();

            // Attempt to print if printer name set
            var printerName = settings.Values["PrinterName"] as string;
            bool printed = false;
            if (!string.IsNullOrEmpty(printerName))
            {
                try
                {
                    RawPrinterHelper.SendStringToPrinter(printerName, ezplCommand);
                    printed = true;
                }
                catch (Exception ex)
                {
                    await ShowDialog("Print Error", $"Failed to print: {ex.Message}");
                }
            }

            /*            // Show mock-up dialog (always, or if not printed)
                        var mockContent = new StackPanel { Spacing = 5 };
                        mockContent.Children.Add(new TextBlock { Text = companyAddress });
                        mockContent.Children.Add(new TextBlock { Text = $"Strain: {strainName}" });
                        mockContent.Children.Add(new TextBlock { Text = $"Type: {type}" });Oka
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
            */
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