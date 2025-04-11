using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WoWServerManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Check for tessdata folder when application starts
            EnsureTessdataFolderExists();
        }

        private void EnsureTessdataFolderExists()
        {
            string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            if (!Directory.Exists(tessdataPath))
            {
                try
                {
                    Directory.CreateDirectory(tessdataPath);
                    MessageBox.Show(
                        "A 'tessdata' folder has been created. Please download the English language data file (eng.traineddata) and place it in this folder.",
                        "OCR Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create tessdata folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (!File.Exists(Path.Combine(tessdataPath, "eng.traineddata")))
            {
                MessageBox.Show(
                    "English language data file (eng.traineddata) not found in the tessdata folder. Character selection by OCR will not work correctly.",
                    "Missing OCR Data",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
