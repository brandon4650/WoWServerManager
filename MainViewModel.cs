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

using System.Windows.Input;
// Explicitly using WPF MessageBox
using MessageBox = System.Windows.MessageBox;
using System.Text.Json.Serialization;
using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
using Point = System.Drawing.Point; // Resolves Point ambiguity
using ImageFormat = System.Drawing.Imaging.ImageFormat; // Resolves ImageFormat ambiguity

using DrawingImage = System.Drawing.Image;
using WpfImage = System.Windows.Controls.Image;
using MediaColor = System.Windows.Media.Color;
using DrawingColor = System.Drawing.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using DrawingColorConverter = System.Drawing.ColorConverter;
// EmguCV namespaces
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using DrawingSize = System.Drawing.Size;
using WpfApplication = System.Windows.Application;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Text.RegularExpressions;
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
        private ObservableCollection<Character> _characters => SelectedAccount?.Characters ?? new ObservableCollection<Character>();
        private Character _selectedCharacter;
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
                    _selectedAccount = value;
                    // Also clear any dependent selections
                    SelectedCharacter = null;
                }
                else
                {
                    _selectedAccount = value;
                    OnPropertyChanged(nameof(Characters));
                    // Don't auto-select a character - let the user choose explicitly
                    SelectedCharacter = null;
                }

                OnPropertyChanged();
            }
        }

        public ObservableCollection<Character> Characters => _characters;

        public Character SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                _selectedCharacter = value;

                // Also update the selected character in the account
                if (SelectedAccount != null)
                {
                    SelectedAccount.SelectedCharacter = value;
                }

                OnPropertyChanged();
            }
        }

        // Add these commands to MainViewModel
        public ICommand AddCharacterCommand { get; }
        public ICommand EditCharacterCommand { get; }
        public ICommand RemoveCharacterCommand { get; }

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

        public ICommand DebugOcrOverlayCommand { get; }

        public ICommand CalibrateOcrCommand { get; }
        public ICommand TestCharacterSelectionCommand { get; }
        public ICommand GetCharacterRecommendationsCommand { get; }
        public ICommand VisualizeOcrResultsCommand { get; }


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

            AddCharacterCommand = new RelayCommand(_ => AddCharacter(), _ => SelectedAccount != null);
            EditCharacterCommand = new RelayCommand(_ => EditCharacter(), _ => SelectedCharacter != null);
            RemoveCharacterCommand = new RelayCommand(_ => RemoveCharacter(), _ => SelectedCharacter != null);


            LaunchGameCommand = new RelayCommand(_ => LaunchGame(), _ => SelectedExpansion != null && SelectedAccount != null);
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());

            ShowHowToUseCommand = new RelayCommand(_ => ShowHowToUse());

            DebugOcrOverlayCommand = new RelayCommand(_ => VisualizeSelectionArea());

            CalibrateOcrCommand = new RelayCommand(_ => OptimizeOcrParameters());
            TestCharacterSelectionCommand = new RelayCommand(_ => TestCharacterSelection(), _ => SelectedAccount != null);
            GetCharacterRecommendationsCommand = new RelayCommand(_ => GetCharacterDetectionRecommendations());
            VisualizeOcrResultsCommand = new RelayCommand(_ => VisualizeOcrResults());
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

                                // Fix character references too
                                foreach (var character in account.Characters)
                                {
                                    character.Account = account;
                                }
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
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void VisualizeSelectionArea()
        {
            try
            {
                // Get screen bounds
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;

                // Start with coordinates focused on character list
                int captureX = 2010;
                int captureY = 62;
                int captureWidth = 380;
                int captureHeight = 1031;

                // Add debugging output
                Console.WriteLine($"Setting capture area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}");
                // Also add MessageBox to confirm values are being set
                MessageBox.Show($"Setting capture coordinates to: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}");

                // Ensure the capture area stays within screen bounds
                if (captureX + captureWidth > screenBounds.Width)
                {
                    captureWidth = screenBounds.Width - captureX - 5;
                }

                if (captureY + captureHeight > screenBounds.Height)
                {
                    captureHeight = screenBounds.Height - captureY - 5;
                }

                // Create a visualization window with precise positioning
                var visualWindow = new Window
                {
                    Title = "OCR Debug Overlay",
                    Width = screenBounds.Width,
                    Height = screenBounds.Height,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)),
                    Topmost = true,
                    Left = 0,
                    Top = 0
                };

                // Create a canvas to draw on
                var canvas = new System.Windows.Controls.Canvas();
                visualWindow.Content = canvas;

                // Draw the OCR area rectangle
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = captureWidth,
                    Height = captureHeight,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
                    StrokeThickness = 3,
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 0, 0))
                };
                System.Windows.Controls.Canvas.SetLeft(rect, captureX);
                System.Windows.Controls.Canvas.SetTop(rect, captureY);
                canvas.Children.Add(rect);

                // Add text label
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "OCR Capture Area",
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold
                };
                System.Windows.Controls.Canvas.SetLeft(textBlock, captureX);
                System.Windows.Controls.Canvas.SetTop(textBlock, captureY - 20);
                canvas.Children.Add(textBlock);

                // Add detailed debug info
                var debugInfoBlock = new System.Windows.Controls.TextBlock
                {
                    Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}",
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Lime),
                    FontSize = 14,
                    FontWeight = FontWeights.Normal
                };
                System.Windows.Controls.Canvas.SetLeft(debugInfoBlock, 10);
                System.Windows.Controls.Canvas.SetTop(debugInfoBlock, 50);
                canvas.Children.Add(debugInfoBlock);

                // Add navigation buttons to help adjust the position
                // Left button
                var leftButton = new System.Windows.Controls.Button
                {
                    Content = "◀",
                    Width = 40,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                leftButton.Click += (s, e) => {
                    captureX -= 50;
                    System.Windows.Controls.Canvas.SetLeft(rect, captureX);
                    System.Windows.Controls.Canvas.SetLeft(textBlock, captureX);
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(leftButton, 10);
                System.Windows.Controls.Canvas.SetTop(leftButton, 90);
                canvas.Children.Add(leftButton);

                // Right button
                var rightButton = new System.Windows.Controls.Button
                {
                    Content = "▶",
                    Width = 40,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                rightButton.Click += (s, e) => {
                    captureX += 50;
                    System.Windows.Controls.Canvas.SetLeft(rect, captureX);
                    System.Windows.Controls.Canvas.SetLeft(textBlock, captureX);
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(rightButton, 60);
                System.Windows.Controls.Canvas.SetTop(rightButton, 90);
                canvas.Children.Add(rightButton);

                // Up button
                var upButton = new System.Windows.Controls.Button
                {
                    Content = "▲",
                    Width = 40,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                upButton.Click += (s, e) => {
                    captureY -= 50;
                    System.Windows.Controls.Canvas.SetTop(rect, captureY);
                    System.Windows.Controls.Canvas.SetTop(textBlock, captureY - 20);
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(upButton, 110);
                System.Windows.Controls.Canvas.SetTop(upButton, 90);
                canvas.Children.Add(upButton);

                // Down button
                var downButton = new System.Windows.Controls.Button
                {
                    Content = "▼",
                    Width = 40,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                downButton.Click += (s, e) => {
                    captureY += 50;
                    System.Windows.Controls.Canvas.SetTop(rect, captureY);
                    System.Windows.Controls.Canvas.SetTop(textBlock, captureY - 20);
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(downButton, 160);
                System.Windows.Controls.Canvas.SetTop(downButton, 90);
                canvas.Children.Add(downButton);

                // Wider button (increase width)
                var widerButton = new System.Windows.Controls.Button
                {
                    Content = "Wider",
                    Width = 60,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                widerButton.Click += (s, e) => {
                    captureWidth += 20;
                    rect.Width = captureWidth;
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(widerButton, 210);
                System.Windows.Controls.Canvas.SetTop(widerButton, 90);
                canvas.Children.Add(widerButton);

                // Narrower button (decrease width)
                var narrowerButton = new System.Windows.Controls.Button
                {
                    Content = "Narrower",
                    Width = 70,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                narrowerButton.Click += (s, e) => {
                    if (captureWidth > 50)
                    { // Don't go too narrow
                        captureWidth -= 20;
                        rect.Width = captureWidth;
                        debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                    }
                };
                System.Windows.Controls.Canvas.SetLeft(narrowerButton, 280);
                System.Windows.Controls.Canvas.SetTop(narrowerButton, 90);
                canvas.Children.Add(narrowerButton);

                // Taller button (increase height)
                var tallerButton = new System.Windows.Controls.Button
                {
                    Content = "Taller",
                    Width = 60,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                tallerButton.Click += (s, e) => {
                    captureHeight += 20;
                    rect.Height = captureHeight;
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(tallerButton, 360);
                System.Windows.Controls.Canvas.SetTop(tallerButton, 90);
                canvas.Children.Add(tallerButton);

                // Shorter button (decrease height)
                var shorterButton = new System.Windows.Controls.Button
                {
                    Content = "Shorter",
                    Width = 60,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                shorterButton.Click += (s, e) => {
                    if (captureHeight > 50)
                    { // Don't go too short
                        captureHeight -= 20;
                        rect.Height = captureHeight;
                        debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                    }
                };
                System.Windows.Controls.Canvas.SetLeft(shorterButton, 430);
                System.Windows.Controls.Canvas.SetTop(shorterButton, 90);
                canvas.Children.Add(shorterButton);

                // Precise left/right movement
                var minorLeftButton = new System.Windows.Controls.Button
                {
                    Content = "◀ 10px",
                    Width = 60,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                minorLeftButton.Click += (s, e) => {
                    captureX -= 10;
                    System.Windows.Controls.Canvas.SetLeft(rect, captureX);
                    System.Windows.Controls.Canvas.SetLeft(textBlock, captureX);
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(minorLeftButton, 10);
                System.Windows.Controls.Canvas.SetTop(minorLeftButton, 130);
                canvas.Children.Add(minorLeftButton);

                var minorRightButton = new System.Windows.Controls.Button
                {
                    Content = "▶ 10px",
                    Width = 60,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow),
                    BorderThickness = new Thickness(1)
                };
                minorRightButton.Click += (s, e) => {
                    captureX += 10;
                    System.Windows.Controls.Canvas.SetLeft(rect, captureX);
                    System.Windows.Controls.Canvas.SetLeft(textBlock, captureX);
                    debugInfoBlock.Text = $"Screen: {screenBounds.Width}x{screenBounds.Height}\nCapture Area: X={captureX}, Y={captureY}, W={captureWidth}, H={captureHeight}";
                };
                System.Windows.Controls.Canvas.SetLeft(minorRightButton, 80);
                System.Windows.Controls.Canvas.SetTop(minorRightButton, 130);
                canvas.Children.Add(minorRightButton);

                // Save coordinates button
                var saveButton = new System.Windows.Controls.Button
                {
                    Content = "Save Coordinates",
                    Width = 150,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green),
                    BorderThickness = new Thickness(1)
                };
                saveButton.Click += (s, e) => {
                    MessageBox.Show($"Use these coordinates in your code:\n\nint captureX = {captureX};\nint captureY = {captureY};\nint captureWidth = {captureWidth};\nint captureHeight = {captureHeight};",
                        "OCR Coordinates", MessageBoxButton.OK, MessageBoxImage.Information);
                };
                System.Windows.Controls.Canvas.SetLeft(saveButton, 150);
                System.Windows.Controls.Canvas.SetTop(saveButton, 130);
                canvas.Children.Add(saveButton);

                // Close button
                var closeButton = new System.Windows.Controls.Button
                {
                    Content = "Close Overlay",
                    Width = 120,
                    Height = 30,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
                    BorderThickness = new Thickness(1)
                };
                closeButton.Click += (s, e) => visualWindow.Close();
                System.Windows.Controls.Canvas.SetLeft(closeButton, 10);
                System.Windows.Controls.Canvas.SetTop(closeButton, 10);
                canvas.Children.Add(closeButton);

                // Show the window
                visualWindow.Show();

                // Auto-close after 5 minutes for extensive testing
                var timer = new System.Threading.Timer((_) =>
                {
                    WpfApplication.Current.Dispatcher.Invoke(() =>
                    {
                        visualWindow.Close();
                    });
                }, null, 300000, System.Threading.Timeout.Infinite);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error visualizing selection area: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var result = MessageBox.Show(
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

                // Notify UI of changes
                OnPropertyChanged(nameof(Expansions));
                SaveConfig();
            }
        }

        private void RemoveExpansion()
        {
            if (SelectedExpansion == null) return;

            var result = MessageBox.Show(
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
                var index = SelectedExpansion.Accounts.IndexOf(SelectedAccount);

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

            var result = MessageBox.Show(
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

        // Add these methods to the MainViewModel class
        private void AddCharacter()
        {
            if (SelectedAccount == null) return;

            var dialog = new CharacterDialog();
            if (dialog.ShowDialog() == true)
            {
                // Set the reference to the parent account
                dialog.Character.Account = SelectedAccount;

                SelectedAccount.Characters.Add(dialog.Character);
                SelectedCharacter = dialog.Character;
                SaveConfig();
            }
        }

        private void EditCharacter()
        {
            if (SelectedCharacter == null) return;

            var dialog = new CharacterDialog(SelectedCharacter);
            if (dialog.ShowDialog() == true)
            {
                // Only update the properties, not replace the entire Character object
                SelectedCharacter.Name = dialog.Character.Name;
                SelectedCharacter.Realm = dialog.Character.Realm;
                SelectedCharacter.Class = dialog.Character.Class;
                SelectedCharacter.Level = dialog.Character.Level;

                // Notify UI of changes
                OnPropertyChanged(nameof(Characters));
                SaveConfig();
            }
        }

        private void RemoveCharacter()
        {
            if (SelectedCharacter == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to remove the character '{SelectedCharacter.Name}'?",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SelectedAccount.Characters.Remove(SelectedCharacter);
                SelectedCharacter = SelectedAccount.Characters.FirstOrDefault();
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

            // Add a warning about character selection
            var warningBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(255, 70, 70, 70)), // Darker gray matching screenshot
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 20),
                CornerRadius = new CornerRadius(3)
            };

            var warningPanel = new System.Windows.Controls.StackPanel();
            warningBorder.Child = warningPanel;

            // Add the warning icon and text in a horizontal stack
            var warningHeaderPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            // Add the triangle warning icon
            warningHeaderPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "⚠",
                FontSize = 18,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Add the warning header text
            warningHeaderPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "IMPORTANT NOTE",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                VerticalAlignment = VerticalAlignment.Center
            });

            warningPanel.Children.Add(warningHeaderPanel);

            // Add the warning message
            warningPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Character selection is a work in progress and may not always be accurate. It is NOT required to add characters to use this application. You can simply input your server, expansion, and account details to use the login features.",
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                Margin = new Thickness(0, 10, 0, 0)
            });

            mainPanel.Children.Add(warningBorder);

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

            AddHowToUseSection(mainPanel, "4. CHARACTERS (OPTIONAL)",
                "The Characters panel is OPTIONAL and allows you to store information about your characters. " +
                "The character selection feature attempts to automatically select your character after login, " +
                "but this feature may not always work perfectly. You can use the application without adding any characters.",
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
            var addExpansionResult = MessageBox.Show(
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
                var result = MessageBox.Show(
                    "Would you like to add another account for this expansion?",
                    "Add Another Account",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                continueAddingAccounts = (result == MessageBoxResult.Yes);
            }

            // After adding accounts, ask if they want to add another expansion
            var addAnotherExpansion = MessageBox.Show(
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
                MessageBox.Show("Please select an expansion and account first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                // Check if the launcher exists
                if (!File.Exists(SelectedExpansion.LauncherPath))
                {
                    MessageBox.Show($"The launcher at '{SelectedExpansion.LauncherPath}' does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // Launch the game
                var process = Process.Start(SelectedExpansion.LauncherPath);
                if (process == null)
                {
                    MessageBox.Show("Failed to start the game launcher.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // Wait for the game client to fully load
                await Task.Delay(SelectedExpansion.LaunchDelayMs);
                // Simulate keystrokes for login
                SendKeys.SendWait(SelectedAccount.Username);
                SendKeys.SendWait("{TAB}");
                SendKeys.SendWait(SelectedAccount.Password);
                SendKeys.SendWait("{ENTER}");
                // Wait for character selection screen
                await Task.Delay(SelectedExpansion.CharacterSelectDelayMs);

                string result = $"Game launched successfully with account: {SelectedAccount.Username}";

                // Only attempt character selection if a character is explicitly selected
                if (SelectedAccount.SelectedCharacter != null)
                {
                    bool selectionSuccessful = await SelectCharacterByName(SelectedAccount.SelectedCharacter.Name);

                    if (selectionSuccessful)
                    {
                        result += $"\nCharacter '{SelectedAccount.SelectedCharacter.Name}' was selected.";
                    }
                    else
                    {
                        result += $"\nAttempted to select character '{SelectedAccount.SelectedCharacter.Name}' but could not confirm selection.";
                    }
                }
                else
                {
                    // No character selection was attempted
                    result += "\nNo character was selected in the application.";
                }

                MessageBox.Show(result, "Launch Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VisualizeOcrResults()
        {
            // Similar to the Test Character Selection but with focus on OCR output visualization
            MessageBox.Show(
                "This will visualize what the OCR engine sees when attempting to detect characters.\n\n" +
                "Please make sure your WoW client is open with the character selection screen visible.",
                "Visualize OCR Results",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            try
            {
                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug", "Visualization");
                if (!Directory.Exists(debugDir))
                    Directory.CreateDirectory(debugDir);

                // Take a screenshot
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string originalPath = Path.Combine(debugDir, $"ocr_original_{timestamp}.png");
                string processedPath = Path.Combine(debugDir, $"ocr_processed_{timestamp}.png");
                string highlightPath = Path.Combine(debugDir, $"ocr_highlight_{timestamp}.png");
                string resultPath = Path.Combine(debugDir, $"ocr_result_{timestamp}.txt");

                // Capture the character list area
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
                int captureX = 2010;
                int captureY = 62;
                int captureWidth = 380;
                int captureHeight = 1031;

                // Ensure the capture area stays within screen bounds
                if (captureX + captureWidth > screenBounds.Width)
                {
                    captureWidth = screenBounds.Width - captureX - 5;
                }

                if (captureY + captureHeight > screenBounds.Height)
                {
                    captureHeight = screenBounds.Height - captureY - 5;
                }

                Rectangle captureBounds = new Rectangle(captureX, captureY, captureWidth, captureHeight);

                // Capture the screen area
                using (Bitmap bitmap = new Bitmap(captureBounds.Width, captureBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(
                            new Point(captureBounds.Left, captureBounds.Top),
                            Point.Empty,
                            captureBounds.Size);
                    }

                    bitmap.Save(originalPath, ImageFormat.Png);
                }

                // Process the image with EmguCV
                using (Image<Bgr, byte> image = new Image<Bgr, byte>(originalPath))
                {
                    // Try to detect the highlighted row
                    Rectangle? highlightedArea = DetectHighlightedRow(image);

                    // Draw a rectangle around the highlighted area if found
                    if (highlightedArea.HasValue)
                    {
                        Rectangle roi = highlightedArea.Value;
                        // Create a copy of the original image with a highlighted rectangle
                        using (Image<Bgr, byte> highlightedImage = image.Clone())
                        {
                            // Draw a red rectangle around the highlighted area
                            MCvScalar redColor = new MCvScalar(0, 0, 255); // BGR format
                            CvInvoke.Rectangle(highlightedImage, roi, redColor, 3);
                            highlightedImage.Save(highlightPath);

                            // Crop to the highlighted area
                            using (Image<Bgr, byte> croppedImage = image.Copy(roi))
                            {
                                // Process the cropped image
                                var ocrProcessedImage = PreprocessImageWithEmguCV(croppedImage);
                                ocrProcessedImage.Save(processedPath);

                                // Perform OCR
                                string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                                using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                                {
                                    engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.' ");
                                    engine.SetVariable("tessedit_pageseg_mode", "6");
                                    engine.SetVariable("tessedit_ocr_engine_mode", "2");
                                    engine.SetVariable("language_model_penalty_non_dict_word", "0.1");
                                    engine.SetVariable("language_model_penalty_case", "0.1");

                                    using (var img = ConvertEmguCvImageToPix(ocrProcessedImage))
                                    {
                                        using (var page = engine.Process(img, PageSegMode.SingleBlock))
                                        {
                                            string result = page.GetText().Trim();
                                            File.WriteAllText(resultPath, result);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // No highlight found, process the entire image
                        var ocrProcessedImage = PreprocessImageWithEmguCV(image);
                        ocrProcessedImage.Save(processedPath);

                        // Perform OCR on the whole image
                        string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                        using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                        {
                            engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.' ");
                            engine.SetVariable("tessedit_pageseg_mode", "6");
                            engine.SetVariable("tessedit_ocr_engine_mode", "2");
                            engine.SetVariable("language_model_penalty_non_dict_word", "0.1");
                            engine.SetVariable("language_model_penalty_case", "0.1");

                            using (var img = ConvertEmguCvImageToPix(ocrProcessedImage))
                            {
                                using (var page = engine.Process(img, PageSegMode.SingleBlock))
                                {
                                    string result = page.GetText().Trim();
                                    File.WriteAllText(resultPath, result);
                                }
                            }
                        }
                    }
                }

                // Show a visualization window with the results
                var visualWindow = new Window
                {
                    Title = "OCR Visualization Results",
                    Width = 1000,
                    Height = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.Margin = new Thickness(20);

                // Header
                var header = new System.Windows.Controls.TextBlock
                {
                    Text = "OCR VISUALIZATION RESULTS",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                System.Windows.Controls.Grid.SetRow(header, 0);
                System.Windows.Controls.Grid.SetColumnSpan(header, 2);
                grid.Children.Add(header);

                // Create a panel for the images
                var imagesPanel = new System.Windows.Controls.Grid();
                imagesPanel.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                imagesPanel.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                imagesPanel.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                imagesPanel.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                imagesPanel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                imagesPanel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                System.Windows.Controls.Grid.SetRow(imagesPanel, 1);
                System.Windows.Controls.Grid.SetColumn(imagesPanel, 0);

                // Labels and images
                var originalLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "Original Capture",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(5)
                };
                System.Windows.Controls.Grid.SetRow(originalLabel, 0);
                System.Windows.Controls.Grid.SetColumn(originalLabel, 0);
                imagesPanel.Children.Add(originalLabel);

                // Original image
                var originalImage = new System.Windows.Controls.Image
                {
                    Margin = new Thickness(5),
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly
                };
                var originalBitmap = new BitmapImage();
                originalBitmap.BeginInit();
                originalBitmap.UriSource = new Uri(originalPath, UriKind.Absolute);
                originalBitmap.CacheOption = BitmapCacheOption.OnLoad;
                originalBitmap.EndInit();
                originalImage.Source = originalBitmap;
                System.Windows.Controls.Grid.SetRow(originalImage, 1);
                System.Windows.Controls.Grid.SetColumn(originalImage, 0);
                imagesPanel.Children.Add(originalImage);

                // Highlighted image (if available)
                if (File.Exists(highlightPath))
                {
                    var highlightedLabel = new System.Windows.Controls.TextBlock
                    {
                        Text = "Highlighted Row Detection",
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(5)
                    };
                    System.Windows.Controls.Grid.SetRow(highlightedLabel, 0);
                    System.Windows.Controls.Grid.SetColumn(highlightedLabel, 1);
                    imagesPanel.Children.Add(highlightedLabel);

                    var highlightedImage = new System.Windows.Controls.Image
                    {
                        Margin = new Thickness(5),
                        Stretch = Stretch.Uniform,
                        StretchDirection = StretchDirection.DownOnly
                    };
                    var highlightBitmap = new BitmapImage();
                    highlightBitmap.BeginInit();
                    highlightBitmap.UriSource = new Uri(highlightPath, UriKind.Absolute);
                    highlightBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    highlightBitmap.EndInit();
                    highlightedImage.Source = highlightBitmap;
                    System.Windows.Controls.Grid.SetRow(highlightedImage, 1);
                    System.Windows.Controls.Grid.SetColumn(highlightedImage, 1);
                    imagesPanel.Children.Add(highlightedImage);
                }

                // Processed image
                var processedLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "Processed for OCR",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(5)
                };
                System.Windows.Controls.Grid.SetRow(processedLabel, 2);
                System.Windows.Controls.Grid.SetColumn(processedLabel, 0);
                imagesPanel.Children.Add(processedLabel);

                var processedImage = new System.Windows.Controls.Image
                {
                    Margin = new Thickness(5),
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly
                };
                var processedBitmap = new BitmapImage();
                processedBitmap.BeginInit();
                processedBitmap.UriSource = new Uri(processedPath, UriKind.Absolute);
                processedBitmap.CacheOption = BitmapCacheOption.OnLoad;
                processedBitmap.EndInit();
                processedImage.Source = processedBitmap;
                System.Windows.Controls.Grid.SetRow(processedImage, 3);
                System.Windows.Controls.Grid.SetColumn(processedImage, 0);
                imagesPanel.Children.Add(processedImage);

                // OCR Results panel
                var resultsPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
                System.Windows.Controls.Grid.SetRow(resultsPanel, 1);
                System.Windows.Controls.Grid.SetColumn(resultsPanel, 1);

                // OCR Result heading
                resultsPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "OCR RESULTS",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // OCR Result text
                string ocrText = File.Exists(resultPath) ? File.ReadAllText(resultPath) : "No OCR results available";
                var ocrTextBox = new System.Windows.Controls.TextBox
                {
                    Text = ocrText,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Height = 300,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 30)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                resultsPanel.Children.Add(ocrTextBox);

                // Character matching section
                if (SelectedAccount != null && SelectedAccount.Characters.Count > 0)
                {
                    resultsPanel.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text = "CHARACTER MATCHING",
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    // Create a grid for character matching
                    var matchGrid = new System.Windows.Controls.Grid();
                    matchGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    matchGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // Headers
                    matchGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                    var charHeader = new System.Windows.Controls.TextBlock
                    {
                        Text = "Character",
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(5)
                    };
                    System.Windows.Controls.Grid.SetRow(charHeader, 0);
                    System.Windows.Controls.Grid.SetColumn(charHeader, 0);
                    matchGrid.Children.Add(charHeader);

                    var scoreHeader = new System.Windows.Controls.TextBlock
                    {
                        Text = "Match Score",
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(5)
                    };
                    System.Windows.Controls.Grid.SetRow(scoreHeader, 0);
                    System.Windows.Controls.Grid.SetColumn(scoreHeader, 1);
                    matchGrid.Children.Add(scoreHeader);

                    // Add each character's match score
                    int row = 1;
                    Character bestMatch = null;
                    double bestScore = 0;

                    foreach (var character in SelectedAccount.Characters)
                    {
                        matchGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                        // Calculate match score
                        double score = CalculateCharacterMatchScore(ocrText, character);
                        bool hasExactMatch = ContainsExactCharacterName(ocrText, character.Name);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMatch = character;
                        }

                        // Character name
                        var nameText = new System.Windows.Controls.TextBlock
                        {
                            Text = $"{character.Name} (Lvl {character.Level} {character.Class})",
                            Foreground = new SolidColorBrush(Colors.White),
                            Margin = new Thickness(5)
                        };
                        System.Windows.Controls.Grid.SetRow(nameText, row);
                        System.Windows.Controls.Grid.SetColumn(nameText, 0);
                        matchGrid.Children.Add(nameText);

                        // Score
                        SolidColorBrush scoreBrush;
                        if (score >= 0.85)
                            scoreBrush = new SolidColorBrush(Colors.LightGreen);
                        else if (score >= 0.65)
                            scoreBrush = new SolidColorBrush(Colors.Yellow);
                        else
                            scoreBrush = new SolidColorBrush(Colors.Red);

                        var scoreText = new System.Windows.Controls.TextBlock
                        {
                            Text = $"{score:P0}{(hasExactMatch ? " (Exact Name)" : "")}",
                            Foreground = scoreBrush,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(5)
                        };
                        System.Windows.Controls.Grid.SetRow(scoreText, row);
                        System.Windows.Controls.Grid.SetColumn(scoreText, 1);
                        matchGrid.Children.Add(scoreText);

                        row++;
                    }

                    // Add to a border
                    var matchBorder = new System.Windows.Controls.Border
                    {
                        BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    matchBorder.Child = matchGrid;
                    resultsPanel.Children.Add(matchBorder);

                    // Best match summary
                    if (bestMatch != null)
                    {
                        SolidColorBrush bestMatchBrush;
                        if (bestScore >= 0.85)
                            bestMatchBrush = new SolidColorBrush(Colors.LightGreen);
                        else if (bestScore >= 0.65)
                            bestMatchBrush = new SolidColorBrush(Colors.Yellow);
                        else
                            bestMatchBrush = new SolidColorBrush(Colors.Red);

                        var bestMatchText = new System.Windows.Controls.TextBlock
                        {
                            Text = $"Best Match: {bestMatch.Name} ({bestScore:P0})",
                            Foreground = bestMatchBrush,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 0, 15)
                        };
                        resultsPanel.Children.Add(bestMatchText);
                    }
                }

                grid.Children.Add(imagesPanel);
                grid.Children.Add(resultsPanel);

                // Add a close button
                var closeButton = new System.Windows.Controls.Button
                {
                    Content = "Close",
                    Width = 120,
                    Height = 30,
                    Margin = new Thickness(0, 15, 0, 0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Style = System.Windows.Application.Current.Resources["WoWButtonStyle"] as Style
                };
                closeButton.Click += (sender, args) => visualWindow.Close();
                System.Windows.Controls.Grid.SetRow(closeButton, 2);
                System.Windows.Controls.Grid.SetColumnSpan(closeButton, 2);
                grid.Children.Add(closeButton);

                visualWindow.Content = grid;
                visualWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error visualizing OCR results: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GetCharacterDetectionRecommendations()
        {
            // Create a recommendations window
            var recommendationsWindow = new Window
            {
                Title = "Character Detection Recommendations",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var mainPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

            // Header
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "IMPROVING CHARACTER DETECTION",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Introduction
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Character detection using OCR can be challenging due to the visual complexity of World of Warcraft's interface. " +
                       "Here are detailed recommendations to improve detection accuracy:",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Recommendations
            AddRecommendationSection(mainPanel, "1. CHARACTER NAMES",
                "• Character names should match EXACTLY what appears in-game\n" +
                "• Avoid special characters in names that may confuse OCR\n" +
                "• The current system is most accurate with names 4-12 characters long\n" +
                "• Names with unique spelling are easier to detect than common words"
            );

            AddRecommendationSection(mainPanel, "2. CHARACTER POSITIONING",
                "• Position your preferred character at the top of the character list if possible\n" +
                "• Characters that appear in the middle of the list are easier to detect than those at the bottom\n" +
                "• Character highlighting (the gold/yellow selection) significantly improves detection\n" +
                "• Consider reordering characters in-game to match your selection preferences"
            );

            AddRecommendationSection(mainPanel, "3. INTERFACE SETTINGS",
                "• Use the default UI font and size for best OCR results\n" +
                "• Disable any addons that modify the character selection screen\n" +
                "• Higher resolution displays generally provide better OCR results\n" +
                "• If using an ultrawide monitor, use the 'Overlay Debug' to adjust the capture area"
            );

            AddRecommendationSection(mainPanel, "4. CHARACTER DETAILS",
                "• Include accurate level and class information for each character\n" +
                "• Class names should match what appears in-game (e.g., 'Death Knight' not 'DK')\n" +
                "• Realm names should match exactly what appears in the character list\n" +
                "• When adding new characters, launch the game first to confirm exact spelling"
            );

            AddRecommendationSection(mainPanel, "5. TROUBLESHOOTING",
                "• Use 'Test Selection' to check detection without launching the game\n" +
                "• Use 'OCR Analysis' to see what text is being detected from the screen\n" +
                "• Use 'Calibrate OCR' if you're having persistent issues\n" +
                "• If automatic selection fails, you can always manually select your character"
            );

            // Add a note about the current algorithm
            var algorithmNote = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(MediaColor.FromRgb(40, 40, 40)),
                BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 15, 0, 15)
            };

            algorithmNote.Child = new System.Windows.Controls.TextBlock
            {
                Text = "NOTE: The character selection algorithm has been significantly enhanced to now detect highlighted rows, " +
                      "match exact character names, and use fuzzy matching when exact matches aren't found. These improvements " +
                      "should make character selection much more reliable.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White)
            };

            mainPanel.Children.Add(algorithmNote);

            // Close button
            var closeButton = new System.Windows.Controls.Button
            {
                Content = "Close",
                Width = 120,
                Height = 30,
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Style = System.Windows.Application.Current.Resources["WoWButtonStyle"] as Style
            };
            closeButton.Click += (sender, args) => recommendationsWindow.Close();
            mainPanel.Children.Add(closeButton);

            // Add the panel to a scroll viewer
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            };
            scrollViewer.Content = mainPanel;

            recommendationsWindow.Content = scrollViewer;
            recommendationsWindow.ShowDialog();
        }

        private void AddRecommendationSection(System.Windows.Controls.StackPanel parent, string title, string content)
        {
            // Section title
            parent.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(0, 10, 0, 5)
            });

            // Section content
            var contentBorder = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            contentBorder.Child = new System.Windows.Controls.TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White)
            };

            parent.Children.Add(contentBorder);
        }

        private async Task<bool> SelectCharacterByName(string characterName)
        {
            const int maxAttempts = 15;
            const double highConfidenceThreshold = 0.85;
            const double mediumConfidenceThreshold = 0.65;
            const int initialDelay = 1500;
            const int navigationDelay = 600;

            if (SelectedAccount?.SelectedCharacter == null)
            {
                // If no character is selected, just press enter on whatever is highlighted
                SendKeys.SendWait("{ENTER}");
                return false;
            }

            Character target = SelectedAccount.SelectedCharacter;
            Console.WriteLine($"Searching for: {target.Name} (Lvl {target.Level} {target.Class})");

            // Give WoW time to fully load the character selection screen
            await Task.Delay(initialDelay);

            // First, take a screenshot to analyze what characters are visible
            string initialScreenText = CaptureScreenText();
            Console.WriteLine($"Initial screen text:\n{initialScreenText}");

            // Check if target character name appears in the initial screen
            bool targetIsVisible = initialScreenText.IndexOf(target.Name, StringComparison.OrdinalIgnoreCase) >= 0;

            // Start at the top of the character list
            SendKeys.SendWait("{HOME}");
            await Task.Delay(1200);

            // Store best match info
            double bestScore = 0;
            int bestPosition = -1;
            string bestMatchText = "";
            List<(int position, double score, string text)> matches = new List<(int, double, string)>();

            // First, check if the currently selected character (highlighted) is the one we want
            string currentHighlightText = CaptureScreenText();
            double currentScore = CalculateCharacterMatchScore(currentHighlightText, target);
            bool exactNameMatch = ContainsExactCharacterName(currentHighlightText, target.Name);

            Console.WriteLine($"Initial highlighted character: Score {currentScore:F2}, Exact match: {exactNameMatch}\nText: {currentHighlightText}");

            // If the currently selected character is a good match or has exact name match, use it immediately
            if (currentScore >= highConfidenceThreshold || exactNameMatch)
            {
                Console.WriteLine($"Initial character is a match - selecting");
                SendKeys.SendWait("{ENTER}");
                await Task.Delay(1000);
                return true;
            }

            // Otherwise, search through the list
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Take multiple samples at each position for better accuracy
                double positionScore = 0;
                string combinedText = "";
                bool foundExactName = false;

                // Take 2 samples at each position to improve reliability
                for (int sample = 0; sample < 2; sample++)
                {
                    string ocrText = CaptureScreenText();

                    // Check for exact character name match first
                    if (ContainsExactCharacterName(ocrText, target.Name))
                    {
                        foundExactName = true;
                        positionScore = 1.0;
                        combinedText = ocrText;
                        break; // No need for second sample if we found an exact match
                    }

                    double score = CalculateCharacterMatchScore(ocrText, target);

                    positionScore = Math.Max(positionScore, score);
                    if (!string.IsNullOrWhiteSpace(ocrText))
                    {
                        combinedText += ocrText + " ";
                    }

                    // Brief delay between samples
                    await Task.Delay(200);
                }

                // Trim any extra whitespace
                combinedText = combinedText.Trim();

                Console.WriteLine($"Pos {attempt}: Score {positionScore:F2}, Exact match: {foundExactName}\nText: {combinedText}");

                // Add to matches list
                matches.Add((attempt, positionScore, combinedText));

                // Update best match
                if (positionScore > bestScore)
                {
                    bestScore = positionScore;
                    bestPosition = attempt;
                    bestMatchText = combinedText;
                }

                // If we found an exact name match, select immediately
                if (foundExactName)
                {
                    Console.WriteLine($"Exact name match found at position {attempt} - selecting");
                    SendKeys.SendWait("{ENTER}");
                    await Task.Delay(1000);
                    return true;
                }

                // High confidence - select immediately
                if (positionScore >= highConfidenceThreshold)
                {
                    Console.WriteLine($"High confidence match found at position {attempt} - selecting");
                    SendKeys.SendWait("{ENTER}");
                    await Task.Delay(1000);
                    return true;
                }

                // Move to next character
                SendKeys.SendWait("{DOWN}");
                await Task.Delay(navigationDelay);
            }

            // Log debug information for troubleshooting
            LogCharacterSelectionDebug(target, matches);

            // Fallback to best match if reasonable
            if (bestScore >= mediumConfidenceThreshold && bestPosition >= 0)
            {
                Console.WriteLine($"Best match (Score {bestScore:F2}) at position {bestPosition}");
                Console.WriteLine($"Match text: {bestMatchText}");

                // Return to top
                SendKeys.SendWait("{HOME}");
                await Task.Delay(1000);

                // Navigate to best position
                for (int i = 0; i < bestPosition; i++)
                {
                    SendKeys.SendWait("{DOWN}");
                    await Task.Delay(400);
                }

                SendKeys.SendWait("{ENTER}");
                await Task.Delay(1000);
                return true;
            }

            // Final fallback - try with just the names
            var nameOnlyMatch = matches.OrderByDescending(m =>
                CalculateNameOnlyMatchScore(m.text, target.Name)).FirstOrDefault();

            if (nameOnlyMatch.score > 0.5) // Only use if we have a reasonable match
            {
                Console.WriteLine($"Name-only match found at position {nameOnlyMatch.position}");

                // Return to top
                SendKeys.SendWait("{HOME}");
                await Task.Delay(1000);

                // Navigate to matched position
                for (int i = 0; i < nameOnlyMatch.position; i++)
                {
                    SendKeys.SendWait("{DOWN}");
                    await Task.Delay(400);
                }

                SendKeys.SendWait("{ENTER}");
                await Task.Delay(1000);
                return true;
            }

            // Ultimate fallback - just press enter on whatever is selected
            Console.WriteLine("No good match found - selecting current character");
            SendKeys.SendWait("{ENTER}");
            return false;
        }

        // New helper method to check for exact character name match
        private bool ContainsExactCharacterName(string text, string characterName)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(characterName))
                return false;

            // Convert to lowercase for case-insensitive comparison
            string textLower = text.ToLower();
            string nameLower = characterName.ToLower();

            // Method 1: Direct contains check with word boundary check
            if (textLower.Contains(nameLower))
            {
                // Try to verify it's a complete name by checking for boundaries
                // Check for spaces, punctuation, or start/end of text around the name
                int nameIndex = textLower.IndexOf(nameLower);
                bool leftBoundary = nameIndex == 0 || char.IsWhiteSpace(textLower[nameIndex - 1]) || char.IsPunctuation(textLower[nameIndex - 1]);
                bool rightBoundary = nameIndex + nameLower.Length == textLower.Length ||
                                  char.IsWhiteSpace(textLower[nameIndex + nameLower.Length]) ||
                                  char.IsPunctuation(textLower[nameIndex + nameLower.Length]);

                if (leftBoundary || rightBoundary)
                    return true;
            }

            // Method 2: Check for name with word boundaries using regex
            string pattern = $@"\b{Regex.Escape(nameLower)}\b";
            if (Regex.IsMatch(textLower, pattern, RegexOptions.IgnoreCase))
                return true;

            // Method 3: Check for character name followed by common patterns in character selection screen
            string[] postfixPatterns = {
        "level", "lvl", "lv",
        "warrior", "paladin", "hunter", "rogue", "priest", "death knight",
        "shaman", "mage", "warlock", "monk", "druid", "demon hunter", "evoker"
    };

            foreach (var postfix in postfixPatterns)
            {
                // Check for "CharacterName Level" or "CharacterName Mage" patterns
                if (textLower.Contains($"{nameLower} {postfix}"))
                    return true;
            }

            return false;
        }

        private double CalculateNameOnlyMatchScore(string text, string targetName)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetName))
                return 0;

            // Simple case - exact name in the text
            if (text.ToLower().Contains(targetName.ToLower()))
                return 1.0;

            // Check for name parts
            double score = 0;
            string[] parts = targetName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Length >= 3 && text.ToLower().Contains(part.ToLower()))
                {
                    score += 0.5 / parts.Length;
                }
            }

            return score;
        }


        // Helper method to determine the better match when considering class and level
        private bool TryScoringClassAndLevel(string text1, string text2, Character target, out int betterPosition)
        {
            betterPosition = 0; // Default to first option

            if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
                return false;

            bool text1HasClass = !string.IsNullOrEmpty(target.Class) &&
                                text1.ToLower().Contains(target.Class.ToLower());
            bool text2HasClass = !string.IsNullOrEmpty(target.Class) &&
                                text2.ToLower().Contains(target.Class.ToLower());

            bool text1HasLevel = text1.Contains($"Level {target.Level}") ||
                                text1.Contains($"Lvl {target.Level}");
            bool text2HasLevel = text2.Contains($"Level {target.Level}") ||
                                text2.Contains($"Lvl {target.Level}");

            // Calculate a simple score based on class and level matches
            int score1 = (text1HasClass ? 2 : 0) + (text1HasLevel ? 1 : 0);
            int score2 = (text2HasClass ? 2 : 0) + (text2HasLevel ? 1 : 0);

            if (score1 != score2)
            {
                betterPosition = score1 > score2 ? 0 : 1;
                return true;
            }

            return false;
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

        private async void TestCharacterSelection()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MessageBox.Show("This feature requires Windows.", "Platform Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (SelectedAccount == null)
            {
                MessageBox.Show("Please select an account with characters first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SelectedAccount.Characters.Count == 0)
            {
                MessageBox.Show("The selected account has no characters. Please add characters first.", "No Characters", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Create a directory for test files if it doesn't exist
                string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug", "Tests");
                if (!Directory.Exists(testDir))
                    Directory.CreateDirectory(testDir);

                // First, check if we have any recent screenshots to use
                DirectoryInfo ocrDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug"));
                var recentFiles = ocrDir.GetFiles("ocr_capture_*.png", SearchOption.AllDirectories)
                                       .OrderByDescending(f => f.LastWriteTime)
                                       .Take(3)
                                       .ToArray();

                if (recentFiles.Length > 0)
                {
                    var result = MessageBox.Show(
                        $"Do you want to use existing screenshots for testing?\n\nFound {recentFiles.Length} recent screenshot(s).",
                        "Use Existing Screenshots?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Use the most recent file
                        TestCharacterDetectionWithFile(recentFiles[0].FullName);
                        return;
                    }
                }

                // If no recent files or user wants a new screenshot
                MessageBox.Show(
                    "This will attempt to test character selection using a new screenshot.\n\n" +
                    "Please make sure your WoW client is open with the character selection screen visible.",
                    "Test Character Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Take a screenshot of the character list area
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string screenshotPath = Path.Combine(testDir, $"test_selection_{timestamp}.png");

                // Capture the character list area
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;

                // Use exact coordinates from the screenshot - already verified as correct
                int captureX = 2010;
                int captureY = 62;
                int captureWidth = 380;
                int captureHeight = 1031;

                // Ensure the capture area stays within screen bounds
                if (captureX + captureWidth > screenBounds.Width)
                {
                    captureWidth = screenBounds.Width - captureX - 5;
                }

                if (captureY + captureHeight > screenBounds.Height)
                {
                    captureHeight = screenBounds.Height - captureY - 5;
                }

                Rectangle captureBounds = new Rectangle(captureX, captureY, captureWidth, captureHeight);

                // Capture the screen area
                using (Bitmap bitmap = new Bitmap(captureBounds.Width, captureBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(
                            new Point(captureBounds.Left, captureBounds.Top),
                            Point.Empty,
                            captureBounds.Size);
                    }

                    bitmap.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);
                }

                // Test detection with the screenshot
                TestCharacterDetectionWithFile(screenshotPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during character selection test: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestCharacterDetectionWithFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Screenshot file not found: {filePath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Start with sample simulated OCR text
            string ocrText = CaptureScreenTextFromImage(filePath);

            // Create result window to show matches
            var resultsWindow = new Window
            {
                Title = "Character Selection Test Results",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var mainPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

            // Header
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "CHARACTER SELECTION TEST RESULTS",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(0, 0, 0, 20)
            });

            // OCR Text section
            var ocrPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            ocrPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "DETECTED TEXT FROM SCREENSHOT:",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White)
            });

            var ocrTextBox = new System.Windows.Controls.TextBox
            {
                Text = ocrText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 150,
                Margin = new Thickness(0, 5, 0, 0),
                Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                BorderThickness = new Thickness(1)
            };
            ocrPanel.Children.Add(ocrTextBox);
            mainPanel.Children.Add(ocrPanel);

            // Matches section
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "CHARACTER MATCHING RESULTS:",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var grid = new System.Windows.Controls.Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Headers
            var characterHeader = new System.Windows.Controls.TextBlock
            {
                Text = "Character",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(5)
            };
            System.Windows.Controls.Grid.SetColumn(characterHeader, 0);
            grid.Children.Add(characterHeader);

            var detailsHeader = new System.Windows.Controls.TextBlock
            {
                Text = "Details",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(5)
            };
            System.Windows.Controls.Grid.SetColumn(detailsHeader, 1);
            grid.Children.Add(detailsHeader);

            var scoreHeader = new System.Windows.Controls.TextBlock
            {
                Text = "Match Score",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(5)
            };
            System.Windows.Controls.Grid.SetColumn(scoreHeader, 2);
            grid.Children.Add(scoreHeader);

            var matchResultHeader = new System.Windows.Controls.TextBlock
            {
                Text = "Result",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                Margin = new Thickness(5)
            };
            System.Windows.Controls.Grid.SetColumn(matchResultHeader, 3);
            grid.Children.Add(matchResultHeader);

            // Add row definitions for the header and each character
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            // Process each character
            int row = 1;
            bool anyGoodMatches = false;
            Character bestMatch = null;
            double bestScore = 0;

            foreach (var character in SelectedAccount.Characters)
            {
                // Add a row for this character
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                // Calculate match score
                double score = CalculateCharacterMatchScore(ocrText, character);
                bool hasExactNameMatch = ContainsExactCharacterName(ocrText, character.Name);

                // Update best match
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = character;
                }

                // Character name
                var nameCell = new System.Windows.Controls.TextBlock
                {
                    Text = character.Name,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(5)
                };
                System.Windows.Controls.Grid.SetRow(nameCell, row);
                System.Windows.Controls.Grid.SetColumn(nameCell, 0);
                grid.Children.Add(nameCell);

                // Character details
                var detailsCell = new System.Windows.Controls.TextBlock
                {
                    Text = $"Lvl {character.Level} {character.Class}\n{character.Realm}",
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(5)
                };
                System.Windows.Controls.Grid.SetRow(detailsCell, row);
                System.Windows.Controls.Grid.SetColumn(detailsCell, 1);
                grid.Children.Add(detailsCell);

                // Match score - using appropriate color for the score
                var scoreCell = new System.Windows.Controls.TextBlock
                {
                    Text = $"{score:P0}{(hasExactNameMatch ? " (Exact Name)" : "")}",
                    Foreground = score >= 0.85 ? new SolidColorBrush(Colors.LightGreen) :
                              (score >= 0.65 ? new SolidColorBrush(Colors.Yellow) : new SolidColorBrush(Colors.Red)),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5)
                };
                System.Windows.Controls.Grid.SetRow(scoreCell, row);
                System.Windows.Controls.Grid.SetColumn(scoreCell, 2);
                grid.Children.Add(scoreCell);

                // Result text and color
                string resultText;
                SolidColorBrush resultColor;

                if (score >= 0.85 || hasExactNameMatch)
                {
                    resultText = "STRONG MATCH";
                    resultColor = new SolidColorBrush(Colors.LightGreen);
                    anyGoodMatches = true;
                }
                else if (score >= 0.65)
                {
                    resultText = "POSSIBLE MATCH";
                    resultColor = new SolidColorBrush(Colors.Yellow);
                    anyGoodMatches = true;
                }
                else
                {
                    resultText = "WEAK MATCH";
                    resultColor = new SolidColorBrush(Colors.Red);
                }

                var resultCell = new System.Windows.Controls.TextBlock
                {
                    Text = resultText,
                    Foreground = resultColor,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5)
                };
                System.Windows.Controls.Grid.SetRow(resultCell, row);
                System.Windows.Controls.Grid.SetColumn(resultCell, 3);
                grid.Children.Add(resultCell);

                row++;
            }

            // Add the grid to the main panel
            var border = new System.Windows.Controls.Border
            {
                BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 20)
            };
            border.Child = grid;
            mainPanel.Children.Add(border);

            // Summary and recommendations
            var summaryPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 20) };

            summaryPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "SUMMARY:",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 5)
            });

            string summaryText;

            if (anyGoodMatches)
            {
                summaryText = $"Character selection should work with your current setup. The best match is " +
                         $"'{bestMatch.Name}' with a {bestScore:P0} confidence score.";
            }
            else
            {
                summaryText = "Character detection is not reliable with your current setup. Please follow the recommendations below.";
            }

            summaryPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = summaryText,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap
            });

            mainPanel.Children.Add(summaryPanel);

            // Recommendations
            mainPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "RECOMMENDATIONS:",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var recommendationsList = new System.Windows.Controls.ListBox
            {
                Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FFCC00")),
                BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(Colors.White)
            };

            recommendationsList.Items.Add("Make sure character names match exactly what appears in the game.");
            recommendationsList.Items.Add("Add the realm names exactly as they appear in the character selection screen.");
            recommendationsList.Items.Add("For best results, position your characters in order from top to bottom in the character list.");
            recommendationsList.Items.Add("Try using the 'Calibrate OCR' tool to optimize detection for your screen.");
            recommendationsList.Items.Add("Check that your screen resolution matches the capture area coordinates.");
            recommendationsList.Items.Add("If using an ultrawide monitor, you may need to use 'Overlay Debug' to adjust the capture area.");

            mainPanel.Children.Add(recommendationsList);

            // Add a scroll viewer for the content
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            };
            scrollViewer.Content = mainPanel;

            // Set the window content and show it
            resultsWindow.Content = scrollViewer;
            resultsWindow.ShowDialog();
        }


        private string CaptureScreenTextFromImage(string imagePath)
        {
            try
            {
                // Load the image with EmguCV
                using (Image<Bgr, byte> image = new Image<Bgr, byte>(imagePath))
                {
                    // Try to detect the highlighted row (gold/yellow selection)
                    Rectangle? highlightedArea = DetectHighlightedRow(image);

                    Image<Bgr, byte> regionOfInterest;

                    if (highlightedArea.HasValue)
                    {
                        // If we found a highlighted area, crop to that area
                        Rectangle roi = highlightedArea.Value;
                        using (Image<Bgr, byte> highlightedImage = image.Copy(roi))
                        {
                            regionOfInterest = highlightedImage.Clone();
                        }
                    }
                    else
                    {
                        // If no highlight detected, use the entire image
                        regionOfInterest = image.Clone();
                    }

                    // Process image with EmguCV for better OCR results
                    var ocrProcessedImage = PreprocessImageWithEmguCV(regionOfInterest);

                    // Debug: Save the processed image
                    string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug");
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string processedPath = Path.Combine(debugDir, $"test_processed_{timestamp}.png");
                    ocrProcessedImage.Save(processedPath);

                    // Perform OCR with Tesseract
                    string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                    string result = "";

                    using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                    {
                        // Configure Tesseract for game text - expand whitelist for better recognition
                        engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.' ");
                        engine.SetVariable("tessedit_pageseg_mode", "6"); // Assume a single uniform block of text
                        engine.SetVariable("tessedit_ocr_engine_mode", "2"); // LSTM only

                        // Add specific game text optimization
                        engine.SetVariable("language_model_penalty_non_dict_word", "0.1"); // Lower penalty for non-dictionary words (character names)
                        engine.SetVariable("language_model_penalty_case", "0.1"); // Lower penalty for case issues

                        // Convert EmguCV image to a format Tesseract can use
                        using (var img = ConvertEmguCvImageToPix(ocrProcessedImage))
                        {
                            using (var page = engine.Process(img, PageSegMode.SingleBlock))
                            {
                                result = page.GetText().Trim();
                            }
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                return $"Error processing image: {ex.Message}";
            }
        }

        private string CaptureScreenText()
        {
            string debugDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug");
            string debugImagePath = "";
            string debugTextPath = "";

            try
            {
                // Create debug directory if it doesn't exist
                if (!Directory.Exists(debugDirectory))
                {
                    Directory.CreateDirectory(debugDirectory);
                }

                // Get screen bounds
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;

                // Use exact coordinates from the screenshot - already verified as correct
                int captureX = 2010;
                int captureY = 62;
                int captureWidth = 380;
                int captureHeight = 1031;

                // Ensure the capture area stays within screen bounds
                if (captureX + captureWidth > screenBounds.Width)
                {
                    // Adjust width if it goes beyond the screen edge
                    captureWidth = screenBounds.Width - captureX - 5; // 5px margin
                }

                if (captureY + captureHeight > screenBounds.Height)
                {
                    // Adjust height if it goes beyond the screen edge
                    captureHeight = screenBounds.Height - captureY - 5; // 5px margin
                }

                // Validate that we still have a reasonable capture area
                if (captureWidth < 50 || captureHeight < 50)
                {
                    // Fallback to percentage-based calculation if exact coordinates don't work
                    // This handles different screen resolutions
                    captureWidth = (int)(screenBounds.Width * 0.20);
                    captureHeight = (int)(screenBounds.Height * 0.70);
                    captureX = screenBounds.Width - captureWidth - 20;
                    captureY = (int)(screenBounds.Height * 0.15);
                }

                Rectangle captureBounds = new Rectangle(captureX, captureY, captureWidth, captureHeight);

                // Generate timestamp for debug files
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                debugImagePath = Path.Combine(debugDirectory, $"ocr_capture_{timestamp}.png");
                debugTextPath = Path.Combine(debugDirectory, $"ocr_result_{timestamp}.txt");

                // Capture screen region
                using (Bitmap bitmap = new Bitmap(captureBounds.Width, captureBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(
                            new Point(captureBounds.Left, captureBounds.Top),
                            Point.Empty,
                            captureBounds.Size);
                    }

                    // Save the screenshot for debugging
                    bitmap.Save(debugImagePath, ImageFormat.Png);

                    // Load the captured image with EmguCV
                    using (Image<Bgr, byte> emguImage = new Image<Bgr, byte>(debugImagePath))
                    {
                        // Try to detect the highlighted row (gold/yellow selection)
                        Rectangle? highlightedArea = DetectHighlightedRow(emguImage);

                        Image<Bgr, byte> regionOfInterest;

                        if (highlightedArea.HasValue)
                        {
                            // If we found a highlighted area, crop to that area
                            Rectangle roi = highlightedArea.Value;

                            // Save the highlighted region for debugging
                            string highlightedPath = Path.Combine(debugDirectory, $"highlighted_{timestamp}.png");
                            using (Image<Bgr, byte> highlightedImage = emguImage.Copy(roi))
                            {
                                highlightedImage.Save(highlightedPath);

                                // Use the highlighted region for OCR processing
                                regionOfInterest = highlightedImage.Clone();
                            }
                        }
                        else
                        {
                            // If no highlight detected, use the entire image
                            regionOfInterest = emguImage.Clone();
                        }

                        // Process image with EmguCV for better OCR results
                        var processedImage = PreprocessImageWithEmguCV(regionOfInterest);

                        string processedImagePath = Path.Combine(debugDirectory, $"ocr_processed_{timestamp}.png");
                        processedImage.Save(processedImagePath);

                        // Perform OCR with Tesseract
                        string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                        string result = "";

                        try
                        {
                            using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                            {
                                // Configure Tesseract for game text - expand whitelist for better recognition
                                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.' ");
                                engine.SetVariable("tessedit_pageseg_mode", "6"); // Assume a single uniform block of text
                                engine.SetVariable("tessedit_ocr_engine_mode", "2"); // LSTM only

                                // Add specific game text optimization
                                engine.SetVariable("language_model_penalty_non_dict_word", "0.1"); // Lower penalty for non-dictionary words (character names)
                                engine.SetVariable("language_model_penalty_case", "0.1"); // Lower penalty for case issues

                                // Convert EmguCV image to a format Tesseract can use
                                using (var img = ConvertEmguCvImageToPix(processedImage))
                                {
                                    using (var page = engine.Process(img, PageSegMode.SingleBlock))
                                    {
                                        result = page.GetText().Trim();
                                        File.WriteAllText(debugTextPath, result); // Save OCR result
                                    }
                                }
                            }
                        }
                        catch (Exception ocrEx)
                        {
                            File.WriteAllText(debugTextPath, $"OCR Error: {ocrEx.Message}");
                            Console.WriteLine($"OCR Error: {ocrEx.Message}");
                            return string.Empty;
                        }

                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                // Write error to debug file
                try
                {
                    File.WriteAllText(debugTextPath, $"Capture Error: {ex.Message}\n{ex.StackTrace}");
                }
                catch { /* Ignore secondary errors */ }

                Console.WriteLine($"Error capturing screen: {ex.Message}");
                return string.Empty;
            }
        }

        private Rectangle? DetectHighlightedRow(Image<Bgr, byte> image)
        {
            try
            {
                // Convert to HSV for better color detection
                using (Image<Hsv, byte> hsvImage = image.Convert<Hsv, byte>())
                {
                    // Adjusted gold/yellow highlight detection ranges specifically for WoW
                    // In HSV, WoW gold selection color is approximately:
                    Hsv lowerBound = new Hsv(30, 100, 150);  // Adjusted hue to catch more gold variants
                    Hsv upperBound = new Hsv(70, 255, 255);  // Wider range

                    // Create a binary mask for the gold color
                    using (Image<Gray, byte> mask = hsvImage.InRange(lowerBound, upperBound))
                    {
                        // Save mask for debugging
                        string debugDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug");
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssff");
                        mask.Save(Path.Combine(debugDirectory, $"highlight_mask_{timestamp}.png"));

                        // Find rows with golden pixels (highlighting)
                        int[] rowsWithHighlight = new int[image.Height];
                        int maxHighlightCount = 0;
                        int bestRowStart = 0;
                        int consecutiveRows = 0;

                        // Count golden pixels in each row
                        for (int y = 0; y < mask.Height; y++)
                        {
                            int highlightPixels = 0;
                            for (int x = 0; x < mask.Width; x++)
                            {
                                if (mask.Data[y, x, 0] > 0)
                                {
                                    highlightPixels++;
                                }
                            }

                            rowsWithHighlight[y] = highlightPixels;

                            // Lower threshold for highlight detection (15 instead of 20)
                            if (highlightPixels > 15)
                            {
                                consecutiveRows++;

                                // Track the best run of consecutive rows with highlights
                                if (consecutiveRows > maxHighlightCount)
                                {
                                    maxHighlightCount = consecutiveRows;
                                    bestRowStart = y - consecutiveRows + 1;
                                }
                            }
                            else
                            {
                                consecutiveRows = 0;
                            }
                        }

                        // Lower threshold for row detection (4 instead of 5)
                        if (maxHighlightCount >= 4)
                        {
                            // Create a rectangle around the highlighted row with more padding
                            int rowHeight = maxHighlightCount + 6; // More padding

                            // Make sure we don't go out of bounds
                            int startY = Math.Max(0, bestRowStart - 3); // More padding at top
                            rowHeight = Math.Min(rowHeight, image.Height - startY);

                            return new Rectangle(0, startY, image.Width, rowHeight);
                        }
                    }
                }

                // No strong highlight found
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting highlighted row: {ex.Message}");
                return null;
            }
        }

        // Convert EmguCV Image to Pix format for Tesseract
        private Pix ConvertEmguCvImageToPix(Image<Gray, byte> image)
        {
            // First, ensure we're working with the right image format
            using (var bitmap = image.ToBitmap())
            {
                return Pix.LoadFromMemory(ImageToByteArray(bitmap));
            }
        }

        // Enhanced image preprocessing with EmguCV
        private Image<Gray, byte> PreprocessImageWithEmguCV(Image<Bgr, byte> originalImage)
        {
            // Convert to grayscale
            Image<Gray, byte> grayImage = originalImage.Convert<Gray, byte>();

            // Enhance contrast
            CvInvoke.EqualizeHist(grayImage, grayImage);

            // Adaptive thresholding - adjusted parameters for your specific WoW client
            Image<Gray, byte> thresholdImage = new Image<Gray, byte>(grayImage.Size);
            CvInvoke.AdaptiveThreshold(
                grayImage,
                thresholdImage,
                255.0,
                AdaptiveThresholdType.GaussianC,
                ThresholdType.Binary,
                13, // Increased block size for ultrawide monitor
                8   // Adjusted C value for your specific WoW font
            );

            // Apply morphological operations to clean up the image
            var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle,
                                                      new DrawingSize(3, 3),
                                                      new Point(-1, -1));

            // Opening operation (erosion followed by dilation) to remove noise
            CvInvoke.MorphologyEx(thresholdImage, thresholdImage, MorphOp.Open, element,
                                new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

            return thresholdImage;
        }

        // Improved character matching logic
        private double CalculateCharacterMatchScore(string screenText, Character character)
        {
            if (string.IsNullOrWhiteSpace(screenText)) return 0;

            double score = 0;
            string lowerText = screenText.ToLower();
            string targetName = character.Name.ToLower();
            string targetClass = character.Class?.ToLower() ?? "";
            int targetLevel = character.Level;

            // Exact name match - strongest signal
            if (ContainsExactCharacterName(lowerText, targetName))
            {
                score += 0.7; // Heavy weight for exact name match
            }
            else
            {
                // Partial name matching with word segments
                string[] nameParts = targetName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                double nameMatchCount = 0;

                foreach (var part in nameParts)
                {
                    // Only consider parts that are at least 3 characters long
                    if (part.Length >= 3 && lowerText.Contains(part))
                    {
                        nameMatchCount++;
                    }
                }

                if (nameParts.Length > 0)
                {
                    double partialNameScore = nameMatchCount / nameParts.Length * 0.5; // Max 0.5 for partial matches
                    score += partialNameScore;
                }
                else
                {
                    // Fallback to fuzzy matching if no parts
                    double fuzzyScore = CalculateFuzzyMatchScore(lowerText, targetName);
                    score += fuzzyScore * 0.4; // Lower weight for fuzzy matches
                }
            }

            // Class matching - medium signal
            if (!string.IsNullOrWhiteSpace(targetClass))
            {
                if (lowerText.Contains(targetClass))
                {
                    score += 0.2; // Direct class match
                }
                else
                {
                    // Check for common class abbreviations and variations
                    switch (targetClass)
                    {
                        case "death knight":
                            if (lowerText.Contains("dk") || lowerText.Contains("death") || lowerText.Contains("knight"))
                                score += 0.1;
                            break;
                        case "demon hunter":
                            if (lowerText.Contains("dh") || lowerText.Contains("demon") || lowerText.Contains("hunter"))
                                score += 0.1;
                            break;
                        default:
                            // For other classes, check if the first 3+ chars are present
                            if (targetClass.Length > 3 && lowerText.Contains(targetClass.Substring(0, 3)))
                                score += 0.1;
                            break;
                    }
                }
            }

            // Level matching - good signal
            if (targetLevel > 0)
            {
                // Check various level formats
                string[] levelPatterns = {
            $"level {targetLevel}",
            $"lvl {targetLevel}",
            $"lvl{targetLevel}",
            $"level{targetLevel}",
            $"lv {targetLevel}",
            $"lv{targetLevel}",
            $"{targetLevel}" // Just the number, less reliable
        };

                foreach (var pattern in levelPatterns)
                {
                    if (lowerText.Contains(pattern))
                    {
                        score += 0.15;
                        break;
                    }
                }

                // Add level number check with more context - check if the level appears with a boundary
                string levelPattern = $@"\b{targetLevel}\b";
                if (Regex.IsMatch(lowerText, levelPattern))
                {
                    // Check if it's around words related to levels
                    if (lowerText.Contains("level") || lowerText.Contains("lvl") || lowerText.Contains("lv"))
                    {
                        score += 0.05; // Small boost for level appearing with level-related words
                    }
                }
            }

            // Realm matching (if specified) - weaker signal but still useful
            if (!string.IsNullOrWhiteSpace(character.Realm))
            {
                string targetRealm = character.Realm.ToLower();
                if (lowerText.Contains(targetRealm))
                {
                    score += 0.1;
                }
            }

            // Prevent exceeding 1.0
            return Math.Min(score, 1.0);
        }

        // Add this new method for fuzzy string matching
        private double CalculateFuzzyMatchScore(string source, string target)
        {
            // Handle empty strings
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                return 0.0;

            // Normalize strings to lowercase
            source = source.ToLower();
            target = target.ToLower();

            // Check if target parts are in source
            string[] targetParts = target.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            int matchedParts = 0;

            foreach (string part in targetParts)
            {
                if (part.Length >= 3 && source.Contains(part))
                {
                    matchedParts++;
                }
            }

            if (targetParts.Length > 0)
            {
                return (double)matchedParts / targetParts.Length;
            }

            // Fallback: Calculate edit distance ratio
            int maxLength = Math.Max(source.Length, target.Length);
            if (maxLength == 0) return 1.0; // Both strings are empty

            int editDistance = LevenshteinDistance(source, target);
            return 1.0 - ((double)editDistance / maxLength);
        }

        // Add this helper method for calculating edit distance
        private int LevenshteinDistance(string s, string t)
        {
            // Special case: empty strings
            if (string.IsNullOrEmpty(s))
            {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            // Create distance matrix
            int[,] distance = new int[s.Length + 1, t.Length + 1];

            // Initialize first column and first row
            for (int i = 0; i <= s.Length; i++)
            {
                distance[i, 0] = i;
            }

            for (int j = 0; j <= t.Length; j++)
            {
                distance[0, j] = j;
            }

            // Calculate edit distance
            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[s.Length, t.Length];
        }

        // Method to save debug information about character selection
        private void LogCharacterSelectionDebug(Character character, List<(int position, double score, string text)> matches)
        {
            try
            {
                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug");
                if (!Directory.Exists(debugDir))
                    Directory.CreateDirectory(debugDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logPath = Path.Combine(debugDir, $"character_selection_log_{timestamp}.txt");

                using (StreamWriter writer = new StreamWriter(logPath))
                {
                    writer.WriteLine($"Character Selection Debug - {DateTime.Now}");
                    writer.WriteLine($"------------------------------------------");
                    writer.WriteLine($"Searching for: {character.Name}");
                    writer.WriteLine($"Level: {character.Level}");
                    writer.WriteLine($"Class: {character.Class}");
                    writer.WriteLine($"Realm: {character.Realm}");
                    writer.WriteLine($"------------------------------------------");
                    writer.WriteLine("Matches:");

                    foreach (var match in matches.OrderByDescending(m => m.score))
                    {
                        writer.WriteLine($"Position: {match.position}, Score: {match.score:F3}");
                        writer.WriteLine($"Text: \"{match.text}\"");
                        writer.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing debug log: {ex.Message}");
            }
        }

        // Method to extract screenshots from WoW client for testing
        public void CaptureCharacterScreenForTesting()
        {
            try
            {
                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug", "Testing");
                if (!Directory.Exists(debugDir))
                    Directory.CreateDirectory(debugDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string imagePath = Path.Combine(debugDir, $"character_screen_{timestamp}.png");

                // Capture the full screen
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    bitmap.Save(imagePath, ImageFormat.Png);
                }

                MessageBox.Show($"Screen capture saved to:\n{imagePath}", "Screen Capture",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error capturing screen: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Keep the existing ImageToByteArray method
        private byte[] ImageToByteArray(System.Drawing.Image image)
        {

            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        // Method to test OCR on an existing image file
        public string TestOcrOnImage(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    return "File not found!";
                }

                // Load image with EmguCV - this way is compatible with all versions
                using (Image<Bgr, byte> image = new Image<Bgr, byte>(imagePath))
                {
                    // Process with same pipeline as real OCR
                    var processedImage = PreprocessImageWithEmguCV(image);

                    // Save processed image for debugging
                    string processedPath = Path.Combine(
                        Path.GetDirectoryName(imagePath),
                        Path.GetFileNameWithoutExtension(imagePath) + "_processed.png");
                    processedImage.Save(processedPath);

                    // Perform OCR
                    string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                    using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                    {
                        engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789- '");
                        engine.SetVariable("tessedit_pageseg_mode", "6");
                        engine.SetVariable("tessedit_ocr_engine_mode", "2");

                        using (var img = ConvertEmguCvImageToPix(processedImage))
                        {
                            using (var page = engine.Process(img, PageSegMode.SingleBlock))
                            {
                                return page.GetText().Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // Add a method for automatic tuning of OCR parameters
        private void OptimizeOcrParameters()
        {
            try
            {
                // This can be called during setup or from a separate "Calibrate OCR" button
                MessageBox.Show("Please make sure your WoW client is open and on the character selection screen.",
                                "OCR Calibration", MessageBoxButton.OK, MessageBoxImage.Information);

                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug", "Calibration");
                if (!Directory.Exists(debugDir))
                    Directory.CreateDirectory(debugDir);

                // First, capture the current screen
                CaptureCharacterScreenForTesting();

                // Offer to run OCR tests with different parameters
                var result = MessageBox.Show("Would you like to test OCR with different parameters?",
                                           "OCR Calibration", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Sample a few different configurations and log results
                    // This is a simplified example - in practice, you'd iterate through more parameter combinations
                    TestOcrWithParameters(11, 5);  // Default params
                    TestOcrWithParameters(9, 3);   // Less aggressive
                    TestOcrWithParameters(15, 7);  // More aggressive

                    MessageBox.Show("OCR calibration complete. Check the OCR_Debug/Calibration folder for results.",
                                    "OCR Calibration", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                MessageBox.Show(
                    "System.Drawing functionality is not available on this system. This can happen if your Windows installation is missing components. " +
                    "Please ensure you're running on Windows with .NET Desktop Runtime installed.",
                    "Platform Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during OCR calibration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestOcrWithParameters(int blockSize, int cValue)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MessageBox.Show("This feature requires Windows.", "Platform Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                // Get screen bounds (focus on right side where character list appears)
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
                int captureWidth = (int)(screenBounds.Width * 0.3);
                int captureHeight = (int)(screenBounds.Height * 0.7);
                int captureX = screenBounds.Width - captureWidth;
                int captureY = (int)(screenBounds.Height * 0.15);
                Rectangle captureBounds = new Rectangle(captureX, captureY, captureWidth, captureHeight);

                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug", "Calibration");
                if (!Directory.Exists(debugDir))
                    Directory.CreateDirectory(debugDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string tempImagePath = Path.Combine(debugDir, $"temp_capture_{timestamp}.png");

                using (Bitmap bitmap = new Bitmap(captureBounds.Width, captureBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(
                            new System.Drawing.Point(captureBounds.Left, captureBounds.Top),
                            System.Drawing.Point.Empty,
                            captureBounds.Size);
                    }

                    // Save to temp file first
                    bitmap.Save(tempImagePath, ImageFormat.Png);

                    // Load from file instead of direct conversion
                    using (Image<Bgr, byte> emguImage = new Image<Bgr, byte>(tempImagePath))
                    {
                        // Convert to grayscale
                        Image<Gray, byte> grayImage = emguImage.Convert<Gray, byte>();

                        // Apply custom parameters
                        Image<Gray, byte> thresholdImage = new Image<Gray, byte>(grayImage.Size);
                        CvInvoke.AdaptiveThreshold(
                            grayImage,
                            thresholdImage,
                            255.0,
                            AdaptiveThresholdType.GaussianC,
                            ThresholdType.Binary,
                            blockSize,
                            cValue
                        );

                        string imagePath = Path.Combine(debugDir, $"params_b{blockSize}_c{cValue}_{timestamp}.png");
                        thresholdImage.Save(imagePath);

                        // Process with OCR and save results
                        string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                        using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                        {
                            engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789- '");
                            engine.SetVariable("tessedit_pageseg_mode", "6");
                            engine.SetVariable("tessedit_ocr_engine_mode", "2");

                            using (var img = ConvertEmguCvImageToPix(thresholdImage))
                            {
                                using (var page = engine.Process(img, PageSegMode.SingleBlock))
                                {
                                    string result = page.GetText().Trim();
                                    string resultPath = Path.Combine(debugDir, $"params_b{blockSize}_c{cValue}_{timestamp}.txt");
                                    File.WriteAllText(resultPath, result);
                                }
                            }
                        }
                    }

                    // Clean up the temp file
                    if (File.Exists(tempImagePath))
                    {
                        try { File.Delete(tempImagePath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in parameter testing: {ex.Message}");
            }
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
            private string _iconPath; // New property
            private int _launchDelayMs = 5000;
            private int _characterSelectDelayMs = 8000;
            private ObservableCollection<Account> _accounts;
            private Server _server;

            // Add a new property with a getter and setter
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
            private ObservableCollection<Character> _characters;
            private Character _selectedCharacter;

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

            public ObservableCollection<Character> Characters
            {
                get => _characters;
                set
                {
                    _characters = value;
                    OnPropertyChanged();
                }
            }

            public Character SelectedCharacter
            {
                get => _selectedCharacter;
                set
                {
                    // Simpler setter that doesn't try to access SelectedAccount
                    _selectedCharacter = value;
                    OnPropertyChanged();
                }
            }

            public Account()
            {
                Characters = new ObservableCollection<Character>();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class Character : INotifyPropertyChanged
        {
            private string _name;
            private string _realm;
            private string _class;  // Character class (Warrior, Mage, etc.)
            private int _level;
            private Account _account; // Reference to parent account

            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }

            public string Realm
            {
                get => _realm;
                set
                {
                    _realm = value;
                    OnPropertyChanged();
                }
            }

            public string Class
            {
                get => _class;
                set
                {
                    _class = value;
                    OnPropertyChanged();
                }
            }

            public int Level
            {
                get => _level;
                set
                {
                    _level = value;
                    OnPropertyChanged();
                }
            }

            [JsonIgnore] // Prevents circular reference during serialization
            public Account Account
            {
                get => _account;
                set
                {
                    _account = value;
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
                        MessageBox.Show("Server name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                        // Debug output
                        System.Diagnostics.Debug.WriteLine($"Selected Expansion: {selectedExpansion}");
                        System.Diagnostics.Debug.WriteLine($"Icon Path: {iconPath}");
                        System.Diagnostics.Debug.WriteLine($"File Exists: {System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, iconPath.TrimStart('/')))}");
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
                        MessageBox.Show("Expansion name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Expansion.LauncherPath))
                    {
                        MessageBox.Show("Launcher path is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("Username is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(Account.Password))
                    {
                        MessageBox.Show("Password is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public class CharacterDialog : Window
        {
            public Character Character { get; private set; }

            public CharacterDialog(Character existingCharacter = null)
            {
                // Set dialog properties and layout
                Title = existingCharacter == null ? "Add Character" : "Edit Character";
                Width = 400;
                Height = 250;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                Character = existingCharacter != null
                    ? new Character
                    {
                        Name = existingCharacter.Name,
                        Realm = existingCharacter.Realm,
                        Class = existingCharacter.Class,
                        Level = existingCharacter.Level
                    }
                    : new Character();

                // Create form layout
                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.Margin = new Thickness(10);

                // Name label and textbox
                var nameLabel = new System.Windows.Controls.Label { Content = "Character Name:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(nameLabel, 0);
                System.Windows.Controls.Grid.SetColumn(nameLabel, 0);

                var nameTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Character.Name };
                nameTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Name") { Source = Character, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
                System.Windows.Controls.Grid.SetRow(nameTextBox, 0);
                System.Windows.Controls.Grid.SetColumn(nameTextBox, 1);

                // Realm label and textbox
                var realmLabel = new System.Windows.Controls.Label { Content = "Realm:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(realmLabel, 1);
                System.Windows.Controls.Grid.SetColumn(realmLabel, 0);

                var realmTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Character.Realm };
                realmTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Realm") { Source = Character, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
                System.Windows.Controls.Grid.SetRow(realmTextBox, 1);
                System.Windows.Controls.Grid.SetColumn(realmTextBox, 1);

                // Class label and combobox
                var classLabel = new System.Windows.Controls.Label { Content = "Class:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(classLabel, 2);
                System.Windows.Controls.Grid.SetColumn(classLabel, 0);

                var classComboBox = new System.Windows.Controls.ComboBox { Margin = new Thickness(5) };
                string[] wowClasses = { "Warrior", "Paladin", "Hunter", "Rogue", "Priest", "Death Knight", "Shaman", "Mage", "Warlock", "Monk", "Druid", "Demon Hunter", "Evoker" };
                foreach (var wowClass in wowClasses)
                {
                    classComboBox.Items.Add(wowClass);
                }
                classComboBox.SelectedItem = Character.Class;
                classComboBox.SelectionChanged += (sender, args) => Character.Class = classComboBox.SelectedItem as string;
                System.Windows.Controls.Grid.SetRow(classComboBox, 2);
                System.Windows.Controls.Grid.SetColumn(classComboBox, 1);

                // Level label and numeric input
                var levelLabel = new System.Windows.Controls.Label { Content = "Level:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(levelLabel, 3);
                System.Windows.Controls.Grid.SetColumn(levelLabel, 0);

                var levelTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Character.Level.ToString() };
                levelTextBox.TextChanged += (sender, args) =>
                {
                    if (int.TryParse(levelTextBox.Text, out int level))
                    {
                        Character.Level = level;
                    }
                };
                System.Windows.Controls.Grid.SetRow(levelTextBox, 3);
                System.Windows.Controls.Grid.SetColumn(levelTextBox, 1);

                // Button panel
                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                System.Windows.Controls.Grid.SetRow(buttonPanel, 4);
                System.Windows.Controls.Grid.SetColumn(buttonPanel, 0);
                System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 2);

                var saveButton = new System.Windows.Controls.Button { Content = "Save", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
                saveButton.Click += (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(Character.Name))
                    {
                        MessageBox.Show("Character name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Character.Realm))
                    {
                        MessageBox.Show("Realm is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Character.Class))
                    {
                        MessageBox.Show("Class is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    DialogResult = true;
                };

                var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };

                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(cancelButton);

                grid.Children.Add(nameLabel);
                grid.Children.Add(nameTextBox);
                grid.Children.Add(realmLabel);
                grid.Children.Add(realmTextBox);
                grid.Children.Add(classLabel);
                grid.Children.Add(classComboBox);
                grid.Children.Add(levelLabel);
                grid.Children.Add(levelTextBox);
                grid.Children.Add(buttonPanel);

                Content = grid;
            }
        }
    }

}
