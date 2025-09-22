using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace DispensaryLabel
{
    public sealed partial class HomePage : Page
    {
        private List<Strain> strains = new List<Strain>();

        public HomePage()
        {
            this.InitializeComponent();
            this.Loaded += HomePage_Loaded;
        }

        // In HomePage.xaml.cs - Update LoadStrainsFromCsv to use the saved token/path

        private async void HomePage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            LoadStrainsFromCsv();
        }

        private async void LoadStrainsFromCsv()
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
                // Handle errors
                var dialog = new ContentDialog { Title = "Error", Content = ex.Message, CloseButtonText = "OK" };
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
        }

        private async Task<StorageFile> GetCsvFileAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var csvToken = settings.Values["CsvFileToken"] as string;

            if (!string.IsNullOrEmpty(csvToken) && StorageApplicationPermissions.FutureAccessList.ContainsItem(csvToken))
            {
                return await StorageApplicationPermissions.FutureAccessList.GetFileAsync(csvToken);
            }
            else
            {
                // Fallback to bundled default
                var folder = ApplicationData.Current.LocalFolder;
                StorageFile file;
                try
                {
                    file = await folder.GetFileAsync("strains.csv");
                }
                catch (FileNotFoundException)
                {
                    var uri = new Uri("ms-appx:///Assets/strains.csv");
                    var sourceFile = await StorageFile.GetFileFromApplicationUriAsync(uri);
                    file = await sourceFile.CopyAsync(folder, "strains.csv", NameCollisionOption.ReplaceExisting);
                }
                return file;
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
                        // Handle invalid URI: Set to null (disables navigation) and optionally update content
                        StrainHyperlink.NavigateUri = null;
                        StrainHyperlink.Content = $"{selectedStrain.Hyperlink} (Invalid URL)";
                        // Optionally show a dialog or log the error
                    }
                }
            }
        }


    }

    public class Strain
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Thc { get; set; }
        public string Hyperlink { get; set; }
    }
}