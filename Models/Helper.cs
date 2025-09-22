// New file: Helper.cs (add to project root)
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace DispensaryLabel
{
    public static class Helper
    {
        public static async Task<StorageFile> GetCsvFileAsync()
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
    }
}