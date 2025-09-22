using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
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
                scaleTimer.Interval = TimeSpan.FromSeconds(1); // Poll every 1 second
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
                            WeightTextBox.Text = weight.ToString("F3"); // Format to 3 decimal places or as needed
                        }
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
    }
}