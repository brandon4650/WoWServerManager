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

// Explicitly using WPF MessageBox
using MessageBox = System.Windows.MessageBox;
using System.Text.Json.Serialization;
using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
using Point = System.Drawing.Point; // Resolves Point ambiguity
using ImageFormat = System.Drawing.Imaging.ImageFormat; // Resolves ImageFormat ambiguity

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
using System.Net.Http;
using System.Text;

// Add this to resolve Mat reference errors
using Mat = Emgu.CV.Mat;

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
        public ICommand VisualizeOcrResultsCommand { get; }

        public ICommand GetCharacterRecommendationsCommand { get; }
        public ICommand TestCharacterSelectionCommand { get; }



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
            CalibrateOcrCommand = new RelayCommand(_ => CalibrateCharacterSelectionOcr());
            VisualizeOcrResultsCommand = new RelayCommand(_ => VisualizeOcrResults());

            CalibrateOcrCommand = new RelayCommand(async _ => await CalibrateCharacterSelectionOcr());
            TestCharacterSelectionCommand = new RelayCommand(async _ => await TestCharacterSelection());
            GetCharacterRecommendationsCommand = new RelayCommand(_ => ShowCharacterRecommendations());
            
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

        private void EnsureDefaultIcons()
        {
            try
            {
                string iconsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons");
                if (!Directory.Exists(iconsDir))
                {
                    Directory.CreateDirectory(iconsDir);
                }

                // List of required icon files
                string[] iconFiles = {
            "default_icon.png",
            "classic_icon.png",
            "tbc_icon.png",
            "wotlk_icon.png",
            "cata_icon.png",
            "mop_icon.png",
            "wod_icon.png",
            "legion_icon.png",
            "bfa_icon.png",
            "shadowlands_icon.png",
            "dragonflight_icon.png"
        };

                bool createdAnyIcon = false;

                // Create placeholder icons if they don't exist
                foreach (string iconFile in iconFiles)
                {
                    string iconPath = Path.Combine(iconsDir, iconFile);
                    if (!File.Exists(iconPath))
                    {
                        // Create a simple colored square as placeholder
                        using (Bitmap bitmap = new Bitmap(64, 64))
                        {
                            using (Graphics g = Graphics.FromImage(bitmap))
                            {
                                // Fill with a color based on icon name
                                Color color = GetColorForIcon(iconFile);
                                g.FillRectangle(new SolidBrush(color), 0, 0, 64, 64);
                                g.DrawRectangle(new Pen(Color.Gold, 2), 1, 1, 61, 61);

                                // Add text label
                                string label = iconFile.Replace("_icon.png", "").ToUpper();
                                using (Font font = new Font(new FontFamily("Arial"), 8, FontStyle.Bold))
                                {
                                    g.DrawString(label, font, System.Drawing.Brushes.White,
                                                 new RectangleF(2, 24, 60, 20),
                                                 new StringFormat { Alignment = StringAlignment.Center });
                                }
                            }

                            // Create directory if needed
                            Directory.CreateDirectory(Path.GetDirectoryName(iconPath));

                            // Save the bitmap
                            bitmap.Save(iconPath, ImageFormat.Png);
                            createdAnyIcon = true;
                        }

                        Console.WriteLine($"Created placeholder icon: {iconPath}");
                    }
                }

                // Show message if any icons were created
                if (createdAnyIcon)
                {
                    MessageBox.Show(
                        "Placeholder expansion icons have been created. For better appearance, " +
                        "consider replacing them with official WoW expansion icons in the Resources/Icons folder.",
                        "Icons Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring default icons: {ex.Message}");
            }
        }
        private void ProcessImageWithCLAHE(Mat inputMat, Mat outputMat)
        {
            // Create a CLAHE instance 
            var clahe = CvInvoke.CreateCLAHE(2.0, new System.Drawing.Size(8, 8));

            // Apply the CLAHE algorithm
            clahe.Apply(inputMat, outputMat);
        }


        // Helper method to generate colors for placeholder icons
        private System.Drawing.Color GetColorForIcon(string iconName)
        {
            switch (iconName.ToLower())
            {
                case "classic_icon.png": return System.Drawing.Color.FromArgb(120, 120, 120);
                case "tbc_icon.png": return System.Drawing.Color.FromArgb(0, 120, 0);
                case "wotlk_icon.png": return System.Drawing.Color.FromArgb(0, 80, 160);
                case "cata_icon.png": return System.Drawing.Color.FromArgb(200, 60, 0);
                case "mop_icon.png": return System.Drawing.Color.FromArgb(0, 150, 150);
                case "wod_icon.png": return System.Drawing.Color.FromArgb(180, 40, 0);
                case "legion_icon.png": return System.Drawing.Color.FromArgb(0, 180, 0);
                case "bfa_icon.png": return System.Drawing.Color.FromArgb(0, 0, 180);
                case "shadowlands_icon.png": return System.Drawing.Color.FromArgb(100, 60, 160);
                case "dragonflight_icon.png": return System.Drawing.Color.FromArgb(200, 160, 0);
                default: return System.Drawing.Color.FromArgb(80, 80, 80);
            }
        }

        private void ShowCharacterRecommendations()
        {
            string recommendations = GetCharacterSelectionRecommendations();

            var dialog = new Window
            {
                Title = "Character Selection Recommendations",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
            };

            var mainPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20)
            };

            var header = new System.Windows.Controls.TextBlock
            {
                Text = "Character Selection Recommendations",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Gold),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var content = new System.Windows.Controls.TextBox
            {
                Text = recommendations,
                IsReadOnly = true,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 20)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                Padding = new Thickness(10),
                FontFamily = new FontFamily("Consolas"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                Height = 250
            };

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "Close",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (sender, e) => dialog.Close();

            mainPanel.Children.Add(header);
            mainPanel.Children.Add(content);
            mainPanel.Children.Add(closeButton);

            dialog.Content = mainPanel;
            dialog.ShowDialog();
        }

        // Add a method to test character selection without actually logging in
        public async Task TestCharacterSelection()
        {
            if (SelectedAccount?.SelectedCharacter == null)
            {
                MessageBox.Show("Please select a character first.", "No Character Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if WoW is running
            Process[] wowProcesses = Process.GetProcessesByName("Wow");
            Process[] wowRetailProcesses = Process.GetProcessesByName("WowClassic");

            if (wowProcesses.Length == 0 && wowRetailProcesses.Length == 0)
            {
                // Show warning that WoW isn't running
                var result = MessageBox.Show(
                    "World of Warcraft doesn't appear to be running. This test works best when WoW is open on the character selection screen.\n\n" +
                    "Do you want to continue anyway?",
                    "WoW Not Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                    return;
            }

            // Show test in progress dialog
            var testWindow = new Window
            {
                Title = "Character Selection Test",
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
            };

            var mainPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20)
            };

            var header = new System.Windows.Controls.TextBlock
            {
                Text = "Testing Character Selection",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Gold),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var statusText = new System.Windows.Controls.TextBlock
            {
                Text = "Scanning for character names...",
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var progressBar = new System.Windows.Controls.ProgressBar
            {
                IsIndeterminate = true,
                Height = 20,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var resultText = new System.Windows.Controls.TextBox
            {
                IsReadOnly = true,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 20)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                Padding = new Thickness(10),
                FontFamily = new FontFamily("Consolas"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                Height = 100
            };

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "Close",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsEnabled = false
            };
            closeButton.Click += (sender, e) => testWindow.Close();

            mainPanel.Children.Add(header);
            mainPanel.Children.Add(statusText);
            mainPanel.Children.Add(progressBar);
            mainPanel.Children.Add(resultText);
            mainPanel.Children.Add(closeButton);

            testWindow.Content = mainPanel;
            testWindow.Show();

            // Run the test
            try
            {
                // Get the target character
                Character target = SelectedAccount.SelectedCharacter;

                // Capture screen text
                string screenText = CaptureScreenText();

                // Log the OCR result
                resultText.Text = "OCR Result:\n" + screenText;

                // Update status
                statusText.Text = "Analyzing captured text...";
                await Task.Delay(500);

                // Calculate match score
                double matchScore = CalculateCharacterMatchScore(screenText, target);
                bool exactNameMatch = ContainsExactCharacterName(screenText, target.Name);

                // Update status based on results
                if (exactNameMatch)
                {
                    statusText.Text = $"Success! Found exact match for character name '{target.Name}'";
                    statusText.Foreground = new SolidColorBrush(Colors.LightGreen);
                }
                else if (matchScore >= 0.65)
                {
                    statusText.Text = $"Partial match found with {matchScore:P0} confidence";
                    statusText.Foreground = new SolidColorBrush(Colors.Yellow);
                }
                else
                {
                    statusText.Text = $"No match found. Character '{target.Name}' was not detected";
                    statusText.Foreground = new SolidColorBrush(Colors.Red);
                }

                // Append advice based on results
                StringBuilder advice = new StringBuilder();
                advice.AppendLine("\n\nAnalysis:");

                if (exactNameMatch)
                {
                    advice.AppendLine("✓ Character name detected successfully");
                }
                else
                {
                    advice.AppendLine("✗ Character name not found in OCR text");
                }

                // Check for level
                if (screenText.Contains($"Level {target.Level}") || screenText.Contains($"Lvl {target.Level}"))
                {
                    advice.AppendLine($"✓ Character level ({target.Level}) detected");
                }
                else
                {
                    advice.AppendLine($"✗ Character level ({target.Level}) not detected");
                }

                // Check for class
                if (!string.IsNullOrEmpty(target.Class) && screenText.ToLower().Contains(target.Class.ToLower()))
                {
                    advice.AppendLine($"✓ Character class ({target.Class}) detected");
                }
                else if (!string.IsNullOrEmpty(target.Class))
                {
                    advice.AppendLine($"✗ Character class ({target.Class}) not detected");
                }

                // Overall assessment
                advice.AppendLine("\nOverall Assessment:");
                if (matchScore >= 0.85)
                {
                    advice.AppendLine("Excellent! Character selection will work reliably.");
                }
                else if (matchScore >= 0.65)
                {
                    advice.AppendLine("Good. Character selection should work in most cases.");
                }
                else if (matchScore >= 0.4)
                {
                    advice.AppendLine("Fair. Character selection may work but could be unreliable.");
                }
                else
                {
                    advice.AppendLine("Poor. Character selection is unlikely to work reliably.");
                    advice.AppendLine("Try running OCR calibration or adjust screen positioning.");
                }

                resultText.Text += advice.ToString();

                // Stop progress and enable close button
                progressBar.IsIndeterminate = false;
                progressBar.Value = 100;
                closeButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                statusText.Text = "Error during test: " + ex.Message;
                statusText.Foreground = new SolidColorBrush(Colors.Red);
                resultText.Text = ex.ToString();
                progressBar.IsIndeterminate = false;
                closeButton.IsEnabled = true;
            }
        }

        private void AdjustOcrForScreenResolution()
        {
            // Get the current screen resolution
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            // Default character selection area (works for 1920x1080)
            int defaultCaptureX = 2010;
            int defaultCaptureY = 62;
            int defaultCaptureWidth = 380;
            int defaultCaptureHeight = 1031;

            // If resolution is different from the default assumptions
            if (screenWidth != 1920 || screenHeight != 1080)
            {
                // For 4K displays (3840x2160)
                if (screenWidth >= 3840)
                {
                    _charSelectCaptureX = Convert.ToInt32(screenWidth * 0.52);
                    _charSelectCaptureY = Convert.ToInt32(screenHeight * 0.06);
                    _charSelectCaptureWidth = Convert.ToInt32(screenWidth * 0.20);
                    _charSelectCaptureHeight = Convert.ToInt32(screenHeight * 0.85);
                }
                // For ultrawide monitors (21:9 aspect ratio)
                else if ((double)screenWidth / screenHeight >= 2.1)
                {
                    _charSelectCaptureX = Convert.ToInt32(screenWidth * 0.82);
                    _charSelectCaptureY = Convert.ToInt32(screenHeight * 0.06);
                    _charSelectCaptureWidth = Convert.ToInt32(screenWidth * 0.15);
                    _charSelectCaptureHeight = Convert.ToInt32(screenHeight * 0.85);
                }
                // For other resolutions, use proportional scaling
                else
                {
                    double xRatio = (double)screenWidth / 1920;
                    double yRatio = (double)screenHeight / 1080;

                    _charSelectCaptureX = Convert.ToInt32(defaultCaptureX * xRatio);
                    _charSelectCaptureY = Convert.ToInt32(defaultCaptureY * yRatio);
                    _charSelectCaptureWidth = Convert.ToInt32(defaultCaptureWidth * xRatio);
                    _charSelectCaptureHeight = Convert.ToInt32(defaultCaptureHeight * yRatio);
                }

                // Ensure capture area is within screen bounds
                if (_charSelectCaptureX + _charSelectCaptureWidth > screenWidth)
                {
                    _charSelectCaptureWidth = screenWidth - _charSelectCaptureX - 5;
                }

                if (_charSelectCaptureY + _charSelectCaptureHeight > screenHeight)
                {
                    _charSelectCaptureHeight = screenHeight - _charSelectCaptureY - 5;
                }

                // Log the adjusted capture area
                Console.WriteLine($"Adjusted character selection capture area for resolution {screenWidth}x{screenHeight}:");
                Console.WriteLine($"X={_charSelectCaptureX}, Y={_charSelectCaptureY}, W={_charSelectCaptureWidth}, H={_charSelectCaptureHeight}");
            }
            else
            {
                // Use default values for 1920x1080
                _charSelectCaptureX = defaultCaptureX;
                _charSelectCaptureY = defaultCaptureY;
                _charSelectCaptureWidth = defaultCaptureWidth;
                _charSelectCaptureHeight = defaultCaptureHeight;
            }
        }

        // Add these fields to store the capture area coordinates
        private int _charSelectCaptureX = 2010;
        private int _charSelectCaptureY = 62;
        private int _charSelectCaptureWidth = 380;
        private int _charSelectCaptureHeight = 1031;


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

        private async Task EnsureTessDataFilesAsync()
        {
            try
            {
                string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                string engTrainedDataFile = Path.Combine(tessdataPath, "eng.traineddata");

                // Ensure directory exists
                if (!Directory.Exists(tessdataPath))
                {
                    Directory.CreateDirectory(tessdataPath);
                }

                // Check if the English language file exists
                if (!File.Exists(engTrainedDataFile))
                {
                    // Show a message to the user
                    var result = MessageBox.Show(
                        "The OCR engine requires language data files which are not present.\n\n" +
                        "Would you like to download the English language file now?\n" +
                        "(This may take a minute or two depending on your connection)",
                        "Missing OCR Data",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Show progress dialog
                        var progressWindow = new Window
                        {
                            Title = "Downloading OCR Data",
                            Width = 400,
                            Height = 150,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            ResizeMode = ResizeMode.NoResize,
                            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
                        };

                        var progressPanel = new System.Windows.Controls.StackPanel
                        {
                            Margin = new Thickness(20)
                        };

                        var progressText = new System.Windows.Controls.TextBlock
                        {
                            Text = "Downloading English language data file...",
                            Foreground = new SolidColorBrush(Colors.White),
                            Margin = new Thickness(0, 0, 0, 10)
                        };

                        var progressBar = new System.Windows.Controls.ProgressBar
                        {
                            IsIndeterminate = true,
                            Height = 20
                        };

                        progressPanel.Children.Add(progressText);
                        progressPanel.Children.Add(progressBar);
                        progressWindow.Content = progressPanel;

                        progressWindow.Show();

                        try
                        {
                            // Download the file asynchronously
                            using (HttpClient client = new HttpClient())
                            {
                                // Set a user agent to avoid being blocked
                                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 WoWServerManager");

                                // URL to the eng.traineddata file from GitHub
                                string url = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata";

                                // Download the file
                                var response = await client.GetAsync(url);
                                response.EnsureSuccessStatusCode();

                                // Save to file
                                using (var fileStream = new FileStream(engTrainedDataFile, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await response.Content.CopyToAsync(fileStream);
                                }

                                progressWindow.Close();

                                MessageBox.Show(
                                    "English language data file downloaded successfully!\n" +
                                    "The character selection OCR feature is now ready to use.",
                                    "Download Complete",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            progressWindow.Close();

                            MessageBox.Show(
                                $"Error downloading language file: {ex.Message}\n\n" +
                                "Please download the 'eng.traineddata' file manually and place it in the 'tessdata' folder.",
                                "Download Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "Character selection by OCR will not work without the language data file.\n\n" +
                            "If you want to enable this feature later, download 'eng.traineddata' file manually " +
                            "and place it in the 'tessdata' folder.",
                            "OCR Disabled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking Tesseract data files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private async Task<bool> SelectCharacterByName(string characterName)
        {
            const int maxAttempts = 15;
            const double highConfidenceThreshold = 0.85;
            const double mediumConfidenceThreshold = 0.65;
            const int initialDelay = 1500;
            const int navigationDelay = 600;

            // Define character highlight color ranges in HSV
            Hsv goldColorLower = new Hsv(20, 100, 150);  // Lower bound for gold/yellow
            Hsv goldColorUpper = new Hsv(70, 255, 255);  // Upper bound for gold/yellow

            if (SelectedAccount?.SelectedCharacter == null)
            {
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

            Console.WriteLine($"Initial highlighted character: Score {currentScore:F2}\nText: {currentHighlightText}");

            // If the currently selected character is a good match, use it immediately
            if (currentScore >= highConfidenceThreshold)
            {
                Console.WriteLine($"Initial character is a match - selecting");
                SendKeys.SendWait("{ENTER}");
                await Task.Delay(1000);
                return true; // High confidence match
            }

            // Use pattern matching to check for exact character name in the highlighted text
            if (ContainsExactCharacterName(currentHighlightText, target.Name))
            {
                Console.WriteLine($"Found exact character name in highlighted text - selecting");
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

                Console.WriteLine($"Pos {attempt}: Score {positionScore:F2}\nText: {combinedText}");

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
                    return true; // High confidence match
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
                return true; // Medium confidence match
            }

            // Final fallback - try with just the names
            var nameOnlyMatch = matches.OrderByDescending(m =>
                CalculateNameOnlyMatchScore(m.text, target.Name)).FirstOrDefault();

            if (nameOnlyMatch.position >= 0 && nameOnlyMatch.score > 0.5) // Only use if we have a reasonable match
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
                return true; // Name-only match
            }

            // Ultimate fallback - just press enter on whatever is selected
            Console.WriteLine("No good match found - selecting current character");
            SendKeys.SendWait("{ENTER}");
            return false; // Could not find a good match
        }

        // New helper method to check for exact character name match
        private bool ContainsExactCharacterName(string text, string characterName)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(characterName))
                return false;

            // Convert to lowercase for case-insensitive comparison
            string textLower = text.ToLower();
            string nameLower = characterName.ToLower();

            // Method 1: Direct contains check
            if (textLower.Contains(nameLower))
                return true;

            // Method 2: Check for name with word boundaries
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
            expansionName = expansionName?.ToLower() ?? "";

            // Use pack URIs for proper resource loading in WPF
            return expansionName switch
            {
                string s when s.Contains("classic") => "pack://application:,,,/Resources/Icons/classic_icon.png",
                string s when s.Contains("burning crusade") => "pack://application:,,,/Resources/Icons/tbc_icon.png",
                string s when s.Contains("lich king") => "pack://application:,,,/Resources/Icons/wotlk_icon.png",
                string s when s.Contains("cataclysm") => "pack://application:,,,/Resources/Icons/cata_icon.png",
                string s when s.Contains("pandaria") => "pack://application:,,,/Resources/Icons/mop_icon.png",
                string s when s.Contains("draenor") => "pack://application:,,,/Resources/Icons/wod_icon.png",
                string s when s.Contains("legion") => "pack://application:,,,/Resources/Icons/legion_icon.png",
                string s when s.Contains("azeroth") => "pack://application:,,,/Resources/Icons/bfa_icon.png",
                string s when s.Contains("shadowlands") => "pack://application:,,,/Resources/Icons/shadowlands_icon.png",
                string s when s.Contains("dragonflight") => "pack://application:,,,/Resources/Icons/dragonflight_icon.png",
                _ => "pack://application:,,,/Resources/Icons/default_icon.png"
            };
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
        private Image<Gray, byte> ApplyCLAHE(Image<Gray, byte> image, double clipLimit = 2.0, System.Drawing.Size gridSize = default)
        {
            if (gridSize == default)
                gridSize = new System.Drawing.Size(8, 8);

            // Create a new Mat to store the result
            Mat result = new Mat();

            // Create a CLAHE object (or whatever equivalent your EmguCV version supports)
            var clahe = CvInvoke.CreateCLAHE(clipLimit, gridSize);

            // Apply CLAHE
            clahe.Apply(image.Mat, result);

            // Convert back to Image<Gray, byte>
            return new Image<Gray, byte>(result.Bitmap);
        }


        // Enhanced image preprocessing with EmguCV
        private Image<Gray, byte> PreprocessImageWithEmguCV(Image<Bgr, byte> originalImage)
        {
            // Convert to grayscale
            Image<Gray, byte> grayImage = originalImage.Convert<Gray, byte>();

            // Apply bilateral filter to reduce noise while preserving edges
            CvInvoke.BilateralFilter(grayImage, grayImage, 9, 75, 75);

            // Enhance contrast using CLAHE with our custom implementation
            grayImage = ApplyCLAHE(grayImage, 2.0, new System.Drawing.Size(8, 8));

            // Adaptive thresholding with calibrated parameters
            Image<Gray, byte> thresholdImage = new Image<Gray, byte>(grayImage.Size);
            CvInvoke.AdaptiveThreshold(
                grayImage,
                thresholdImage,
                255.0,
                AdaptiveThresholdType.GaussianC,
                ThresholdType.Binary,
                _ocrBlockSize, // Use calibrated block size
                _ocrCValue     // Use calibrated C value
            );

            // Apply morphological operations to clean up the text
            var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle,
                                                      new System.Drawing.Size(2, 2), // Smaller kernel for text details
                                                      new Point(-1, -1));

            // Opening operation to remove noise
            CvInvoke.MorphologyEx(thresholdImage, thresholdImage, MorphOp.Open, element,
                                new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

            return thresholdImage;
        }

        private System.Windows.Controls.Image ConvertBitmapToWpfImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                System.Windows.Controls.Image wpfImage = new System.Windows.Controls.Image();
                wpfImage.Source = bitmapImage;
                return wpfImage;
            }
        }


        // Add a method to detect character selection screen
        private Rectangle? FindCharacterSelectionArea(Image<Bgr, byte> image)
        {
            try
            {
                // Convert to HSV for better color detection
                using (Image<Hsv, byte> hsvImage = image.Convert<Hsv, byte>())
                {
                    // First, look for gold/yellow highlights of selected character
                    Hsv goldLower = new Hsv(20, 100, 150);
                    Hsv goldUpper = new Hsv(40, 255, 255);

                    using (Image<Gray, byte> goldMask = hsvImage.InRange(goldLower, goldUpper))
                    {
                        // Now look for blue UI elements common in WoW character selection
                        Hsv blueLower = new Hsv(100, 80, 80);
                        Hsv blueUpper = new Hsv(140, 255, 255);

                        using (Image<Gray, byte> blueMask = hsvImage.InRange(blueLower, blueUpper))
                        {
                            // Combine masks
                            Image<Gray, byte> combinedMask = goldMask.Or(blueMask);

                            // Find contours in the combined mask
                            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                            CvInvoke.FindContours(
                                combinedMask,
                                contours,
                                null,
                                RetrType.List,
                                ChainApproxMethod.ChainApproxSimple);

                            // If we found contours, find the bounding rectangle
                            if (contours.Size > 0)
                            {
                                // Get bounding rectangle of all contours
                                Rectangle boundingRect = CvInvoke.BoundingRectangle(contours[0]);
                                for (int i = 1; i < contours.Size; i++)
                                {
                                    boundingRect = Rectangle.Union(boundingRect,
                                        CvInvoke.BoundingRectangle(contours[i]));
                                }

                                // Add padding
                                int padding = 10;
                                boundingRect = new Rectangle(
                                    Math.Max(0, boundingRect.X - padding),
                                    Math.Max(0, boundingRect.Y - padding),
                                    Math.Min(image.Width - boundingRect.X + padding, boundingRect.Width + 2 * padding),
                                    Math.Min(image.Height - boundingRect.Y + padding, boundingRect.Height + 2 * padding)
                                );

                                return boundingRect;
                            }
                        }
                    }
                }

                // Fallback to default selection area
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding character selection area: {ex.Message}");
                return null;
            }
        }

        // Add a debug visualization method for OCR results
        public void VisualizeOcrResults()
        {
            try
            {
                // Capture the current screen for OCR
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
                int captureX = 2010;
                int captureY = 62;
                int captureWidth = 380;
                int captureHeight = 1031;

                // Ensure the capture area is valid
                if (captureX + captureWidth > screenBounds.Width)
                    captureWidth = screenBounds.Width - captureX - 5;

                if (captureY + captureHeight > screenBounds.Height)
                    captureHeight = screenBounds.Height - captureY - 5;

                // Capture area
                Rectangle captureBounds = new Rectangle(captureX, captureY, captureWidth, captureHeight);

                // Create debug directory
                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug");
                if (!Directory.Exists(debugDir))
                    Directory.CreateDirectory(debugDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string originalPath = Path.Combine(debugDir, $"ocr_visual_original_{timestamp}.png");
                string processedPath = Path.Combine(debugDir, $"ocr_visual_processed_{timestamp}.png");
                string textPath = Path.Combine(debugDir, $"ocr_visual_text_{timestamp}.txt");

                // Capture and save original
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

                    // Process with OCR pipeline
                    using (Image<Bgr, byte> image = new Image<Bgr, byte>(bitmap))
                    {
                        // Try to detect the highlighted row
                        Rectangle? highlightedArea = DetectHighlightedRow(image);

                        Image<Bgr, byte> regionOfInterest;
                        if (highlightedArea.HasValue)
                        {
                            // Crop to highlighted area
                            regionOfInterest = image.Copy(highlightedArea.Value);
                        }
                        else
                        {
                            regionOfInterest = image.Clone();
                        }

                        // Process image
                        var processedImage = PreprocessImageWithEmguCV(regionOfInterest);
                        processedImage.Save(processedPath);

                        // Perform OCR
                        string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                        string result = "";

                        using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                        {
                            engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.' ");
                            engine.SetVariable("tessedit_pageseg_mode", "6");
                            engine.SetVariable("tessedit_ocr_engine_mode", "2");

                            using (var img = ConvertEmguCvImageToPix(processedImage))
                            {
                                using (var page = engine.Process(img, PageSegMode.SingleBlock))
                                {
                                    result = page.GetText().Trim();
                                    File.WriteAllText(textPath, result);

                                    // Show OCR results
                                    var resultWindow = new Window
                                    {
                                        Title = "OCR Debug Results",
                                        Width = 800,
                                        Height = 600,
                                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
                                    };

                                    var grid = new System.Windows.Controls.Grid();
                                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

                                    // Original image header
                                    var originalHeader = new System.Windows.Controls.TextBlock
                                    {
                                        Text = "Original Capture",
                                        FontWeight = FontWeights.Bold,
                                        Foreground = new SolidColorBrush(Colors.White),
                                        Margin = new Thickness(10, 5, 0, 0)
                                    };
                                    System.Windows.Controls.Grid.SetRow(originalHeader, 0);
                                    grid.Children.Add(originalHeader);

                                    // Original image
                                    var originalImage = new System.Windows.Controls.Image
                                    {
                                        Source = new BitmapImage(new Uri(originalPath, UriKind.Absolute)),
                                        Stretch = Stretch.Uniform,
                                        Margin = new Thickness(10)
                                    };
                                    System.Windows.Controls.Grid.SetRow(originalImage, 1);
                                    grid.Children.Add(originalImage);

                                    // Processed image header
                                    var processedHeader = new System.Windows.Controls.TextBlock
                                    {
                                        Text = "Processed Image for OCR",
                                        FontWeight = FontWeights.Bold,
                                        Foreground = new SolidColorBrush(Colors.White),
                                        Margin = new Thickness(10, 5, 0, 0)
                                    };
                                    System.Windows.Controls.Grid.SetRow(processedHeader, 2);
                                    grid.Children.Add(processedHeader);

                                    // Processed image
                                    var processedImage2 = new System.Windows.Controls.Image
                                    {
                                        Source = new BitmapImage(new Uri(processedPath, UriKind.Absolute)),
                                        Stretch = Stretch.Uniform,
                                        Margin = new Thickness(10)
                                    };
                                    System.Windows.Controls.Grid.SetRow(processedImage2, 3);
                                    grid.Children.Add(processedImage2);

                                    // OCR results
                                    var resultsPanel = new System.Windows.Controls.StackPanel
                                    {
                                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                                        Margin = new Thickness(10, 5, 10, 5)
                                    };

                                    var resultsLabel = new System.Windows.Controls.TextBlock
                                    {
                                        Text = "OCR Results:",
                                        FontWeight = FontWeights.Bold,
                                        Foreground = new SolidColorBrush(Colors.White),
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Margin = new Thickness(0, 0, 10, 0)
                                    };

                                    var resultsText = new System.Windows.Controls.TextBlock
                                    {
                                        Text = result,
                                        Foreground = new SolidColorBrush(Colors.Yellow),
                                        VerticalAlignment = VerticalAlignment.Center,
                                        TextWrapping = TextWrapping.Wrap
                                    };

                                    resultsPanel.Children.Add(resultsLabel);
                                    resultsPanel.Children.Add(resultsText);

                                    System.Windows.Controls.Grid.SetRow(resultsPanel, 4);
                                    grid.Children.Add(resultsPanel);

                                    // Set content and show
                                    resultWindow.Content = grid;
                                    resultWindow.ShowDialog();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error visualizing OCR results: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Improved character matching logic
        private double CalculateCharacterMatchScore(string screenText, Character character)
        {
            if (string.IsNullOrWhiteSpace(screenText)) return 0;

            double score = 0;
            string lowerText = screenText.ToLower();
            string targetName = character.Name.ToLower();
            string targetClass = character.Class?.ToLower() ?? "";

            // Name matching with higher weight
            // Exact name match gets highest score
            if (ContainsExactCharacterName(lowerText, targetName))
            {
                score += 0.8; // Even higher weight for exact name match
            }
            else if (lowerText.Contains(targetName))
            {
                score += 0.7; // Strong match for name contained in text
            }
            else
            {
                // Try fuzzy matching for name
                double nameScore = CalculateFuzzyMatchScore(lowerText, targetName);
                score += nameScore * 0.5; // Weight partial matches
            }

            // Class matching with enhanced pattern detection
            if (!string.IsNullOrWhiteSpace(character.Class))
            {
                // Try different class formats (Mage, mage, MAG, etc.)
                if (lowerText.Contains(targetClass))
                {
                    score += 0.2;
                }
                else
                {
                    // Common class abbreviations and variations
                    var classVariations = new List<string>();

                    // Standard abbreviation (first 3 chars)
                    if (targetClass.Length > 3)
                        classVariations.Add(targetClass.Substring(0, 3));

                    // Class-specific abbreviations
                    switch (targetClass)
                    {
                        case "warrior": classVariations.Add("war"); break;
                        case "paladin": classVariations.Add("pal"); classVariations.Add("pally"); break;
                        case "hunter": classVariations.Add("hunt"); break;
                        case "rogue": classVariations.Add("rog"); break;
                        case "priest": classVariations.Add("pri"); break;
                        case "death knight": classVariations.Add("dk"); classVariations.Add("death"); break;
                        case "shaman": classVariations.Add("sham"); classVariations.Add("shammy"); break;
                        case "mage": classVariations.Add("mag"); break;
                        case "warlock": classVariations.Add("lock"); classVariations.Add("wlock"); break;
                        case "monk": classVariations.Add("mnk"); break;
                        case "druid": classVariations.Add("dru"); break;
                        case "demon hunter": classVariations.Add("dh"); classVariations.Add("demon"); break;
                        case "evoker": classVariations.Add("evo"); break;
                    }

                    // Check for class variations
                    foreach (var variation in classVariations)
                    {
                        if (lowerText.Contains(variation))
                        {
                            score += 0.15;
                            break;
                        }
                    }
                }
            }

            // Level matching (with expanded formats)
            string[] levelPatterns = {
        $"level {character.Level}",
        $"lvl {character.Level}",
        $"lvl{character.Level}",
        $"level{character.Level}",
        $"lv {character.Level}",
        $"lv{character.Level}",
        $"level: {character.Level}",
        $"level:{character.Level}",
        $"{character.Level}"  // Raw number (might appear after name)
    };

            foreach (var pattern in levelPatterns)
            {
                if (lowerText.Contains(pattern))
                {
                    score += 0.15;
                    break;
                }
            }

            // Pattern bonus: Look for the typical pattern in WoW character selection
            // Format is usually "Name Level XX Class"
            string fullPattern = $"{targetName.ToLower()} level {character.Level} {targetClass.ToLower()}";
            string altPattern = $"{targetName.ToLower()} lvl {character.Level} {targetClass.ToLower()}";

            if (lowerText.Contains(fullPattern) || lowerText.Contains(altPattern))
            {
                score += 0.15; // Bonus for matching the exact expected pattern
            }

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

        private void EnsureIconDirectoriesExist()
        {
            try
            {
                string iconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons");
                if (!Directory.Exists(iconsPath))
                {
                    Directory.CreateDirectory(iconsPath);

                    // Log directory creation
                    Console.WriteLine($"Created icons directory at: {iconsPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating icons directory: {ex.Message}");
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
        private byte[] BitmapToByteArray(Bitmap bitmap)
        {
            // Create a byte array to hold the image data
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
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

        public string GetCharacterSelectionRecommendations()
        {
            StringBuilder recommendations = new StringBuilder();
            recommendations.AppendLine("## Character Selection Recommendations ##");
            recommendations.AppendLine();

            if (SelectedAccount?.SelectedCharacter == null)
            {
                recommendations.AppendLine("No character is currently selected. Please select a character first.");
                return recommendations.ToString();
            }

            Character target = SelectedAccount.SelectedCharacter;

            recommendations.AppendLine($"Target Character: {target.Name}");
            recommendations.AppendLine($"Level: {target.Level}");
            recommendations.AppendLine($"Class: {target.Class}");
            recommendations.AppendLine();

            // Check if name has special characters that might be hard for OCR
            bool hasSpecialChars = target.Name.Any(c => !char.IsLetterOrDigit(c));
            if (hasSpecialChars)
            {
                recommendations.AppendLine("⚠️ Character name contains special characters which may be difficult for OCR to recognize.");
                recommendations.AppendLine("   Consider using a character with a simpler name for more reliable selection.");
            }

            // Check if the name is very short
            if (target.Name.Length < 4)
            {
                recommendations.AppendLine("⚠️ Character name is very short. Short names may not be distinctive enough for reliable OCR detection.");
            }

            // Check if class is set
            if (string.IsNullOrWhiteSpace(target.Class))
            {
                recommendations.AppendLine("⚠️ Character class is not set. Setting the class improves selection accuracy.");
            }

            // Check if level is set
            if (target.Level <= 0)
            {
                recommendations.AppendLine("⚠️ Character level is not set or invalid. Setting the correct level improves selection accuracy.");
            }

            // Check for english.traineddata
            string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string engTrainedDataFile = Path.Combine(tessdataPath, "eng.traineddata");
            if (!File.Exists(engTrainedDataFile))
            {
                recommendations.AppendLine("⚠️ OCR language file (eng.traineddata) is missing. Character selection may not work properly.");
                recommendations.AppendLine("   Run the application as administrator and allow it to download the file when prompted.");
            }

            // Add recommendation for calibration
            string calibrationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_calibration.json");
            if (!File.Exists(calibrationFile))
            {
                recommendations.AppendLine("⚠️ OCR has not been calibrated. Running the calibration may improve selection accuracy.");
                recommendations.AppendLine("   Use the 'Calibrate OCR' button for best results.");
            }

            // If no issues found
            if (recommendations.Length <= 100)
            {
                recommendations.AppendLine("✓ Character selection setup looks good! All necessary information is provided.");
                recommendations.AppendLine("  For best results, make sure your WoW client is running in windowed mode or borderless fullscreen.");
            }

            return recommendations.ToString();
        }

        // Add a method for automatic tuning of OCR parameters
        private void OptimizeOcrParameters()
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

        private void TestOcrWithParameters(int blockSize, int cValue)
        {
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

        public async Task CalibrateCharacterSelectionOcr()
        {
            MessageBox.Show("Character selection OCR calibration will begin.\n\n" +
                           "Please make sure:\n" +
                           "1. WoW is running and on the character selection screen\n" +
                           "2. Your selected character is highlighted\n" +
                           "3. Don't move your mouse or use keyboard during calibration",
                           "OCR Calibration", MessageBoxButton.OK, MessageBoxImage.Information);

            // Create debug directory
            string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug");
            if (!Directory.Exists(debugDir))
                Directory.CreateDirectory(debugDir);

            // First, take a reference screenshot
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string referencePath = Path.Combine(debugDir, $"calibration_reference_{timestamp}.png");

            // Capture full screen for reference
            using (Bitmap fullScreen = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(fullScreen))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, Screen.PrimaryScreen.Bounds.Size);
                }
                fullScreen.Save(referencePath, ImageFormat.Png);
            }

            // Wait for a moment between captures
            await Task.Delay(500);

            // Test all preprocessing parameter combinations
            List<(int blockSize, int cValue, double score)> results = new List<(int, int, double)>();

            // Block sizes should always be odd numbers for adaptiveThreshold
            int[] blockSizes = { 7, 9, 11, 13, 15 };
            int[] cValues = { 2, 4, 6, 8, 10 };

            // Show progress dialog
            var progressWindow = new Window
            {
                Title = "OCR Calibration Progress",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
            };

            var progressPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20)
            };

            var progressText = new System.Windows.Controls.TextBlock
            {
                Text = "Testing OCR parameters...",
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var progressBar = new System.Windows.Controls.ProgressBar
            {
                Minimum = 0,
                Maximum = blockSizes.Length * cValues.Length,
                Height = 20,
                Value = 0
            };

            progressPanel.Children.Add(progressText);
            progressPanel.Children.Add(progressBar);
            progressWindow.Content = progressPanel;

            progressWindow.Show();

            int currentTest = 0;
            int totalTests = blockSizes.Length * cValues.Length;

            // Test each parameter combination
            foreach (int blockSize in blockSizes)
            {
                foreach (int cValue in cValues)
                {
                    currentTest++;
                    double score = await TestOcrParametersAsync(blockSize, cValue);
                    results.Add((blockSize, cValue, score));

                    // Update progress UI
                    WpfApplication.Current.Dispatcher.Invoke(() => {
                        progressText.Text = $"Testing OCR parameters: {currentTest}/{totalTests}";
                        progressBar.Value = currentTest;
                    });

                    // Wait between tests
                    await Task.Delay(300);
                }
            }

            // Close progress window
            progressWindow.Close();

            // Find the best parameter combination
            var bestResult = results.OrderByDescending(r => r.score).First();

            // Save the optimal parameters to a calibration file
            string calibrationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_calibration.json");
            var calibrationData = new
            {
                BlockSize = bestResult.blockSize,
                CValue = bestResult.cValue,
                CalibrationTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ScreenWidth = Screen.PrimaryScreen.Bounds.Width,
                ScreenHeight = Screen.PrimaryScreen.Bounds.Height
            };

            File.WriteAllText(calibrationFile, JsonSerializer.Serialize(calibrationData, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            // Update application to use these parameters
            _ocrBlockSize = bestResult.blockSize;
            _ocrCValue = bestResult.cValue;

            MessageBox.Show($"Calibration complete!\n\nBest parameters found:\nBlock Size: {bestResult.blockSize}\nC Value: {bestResult.cValue}\n\nThese settings have been saved and will be used for character selection.",
                            "Calibration Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Add these field variables to store OCR parameters
        private int _ocrBlockSize = 11; // Default value
        private int _ocrCValue = 8;     // Default value

        // Add the method to test OCR parameters
        private async Task<double> TestOcrParametersAsync(int blockSize, int cValue)
        {
            try
            {
                // Create debug directory
                string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug", "Calibration");
                if (!Directory.Exists(debugDir))
                    Directory.CreateDirectory(debugDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Get screen bounds
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;

                // Use the same capture area as the character selection
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

                // Capture the area
                Rectangle captureBounds = new Rectangle(captureX, captureY, captureWidth, captureHeight);

                using (Bitmap bitmap = new Bitmap(captureBounds.Width, captureBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(
                            new Point(captureBounds.Left, captureBounds.Top),
                            Point.Empty,
                            captureBounds.Size);
                    }

                    // Save the test image
                    string imagePath = Path.Combine(debugDir, $"test_b{blockSize}_c{cValue}_{timestamp}.png");
                    bitmap.Save(imagePath, ImageFormat.Png);

                    // Process the image using EmguCV with the test parameters
                    using (Image<Bgr, byte> image = new Image<Bgr, byte>(bitmap))
                    {
                        // Convert to grayscale
                        Image<Gray, byte> grayImage = image.Convert<Gray, byte>();

                        // Apply bilateral filter and CLAHE
                        CvInvoke.BilateralFilter(grayImage, grayImage, 9, 75, 75);

                        var clahe = new Emgu.CV.CvInvoke.Mat();
                        var claheTool = CvInvoke.CreateCLAHE(2.0, new DrawingSize(8, 8));
                        claheTool.Apply(grayImage, clahe);
                        grayImage = new Image<Gray, byte>(clahe.Bitmap);

                        // Apply custom parameters for thresholding
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

                        // Save the processed image
                        string processedPath = Path.Combine(debugDir, $"processed_b{blockSize}_c{cValue}_{timestamp}.png");
                        thresholdImage.Save(processedPath);

                        // Perform OCR
                        string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                        string result = "";

                        try
                        {
                            using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                            {
                                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.' ");
                                engine.SetVariable("tessedit_pageseg_mode", "6");
                                engine.SetVariable("tessedit_ocr_engine_mode", "2");

                                using (var img = ConvertEmguCvImageToPix(thresholdImage))
                                {
                                    using (var page = engine.Process(img, PageSegMode.SingleBlock))
                                    {
                                        result = page.GetText().Trim();
                                        string resultPath = Path.Combine(debugDir, $"ocr_b{blockSize}_c{cValue}_{timestamp}.txt");
                                        File.WriteAllText(resultPath, result);

                                        // Calculate score based on:
                                        // 1. Mean confidence from Tesseract
                                        // 2. Number of WoW-related keywords detected
                                        double meanConfidence = page.GetMeanConfidence();

                                        // Count known WoW keywords in the OCR result
                                        string[] wowKeywords = {"level", "lvl", "warrior", "paladin", "hunter", "rogue", "priest",
                                                        "shaman", "mage", "warlock", "druid", "monk", "death knight",
                                                        "demon hunter", "evoker", "alliance", "horde", "character", "realm"};

                                        int keywordMatches = 0;
                                        foreach (string keyword in wowKeywords)
                                        {
                                            if (result.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                                                keywordMatches++;
                                        }

                                        // Calculate combined score (70% confidence, 30% keyword presence)
                                        double keywordFactor = (double)keywordMatches / wowKeywords.Length;
                                        double combinedScore = (meanConfidence * 0.7) + (keywordFactor * 0.3);

                                        return combinedScore;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"OCR error for blockSize={blockSize}, cValue={cValue}: {ex.Message}");
                            return 0.0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parameter test error: {ex.Message}");
                return 0.0;
            }
        }

        // Add method to load OCR calibration if available
        private void LoadOcrCalibration()
        {
            try
            {
                string calibrationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_calibration.json");
                if (File.Exists(calibrationFile))
                {
                    string json = File.ReadAllText(calibrationFile);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;

                        if (root.TryGetProperty("BlockSize", out JsonElement blockSizeElement) &&
                            blockSizeElement.TryGetInt32(out int blockSize))
                        {
                            _ocrBlockSize = blockSize;
                        }

                        if (root.TryGetProperty("CValue", out JsonElement cValueElement) &&
                            cValueElement.TryGetInt32(out int cValue))
                        {
                            _ocrCValue = cValue;
                        }

                        Console.WriteLine($"Loaded OCR calibration: BlockSize={_ocrBlockSize}, CValue={_ocrCValue}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading OCR calibration: {ex.Message}");
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
