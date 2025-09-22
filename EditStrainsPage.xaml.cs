// Update EditStrainsPage.xaml.cs to use Helper.GetCsvFileAsync()
// (Replace the entire class content with this)

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using Windows.Storage;

namespace DispensaryLabel
{
    public sealed partial class EditStrainsPage : Page
    {
        public EditStrainsPage()
        {
            this.InitializeComponent();
            LoadCsvForEditing();
        }

        private async void LoadCsvForEditing()
        {
            try
            {
                var file = await Helper.GetCsvFileAsync();
                var content = await FileIO.ReadTextAsync(file);
                CsvEditor.Text = content;
            }
            catch
            {
                // Handle file not found or errors (e.g., show a dialog)
            }
        }

        private async void SaveCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await Helper.GetCsvFileAsync();
                await FileIO.WriteTextAsync(file, CsvEditor.Text);
                // Optionally show confirmation
            }
            catch
            {
                // Handle errors
            }
        }
    }
}