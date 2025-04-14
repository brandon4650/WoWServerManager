using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WoWServerManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void OnServerButtonClick(object sender, RoutedEventArgs e)
        {
            ServerListBox.Focus();
        }

        private void OnExpansionButtonClick(object sender, RoutedEventArgs e)
        {
            ExpansionListBox.Focus();
        }

        private void OnAccountButtonClick(object sender, RoutedEventArgs e)
        {
            AccountListBox.Focus();
        }

        private void OnCharacterButtonClick(object sender, RoutedEventArgs e)
        {
            CharacterListBox.Focus();
        }
    }
}