using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DispensaryLabel
{
    public sealed partial class EditStrainsPage : Page
    {
        private List<Strain> strains = new List<Strain>();
        private Strain currentStrain; // Track the currently selected strain for editing

        public EditStrainsPage()
        {
            this.InitializeComponent();
            this.Loaded += EditStrainsPage_Loaded;
        }

        private async void EditStrainsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadStrainsAsync();
        }

        private async System.Threading.Tasks.Task LoadStrainsAsync()
        {
            try
            {
                var file = await Helper.GetCsvFileAsync();
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
                await ShowDialog("Error", $"Failed to load strains: {ex.Message}");
            }
        }

        private void StrainComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrainComboBox.SelectedItem is string selectedName)
            {
                currentStrain = strains.FirstOrDefault(s => s.Name == selectedName);
                if (currentStrain != null)
                {
                    NameTextBox.Text = currentStrain.Name;
                    TypeComboBox.SelectedItem = currentStrain.Type;
                    ThcTextBox.Text = currentStrain.Thc;
                    HyperlinkTextBox.Text = currentStrain.Hyperlink;
                    EditPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private async void AddNew_Click(object sender, RoutedEventArgs e)
        {
            // Clear fields for new strain
            currentStrain = null;
            NameTextBox.Text = string.Empty;
            TypeComboBox.SelectedItem = null;
            ThcTextBox.Text = string.Empty;
            HyperlinkTextBox.Text = string.Empty;
            EditPanel.Visibility = Visibility.Visible;
            StrainComboBox.SelectedItem = null;
        }

        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                await ShowDialog("Error", "Strain name is required.");
                return;
            }

            var newStrain = new Strain
            {
                Name = NameTextBox.Text.Trim(),
                Type = TypeComboBox.SelectedItem as string ?? string.Empty,
                Thc = ThcTextBox.Text.Trim(),
                Hyperlink = HyperlinkTextBox.Text.Trim()
            };

            // If editing existing, remove old and add new (handles name changes)
            if (currentStrain != null)
            {
                strains.Remove(currentStrain);
            }

            // Check for duplicate names
            if (strains.Any(s => s.Name.Equals(newStrain.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await ShowDialog("Error", "A strain with this name already exists.");
                return;
            }

            strains.Add(newStrain);

            // Save to CSV
            await SaveStrainsToCsvAsync();

            // Refresh ComboBox
            StrainComboBox.ItemsSource = strains.Select(s => s.Name).ToList();
            StrainComboBox.SelectedItem = newStrain.Name;
            currentStrain = newStrain;

            await ShowDialog("Success", "Strain saved successfully.");
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (currentStrain == null)
            {
                await ShowDialog("Error", "No strain selected to delete.");
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = "Confirm Delete",
                Content = $"Are you sure you want to delete '{currentStrain.Name}'?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            };
            confirmDialog.XamlRoot = this.XamlRoot;
            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                strains.Remove(currentStrain);
                await SaveStrainsToCsvAsync();

                // Refresh
                StrainComboBox.ItemsSource = strains.Select(s => s.Name).ToList();
                StrainComboBox.SelectedItem = null;
                EditPanel.Visibility = Visibility.Collapsed;

                await ShowDialog("Success", "Strain deleted.");
            }
        }

        private async System.Threading.Tasks.Task SaveStrainsToCsvAsync()
        {
            try
            {
                var file = await Helper.GetCsvFileAsync();
                var lines = strains.Select(s => $"{s.Name},{s.Type},{s.Thc},{s.Hyperlink}");
                await FileIO.WriteLinesAsync(file, lines);
            }
            catch (Exception ex)
            {
                await ShowDialog("Error", $"Failed to save strains: {ex.Message}");
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