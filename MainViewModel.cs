using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Text.Json;
using System.Windows.Forms; // For SendKeys
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;

namespace WoWServerManager
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class MainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Server> _servers;
        private Server _selectedServer;
        private Expansion _selectedExpansion;
        private Account _selectedAccount;
        private readonly string _configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WoWServerManager",
            "config.json");

        public ObservableCollection<Server> Servers
        {
            get => _servers;
            set
            {
                _servers = value;
                OnPropertyChanged();
            }
        }

        public Server SelectedServer
        {
            get => _selectedServer;
            set
            {
                // Allow deselection by clicking the selected item again
                if (_selectedServer == value)
                {
                    _selectedServer = null;
                    // Also clear any dependent selections
                    SelectedExpansion = null;
                }
                else
                {
                    _selectedServer = value;
                    OnPropertyChanged(nameof(Expansions));
                    SelectedExpansion = Expansions.FirstOrDefault();
                }

                OnPropertyChanged();
            }
        }

        public ObservableCollection<Expansion> Expansions =>
            SelectedServer?.Expansions ?? new ObservableCollection<Expansion>();

        public Expansion SelectedExpansion
        {
            get => _selectedExpansion;
            set
            {
                // Allow deselection by clicking the selected item again
                if (_selectedExpansion == value)
                {
                    _selectedExpansion = null;
                    // Also clear any dependent selections
                    SelectedAccount = null;
                }
                else
                {
                    _selectedExpansion = value;
                    OnPropertyChanged(nameof(Accounts));
                    SelectedAccount = Accounts.FirstOrDefault();
                }

                OnPropertyChanged();
            }
        }

        public ObservableCollection<Account> Accounts =>
            SelectedExpansion?.Accounts ?? new ObservableCollection<Account>();

        public Account SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                // Allow deselection by clicking the selected item again
                if (_selectedAccount == value)
                {
                    _selectedAccount = null;
                }
                else
                {
                    _selectedAccount = value;
                }

                OnPropertyChanged();
            }
        }

        // Command Properties
        public ICommand AddServerCommand { get; }
        public ICommand EditServerCommand { get; }
        public ICommand RemoveServerCommand { get; }
        public ICommand AddExpansionCommand { get; }
        public ICommand EditExpansionCommand { get; }
        public ICommand RemoveExpansionCommand { get; }
        public ICommand AddAccountCommand { get; }
        public ICommand EditAccountCommand { get; }
        public ICommand RemoveAccountCommand { get; }
        public ICommand LaunchGameCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand ShowHowToUseCommand { get; }

        // Define these to prevent errors but disable functionality
        public ICommand CalibrateOcrCommand { get; }
        public ICommand TestCharacterSelectionCommand { get; }
        public ICommand GetCharacterRecommendationsCommand { get; }
        public ICommand VisualizeOcrResultsCommand { get; }
        public ICommand DebugOcrOverlayCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public MainViewModel()
        {
            LoadConfig();

            // Modified to use guided setup
            AddServerCommand = new RelayCommand(_ => AddServerWithGuidedSetup());

            // Keep the original commands for manual editing
            EditServerCommand = new RelayCommand(_ => EditServer(), _ => SelectedServer != null);
            RemoveServerCommand = new RelayCommand(_ => RemoveServer(), _ => SelectedServer != null);

            AddExpansionCommand = new RelayCommand(async _ => await AddExpansionWithAccounts(), _ => SelectedServer != null);
            EditExpansionCommand = new RelayCommand(_ => EditExpansion(), _ => SelectedExpansion != null);
            RemoveExpansionCommand = new RelayCommand(_ => RemoveExpansion(), _ => SelectedExpansion != null);

            AddAccountCommand = new RelayCommand(async _ => await AddMultipleAccounts(), _ => SelectedExpansion != null);
            EditAccountCommand = new RelayCommand(_ => EditAccount(), _ => SelectedAccount != null);
            RemoveAccountCommand = new RelayCommand(_ => RemoveAccount(), _ => SelectedAccount != null);

            LaunchGameCommand = new RelayCommand(_ => LaunchGame(), _ => SelectedExpansion != null && SelectedAccount != null);
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());

            ShowHowToUseCommand = new RelayCommand(_ => ShowHowToUse());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());

            // Disable all character-related functionality with informative messages
            string featureDisabledMsg = "Character selection features are disabled in this version.";
            CalibrateOcrCommand = new RelayCommand(_ => System.Windows.MessageBox.Show(featureDisabledMsg, "Feature Disabled", MessageBoxButton.OK, MessageBoxImage.Information));
            TestCharacterSelectionCommand = new RelayCommand(_ => System.Windows.MessageBox.Show(featureDisabledMsg, "Feature Disabled", MessageBoxButton.OK, MessageBoxImage.Information));
            GetCharacterRecommendationsCommand = new RelayCommand(_ => System.Windows.MessageBox.Show(featureDisabledMsg, "Feature Disabled", MessageBoxButton.OK, MessageBoxImage.Information));
            VisualizeOcrResultsCommand = new RelayCommand(_ => System.Windows.MessageBox.Show(featureDisabledMsg, "Feature Disabled", MessageBoxButton.OK, MessageBoxImage.Information));
            DebugOcrOverlayCommand = new RelayCommand(_ => System.Windows.MessageBox.Show(featureDisabledMsg, "Feature Disabled", MessageBoxButton.OK, MessageBoxImage.Information));
        }

        private void LoadConfig()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var options = new JsonSerializerOptions
                    {
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
                    };

                    Servers = JsonSerializer.Deserialize<ObservableCollection<Server>>(json, options) ?? new ObservableCollection<Server>();

                    // Fix circular references after deserialization
                    foreach (var server in Servers)
                    {
                        foreach (var expansion in server.Expansions)
                        {
                            expansion.Server = server;

                            foreach (var account in expansion.Accounts)
                            {
                                account.Expansion = expansion;
                            }
                        }
                    }
                }
                else
                {
                    Servers = new ObservableCollection<Server>();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Servers = new ObservableCollection<Server>();
            }
        }

        public void SaveConfig()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
                };

                var json = JsonSerializer.Serialize(Servers, options);
                File.WriteAllText(_configFilePath, json);
                System.Windows.MessageBox.Show("Configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddServer()
        {
            var dialog = new ServerDialog();
            if (dialog.ShowDialog() == true)
            {
                Servers.Add(dialog.Server);
                SelectedServer = dialog.Server;
                SaveConfig();
            }
        }

        private void EditServer()
        {
            if (SelectedServer == null) return;

            var dialog = new ServerDialog(SelectedServer);
            if (dialog.ShowDialog() == true)
            {
                // Only update the name property, not replace the entire Server object
                // This preserves all expansions and their accounts
                SelectedServer.Name = dialog.Server.Name;

                // Notify UI of changes
                OnPropertyChanged(nameof(Servers));
                SaveConfig();
            }
        }

        private void RemoveServer()
        {
            if (SelectedServer == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to remove the server '{SelectedServer.Name}'?",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Servers.Remove(SelectedServer);
                SelectedServer = Servers.FirstOrDefault();
                SaveConfig();
            }
        }


        private void AddExpansion()
        {
            if (SelectedServer == null) return;

            var dialog = new ExpansionDialog();
            if (dialog.ShowDialog() == true)
            {
                // Set the reference to the parent server
                dialog.Expansion.Server = SelectedServer;

                SelectedServer.Expansions.Add(dialog.Expansion);
                SelectedExpansion = dialog.Expansion;
                SaveConfig();
            }
        }

        private void EditExpansion()
        {
            if (SelectedExpansion == null) return;

            var dialog = new ExpansionDialog(SelectedExpansion);
            if (dialog.ShowDialog() == true)
            {
                // Only update the properties, not replace the entire Expansion object
                // This preserves all accounts and the server reference
                SelectedExpansion.Name = dialog.Expansion.Name;
                SelectedExpansion.LauncherPath = dialog.Expansion.LauncherPath;
                SelectedExpansion.LaunchDelayMs = dialog.Expansion.LaunchDelayMs;
                SelectedExpansion.CharacterSelectDelayMs = dialog.Expansion.CharacterSelectDelayMs;

                // Notify UI of changes
                OnPropertyChanged(nameof(Expansions));
                SaveConfig();
            }
        }

        private void RemoveExpansion()
        {
            if (SelectedExpansion == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to remove the expansion '{SelectedExpansion.Name}'?",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SelectedServer.Expansions.Remove(SelectedExpansion);
                SelectedExpansion = SelectedServer.Expansions.FirstOrDefault();
                SaveConfig();
            }
        }

        private void AddAccount()
        {
            if (SelectedExpansion == null) return;

            var dialog = new AccountDialog();
            if (dialog.ShowDialog() == true)
            {
                // Set the reference to the parent expansion
                dialog.Account.Expansion = SelectedExpansion;

                SelectedExpansion.Accounts.Add(dialog.Account);
                SelectedAccount = dialog.Account;
                SaveConfig();
            }
        }

        private void EditAccount()
        {
            if (SelectedAccount == null) return;

            var dialog = new AccountDialog(SelectedAccount);
            if (dialog.ShowDialog() == true)
            {
                // Update properties but preserve expansion reference
                SelectedAccount.Username = dialog.Account.Username;
                SelectedAccount.Password = dialog.Account.Password;

                OnPropertyChanged(nameof(Accounts));
                SaveConfig();
            }
        }

        private void RemoveAccount()
        {
            if (SelectedAccount == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to remove the account '{SelectedAccount.Username}'?",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SelectedExpansion.Accounts.Remove(SelectedAccount);
                SelectedAccount = SelectedExpansion.Accounts.FirstOrDefault();
                SaveConfig();
            }
        }

        private void ShowHowToUse()
        {
            // Create the How To Use window
            var howToUseWindow = new Window
            {
                Title = "How to Use WoW Server Manager",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            // Set the background similar to the main window
            howToUseWindow.Background = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/wow-background.jpg")),
                Opacity = 0.2,
                Stretch = Stretch.UniformToFill
            };

            // Create a scroll viewer for the content
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Margin = new Thickness(20)
            };

            // Create a main panel for content
            var mainPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(10)
            };
            scrollViewer.Content = mainPanel;

            // Add the title
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "HOW TO USE THE WOW SERVER MANAGER",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 20)
            });

            // Add a note about the simplified version
            var simplifiedNoteBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(255, 70, 70, 70)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 20),
                CornerRadius = new CornerRadius(3)
            };

            var simplifiedNotePanel = new System.Windows.Controls.StackPanel();
            simplifiedNoteBorder.Child = simplifiedNotePanel;

            // Add the info icon and text in a horizontal stack
            var infoHeaderPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            // Add the info icon
            infoHeaderPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "ℹ",
                FontSize = 18,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Add the header text
            infoHeaderPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "ABOUT THIS VERSION",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                VerticalAlignment = VerticalAlignment.Center
            });

            simplifiedNotePanel.Children.Add(infoHeaderPanel);

            // Add the message
            simplifiedNotePanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "This is a simplified version of WoW Server Manager that focuses on server, expansion, and account management. Character selection features are disabled in this version to ensure maximum compatibility with all Windows systems.",
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                Margin = new Thickness(0, 10, 0, 0)
            });

            mainPanel.Children.Add(simplifiedNoteBorder);

            // Add the sections explaining each part of the app with updated colors
            AddHowToUseSection(mainPanel, "1. SERVERS",
                "The Servers panel allows you to manage different WoW server connections. Each server can have multiple expansions.",
                System.Windows.Media.Color.FromArgb(255, 70, 70, 70),  // Dark gray background
                System.Windows.Media.Color.FromArgb(255, 255, 204, 0)); // Gold color

            AddHowToUseSection(mainPanel, "2. EXPANSIONS",
                "The Expansions panel lets you configure different WoW versions for each server. " +
                "You must specify the launcher path and can adjust login delays to accommodate your system's performance.",
                System.Windows.Media.Color.FromArgb(255, 70, 70, 70),
                System.Windows.Media.Color.FromArgb(255, 255, 204, 0));

            AddHowToUseSection(mainPanel, "3. ACCOUNTS",
                "The Accounts panel stores your login credentials for each expansion. " +
                "Your password is stored locally and used to automatically log you into the game.",
                System.Windows.Media.Color.FromArgb(255, 70, 70, 70),
                System.Windows.Media.Color.FromArgb(255, 255, 204, 0));

            AddHowToUseSection(mainPanel, "4. LAUNCHING THE GAME",
                "After setting up your server, expansion, and account, simply click the 'LAUNCH GAME' button. " +
                "The application will start the game client and automatically log you in with your stored credentials.",
                System.Windows.Media.Color.FromArgb(255, 70, 70, 70),
                System.Windows.Media.Color.FromArgb(255, 255, 204, 0));

            // Set the content and show the window
            howToUseWindow.Content = scrollViewer;
            howToUseWindow.ShowDialog();
        }

        // Helper method to add sections to the How To Use window
        private void AddHowToUseSection(System.Windows.Controls.StackPanel parent, string title, string content,
                                System.Windows.Media.Color backgroundColor, System.Windows.Media.Color borderColor)
        {
            var sectionBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(backgroundColor),
                BorderBrush = new System.Windows.Media.SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15),
                CornerRadius = new CornerRadius(3)
            };

            var sectionPanel = new System.Windows.Controls.StackPanel();
            sectionBorder.Child = sectionPanel;

            sectionPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(0, 0, 0, 10)
            });

            sectionPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = content,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
            });

            parent.Children.Add(sectionBorder);
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = Application.Current.MainWindow;
            settingsWindow.ShowDialog();
        }

        // Add the PropertyChanged event and OnPropertyChanged method
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void AddServerWithGuidedSetup()
        {
            // Step 1: Add Server
            var serverDialog = new ServerDialog();
            if (serverDialog.ShowDialog() != true)
                return; // User cancelled

            Servers.Add(serverDialog.Server);
            SelectedServer = serverDialog.Server;
            SaveConfig();

            // Step 2: Prompt to add expansion
            var addExpansionResult = System.Windows.MessageBox.Show(
                $"Would you like to add an expansion for server '{serverDialog.Server.Name}'?",
                "Add Expansion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (addExpansionResult == MessageBoxResult.Yes)
            {
                await AddExpansionWithAccounts();
            }
        }

        private async Task AddExpansionWithAccounts()
        {
            // Add expansion
            var expansionDialog = new ExpansionDialog();
            if (expansionDialog.ShowDialog() != true)
                return; // User cancelled

            // Set the reference to the parent server
            expansionDialog.Expansion.Server = SelectedServer;

            SelectedServer.Expansions.Add(expansionDialog.Expansion);
            SelectedExpansion = expansionDialog.Expansion;
            SaveConfig();

            // Continue adding accounts
            await AddMultipleAccounts();
        }

        private async Task AddMultipleAccounts()
        {
            bool continueAddingAccounts = true;

            while (continueAddingAccounts)
            {
                // Add an account
                var accountDialog = new AccountDialog();
                if (accountDialog.ShowDialog() != true)
                    break; // User cancelled

                // Set the reference to the parent expansion
                accountDialog.Account.Expansion = SelectedExpansion;

                SelectedExpansion.Accounts.Add(accountDialog.Account);
                SelectedAccount = accountDialog.Account;
                SaveConfig();

                // Ask if user wants to add another account
                var result = System.Windows.MessageBox.Show(
                    "Would you like to add another account for this expansion?",
                    "Add Another Account",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                continueAddingAccounts = (result == MessageBoxResult.Yes);
            }

            // After adding accounts, ask if they want to add another expansion
            var addAnotherExpansion = System.Windows.MessageBox.Show(
                "Would you like to add another expansion for this server?",
                "Add Another Expansion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (addAnotherExpansion == MessageBoxResult.Yes)
            {
                await AddExpansionWithAccounts();
            }
        }

        private async void LaunchGame()
        {
            if (SelectedExpansion == null || SelectedAccount == null)
            {
                System.Windows.MessageBox.Show("Please select an expansion and account first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                // Check if the launcher exists
                if (!File.Exists(SelectedExpansion.LauncherPath))
                {
                    System.Windows.MessageBox.Show($"The launcher at '{SelectedExpansion.LauncherPath}' does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // Launch the game
                var process = Process.Start(SelectedExpansion.LauncherPath);
                if (process == null)
                {
                    System.Windows.MessageBox.Show("Failed to start the game launcher.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // Wait for the game client to fully load
                await Task.Delay(SelectedExpansion.LaunchDelayMs);
                // Simulate keystrokes for login
                SendKeys.SendWait(SelectedAccount.Username);
                SendKeys.SendWait("{TAB}");
                SendKeys.SendWait(SelectedAccount.Password);
                SendKeys.SendWait("{ENTER}");

                string result = $"Game launched successfully with account: {SelectedAccount.Username}\n\n" +
                               "You will need to manually select your character.";

                System.Windows.MessageBox.Show(result, "Launch Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error launching game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetExpansionIconPath(string expansionName)
        {
            expansionName = expansionName.ToLower();

            return expansionName switch
            {
                string s when s.Contains("classic") => "/Resources/Icons/classic_icon.png",
                string s when s.Contains("burning crusade") => "/Resources/Icons/tbc_icon.png",
                string s when s.Contains("lich king") => "/Resources/Icons/wotlk_icon.png",
                string s when s.Contains("cataclysm") => "/Resources/Icons/cata_icon.png",
                string s when s.Contains("pandaria") => "/Resources/Icons/mop_icon.png",
                string s when s.Contains("draenor") => "/Resources/Icons/wod_icon.png",
                string s when s.Contains("legion") => "/Resources/Icons/legion_icon.png",
                string s when s.Contains("azeroth") => "/Resources/Icons/bfa_icon.png",
                string s when s.Contains("shadowlands") => "/Resources/Icons/shadowlands_icon.png",
                string s when s.Contains("dragonflight") => "/Resources/Icons/dragonflight_icon.png",
                _ => "/Resources/Icons/default_icon.png"
            };
        }

        public class Server : INotifyPropertyChanged
        {
            private string _name;
            private ObservableCollection<Expansion> _expansions;

            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<Expansion> Expansions
            {
                get => _expansions;
                set
                {
                    _expansions = value;
                    OnPropertyChanged();
                }
            }

            public Server()
            {
                Expansions = new ObservableCollection<Expansion>();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class Expansion : INotifyPropertyChanged
        {
            private string _name;
            private string _launcherPath;
            private string _iconPath; // For expansion icon
            private int _launchDelayMs = 5000;
            private int _characterSelectDelayMs = 8000;
            private ObservableCollection<Account> _accounts;
            private Server _server;

            public string IconPath
            {
                get => _iconPath;
                set
                {
                    _iconPath = value;
                    OnPropertyChanged();
                }
            }

            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }

            public string LauncherPath
            {
                get => _launcherPath;
                set
                {
                    _launcherPath = value;
                    OnPropertyChanged();
                }
            }

            public int LaunchDelayMs
            {
                get => _launchDelayMs;
                set
                {
                    _launchDelayMs = value;
                    OnPropertyChanged();
                }
            }

            public int CharacterSelectDelayMs
            {
                get => _characterSelectDelayMs;
                set
                {
                    _characterSelectDelayMs = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<Account> Accounts
            {
                get => _accounts;
                set
                {
                    _accounts = value;
                    OnPropertyChanged();
                }
            }

            [JsonIgnore] // Prevents circular reference during serialization
            public Server Server
            {
                get => _server;
                set
                {
                    _server = value;
                    OnPropertyChanged();
                }
            }

            public Expansion()
            {
                Accounts = new ObservableCollection<Account>();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class Account : INotifyPropertyChanged
        {
            private string _username;
            private string _password;
            private Expansion _expansion; // Reference to parent expansion

            public string Username
            {
                get => _username;
                set
                {
                    _username = value;
                    OnPropertyChanged();
                }
            }

            public string Password
            {
                get => _password;
                set
                {
                    _password = value;
                    OnPropertyChanged();
                }
            }

            [JsonIgnore] // Prevents circular reference during serialization
            public Expansion Expansion
            {
                get => _expansion;
                set
                {
                    _expansion = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Predicate<object> _canExecute;

            public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute == null || _canExecute(parameter);
            }

            public void Execute(object parameter)
            {
                _execute(parameter);
            }

            public event EventHandler CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }
        }

        // Dialog Classes
        public class ServerDialog : Window
        {
            public Server Server { get; private set; }

            public ServerDialog(Server existingServer = null)
            {
                // Set dialog properties and layout
                Title = existingServer == null ? "Add Server" : "Edit Server";
                Width = 400;
                Height = 150; // Reduced height as we only have one field now
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                // Create a new server object for the dialog
                // If we're editing, copy just the name (we'll update only this property)
                Server = new Server { Name = existingServer?.Name ?? string.Empty };

                // Create form layout
                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.Margin = new Thickness(10);

                // Name label and textbox
                var nameLabel = new System.Windows.Controls.Label { Content = "Server Name:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(nameLabel, 0);
                System.Windows.Controls.Grid.SetColumn(nameLabel, 0);

                var nameTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Server.Name };
                nameTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Name") { Source = Server, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
                System.Windows.Controls.Grid.SetRow(nameTextBox, 0);
                System.Windows.Controls.Grid.SetColumn(nameTextBox, 1);

                // Button panel
                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                System.Windows.Controls.Grid.SetRow(buttonPanel, 1);
                System.Windows.Controls.Grid.SetColumn(buttonPanel, 0);
                System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 2);

                var saveButton = new System.Windows.Controls.Button { Content = "Save", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
                saveButton.Click += (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(Server.Name))
                    {
                        System.Windows.MessageBox.Show("Server name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    DialogResult = true;
                };

                var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(cancelButton);

                grid.Children.Add(nameLabel);
                grid.Children.Add(nameTextBox);
                grid.Children.Add(buttonPanel);

                Content = grid;
            }
        }

        public class ExpansionDialog : Window
        {
            public Expansion Expansion { get; private set; }

            public ExpansionDialog(Expansion existingExpansion = null)
            {
                // Set dialog properties and layout
                Title = existingExpansion == null ? "Add Expansion" : "Edit Expansion";
                Width = 500;
                Height = 350; // Slightly increased height to accommodate dropdown
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                // Define WoW expansions
                string[] wowExpansions = {
                    "Classic",
                    "The Burning Crusade (TBC)",
                    "Wrath of the Lich King (WotLK)",
                    "Cataclysm",
                    "Mists of Pandaria (MoP)",
                    "Warlords of Draenor (WoD)",
                    "Legion",
                    "Battle for Azeroth (BfA)",
                    "Shadowlands",
                    "Dragonflight"
                };

                // Create a new expansion for dialog purposes
                // If editing, copy only the properties we want to modify
                Expansion = new Expansion
                {
                    Name = existingExpansion?.Name ?? string.Empty,
                    LauncherPath = existingExpansion?.LauncherPath ?? string.Empty,
                    LaunchDelayMs = existingExpansion?.LaunchDelayMs ?? 5000,
                    CharacterSelectDelayMs = existingExpansion?.CharacterSelectDelayMs ?? 8000
                };

                // Create form layout
                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                grid.Margin = new Thickness(10);

                // Name label and ComboBox (replacing the previous TextBox)
                var nameLabel = new System.Windows.Controls.Label { Content = "Expansion Name:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(nameLabel, 0);
                System.Windows.Controls.Grid.SetColumn(nameLabel, 0);

                var nameComboBox = new System.Windows.Controls.ComboBox
                {
                    Margin = new Thickness(5),
                    Style = System.Windows.Application.Current.Resources["WoWComboBoxStyle"] as Style,
                    IsEditable = false
                };

                // Populate the ComboBox with WoW expansions
                foreach (var expansion in wowExpansions)
                {
                    nameComboBox.Items.Add(expansion);
                }

                // Set the current value if editing an existing expansion
                if (!string.IsNullOrEmpty(Expansion.Name))
                {
                    nameComboBox.SelectedItem = wowExpansions.FirstOrDefault(e =>
                        e.IndexOf(Expansion.Name, StringComparison.OrdinalIgnoreCase) >= 0) ?? Expansion.Name;
                }

                // Update Expansion name when selection changes
                nameComboBox.SelectionChanged += (sender, args) =>
                {
                    Expansion.Name = nameComboBox.SelectedItem as string;

                    // Set the icon path based on the selected expansion
                    if (nameComboBox.SelectedItem is string selectedExpansion)
                    {
                        string iconPath = MainViewModel.GetExpansionIconPath(selectedExpansion);
                        Expansion.IconPath = iconPath;
                    }
                };

                System.Windows.Controls.Grid.SetRow(nameComboBox, 0);
                System.Windows.Controls.Grid.SetColumn(nameComboBox, 1);
                System.Windows.Controls.Grid.SetColumnSpan(nameComboBox, 2);

                // Launcher path label and textbox with browse button
                var pathLabel = new System.Windows.Controls.Label { Content = "Launcher Path:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(pathLabel, 1);
                System.Windows.Controls.Grid.SetColumn(pathLabel, 0);

                var pathTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Expansion.LauncherPath };
                pathTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("LauncherPath") { Source = Expansion, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
                System.Windows.Controls.Grid.SetRow(pathTextBox, 1);
                System.Windows.Controls.Grid.SetColumn(pathTextBox, 1);

                var browseButton = new System.Windows.Controls.Button { Content = "Browse", Width = 75, Margin = new Thickness(5) };
                browseButton.Click += (sender, args) =>
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "Executable Files|*.exe|All Files|*.*",
                        Title = "Select WoW Launcher"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        Expansion.LauncherPath = dialog.FileName;
                    }
                };
                System.Windows.Controls.Grid.SetRow(browseButton, 1);
                System.Windows.Controls.Grid.SetColumn(browseButton, 2);

                // Login delay label and textbox
                var loginDelayLabel = new System.Windows.Controls.Label { Content = "Login Delay (ms):", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(loginDelayLabel, 2);
                System.Windows.Controls.Grid.SetColumn(loginDelayLabel, 0);

                var loginDelayTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Expansion.LaunchDelayMs.ToString() };
                loginDelayTextBox.TextChanged += (sender, args) =>
                {
                    if (int.TryParse(loginDelayTextBox.Text, out int value))
                    {
                        Expansion.LaunchDelayMs = value;
                    }
                };
                System.Windows.Controls.Grid.SetRow(loginDelayTextBox, 2);
                System.Windows.Controls.Grid.SetColumn(loginDelayTextBox, 1);
                System.Windows.Controls.Grid.SetColumnSpan(loginDelayTextBox, 2);

                // Character select delay label and textbox
                var charSelectDelayLabel = new System.Windows.Controls.Label { Content = "Character Select Delay (ms):", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(charSelectDelayLabel, 3);
                System.Windows.Controls.Grid.SetColumn(charSelectDelayLabel, 0);

                var charSelectDelayTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Expansion.CharacterSelectDelayMs.ToString() };
                charSelectDelayTextBox.TextChanged += (sender, args) =>
                {
                    if (int.TryParse(charSelectDelayTextBox.Text, out int value))
                    {
                        Expansion.CharacterSelectDelayMs = value;
                    }
                };
                System.Windows.Controls.Grid.SetRow(charSelectDelayTextBox, 3);
                System.Windows.Controls.Grid.SetColumn(charSelectDelayTextBox, 1);
                System.Windows.Controls.Grid.SetColumnSpan(charSelectDelayTextBox, 2);

                // Button panel
                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                System.Windows.Controls.Grid.SetRow(buttonPanel, 4);
                System.Windows.Controls.Grid.SetColumn(buttonPanel, 0);
                System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 3);

                var saveButton = new System.Windows.Controls.Button { Content = "Save", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
                saveButton.Click += (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(Expansion.Name))
                    {
                        System.Windows.MessageBox.Show("Expansion name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Expansion.LauncherPath))
                    {
                        System.Windows.MessageBox.Show("Launcher path is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    DialogResult = true;
                };

                var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(cancelButton);

                grid.Children.Add(nameLabel);
                grid.Children.Add(nameComboBox);
                grid.Children.Add(pathLabel);
                grid.Children.Add(pathTextBox);
                grid.Children.Add(browseButton);
                grid.Children.Add(loginDelayLabel);
                grid.Children.Add(loginDelayTextBox);
                grid.Children.Add(charSelectDelayLabel);
                grid.Children.Add(charSelectDelayTextBox);
                grid.Children.Add(buttonPanel);

                Content = grid;
            }
        }

        public class AccountDialog : Window
        {
            public Account Account { get; private set; }

            public AccountDialog(Account existingAccount = null)
            {
                // Set dialog properties and layout
                Title = existingAccount == null ? "Add Account" : "Edit Account";
                Width = 400;
                Height = 220; // Increased for additional information
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                Account = existingAccount != null
                    ? new Account { Username = existingAccount.Username, Password = existingAccount.Password }
                    : new Account();

                // Create form layout
                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.Margin = new Thickness(10);

                // Username label and textbox
                var usernameLabel = new System.Windows.Controls.Label { Content = "Username:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(usernameLabel, 0);
                System.Windows.Controls.Grid.SetColumn(usernameLabel, 0);

                var usernameTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Account.Username };
                usernameTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Username") { Source = Account, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
                System.Windows.Controls.Grid.SetRow(usernameTextBox, 0);
                System.Windows.Controls.Grid.SetColumn(usernameTextBox, 1);

                // Password label and textbox
                var passwordLabel = new System.Windows.Controls.Label { Content = "Password:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(passwordLabel, 1);
                System.Windows.Controls.Grid.SetColumn(passwordLabel, 0);

                var passwordBox = new System.Windows.Controls.PasswordBox { Margin = new Thickness(5) };
                passwordBox.Password = Account.Password;
                passwordBox.PasswordChanged += (sender, args) => Account.Password = passwordBox.Password;
                System.Windows.Controls.Grid.SetRow(passwordBox, 1);
                System.Windows.Controls.Grid.SetColumn(passwordBox, 1);

                // Information text
                var infoText = new System.Windows.Controls.TextBlock
                {
                    Text = "After adding this account, you'll have the option to add more accounts for the same expansion.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5, 10, 5, 10),
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontStyle = FontStyles.Italic
                };
                System.Windows.Controls.Grid.SetRow(infoText, 2);
                System.Windows.Controls.Grid.SetColumn(infoText, 0);
                System.Windows.Controls.Grid.SetColumnSpan(infoText, 2);

                // Button panel
                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                System.Windows.Controls.Grid.SetRow(buttonPanel, 3);
                System.Windows.Controls.Grid.SetColumn(buttonPanel, 0);
                System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 2);

                var saveButton = new System.Windows.Controls.Button { Content = "Save", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
                saveButton.Click += (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(Account.Username))
                    {
                        System.Windows.MessageBox.Show("Username is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(Account.Password))
                    {
                        System.Windows.MessageBox.Show("Password is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    DialogResult = true;
                };
                var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };
                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(cancelButton);

                grid.Children.Add(usernameLabel);
                grid.Children.Add(usernameTextBox);
                grid.Children.Add(passwordLabel);
                grid.Children.Add(passwordBox);
                grid.Children.Add(infoText);
                grid.Children.Add(buttonPanel);

                Content = grid;
            }
        }
    }
}
