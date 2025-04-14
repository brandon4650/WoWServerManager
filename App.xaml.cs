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
        // Constructor with no OCR initialization
        public App()
        {
            // No OCR initialization needed
        }

        // Remove the EnsureTessdataFolderExists() method entirely
    }
}