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

namespace WoWServerManager
{
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
                _selectedServer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Expansions));
                SelectedExpansion = Expansions.FirstOrDefault();
            }
        }

        public ObservableCollection<Expansion> Expansions =>
            SelectedServer?.Expansions ?? new ObservableCollection<Expansion>();

        public Expansion SelectedExpansion
        {
            get => _selectedExpansion;
            set
            {
                _selectedExpansion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Accounts));
                SelectedAccount = Accounts.FirstOrDefault();
            }
        }

        public ObservableCollection<Account> Accounts =>
            SelectedExpansion?.Accounts ?? new ObservableCollection<Account>();

        public Account SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Characters)); // Add this line
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

                // If a character is selected, try to select it in the game
                if (SelectedAccount.SelectedCharacter != null)
                {
                    await SelectCharacterByName(SelectedAccount.SelectedCharacter.Name);
                }

                MessageBox.Show($"Game launched successfully with account: {SelectedAccount.Username}" +
                                (SelectedAccount.SelectedCharacter != null ? $" and attempted to select character: {SelectedAccount.SelectedCharacter.Name}" : ""),
                                "Launch Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SelectCharacterByName(string characterName)
        {
            const int maxAttempts = 15;          // Increased attempts for better chance of finding
            const double highConfidenceThreshold = 0.85;
            const double mediumConfidenceThreshold = 0.65;
            const int initialDelay = 1500;       // Longer initial delay to ensure character screen is loaded
            const int navigationDelay = 600;     // Delay between navigation key presses

            if (SelectedAccount?.SelectedCharacter == null)
            {
                SendKeys.SendWait("{ENTER}");
                return;
            }

            Character target = SelectedAccount.SelectedCharacter;
            Console.WriteLine($"Searching for: {target.Name} (Lvl {target.Level} {target.Class})");

            // Potential improvement: Wait for actual character selection screen to appear using image recognition
            await Task.Delay(initialDelay);

            // Go to top of character list
            SendKeys.SendWait("{HOME}");
            await Task.Delay(1200);

            // Store best match info
            double bestScore = 0;
            int bestPosition = -1;
            string bestMatchText = "";
            List<(int position, double score, string text)> matches = new List<(int, double, string)>();

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Take multiple samples at each position for better accuracy
                double positionScore = 0;
                string combinedText = "";

                // Take 2 samples at each position to improve reliability
                for (int sample = 0; sample < 2; sample++)
                {
                    string ocrText = CaptureScreenText();
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

                // High confidence - select immediately
                if (positionScore >= highConfidenceThreshold)
                {
                    Console.WriteLine($"Exact match found at position {attempt} - selecting");
                    SendKeys.SendWait("{ENTER}");
                    await Task.Delay(1000);
                    return;
                }

                // Move to next character
                SendKeys.SendWait("{DOWN}");
                await Task.Delay(navigationDelay);
            }

            // Log debug information for troubleshooting
            LogCharacterSelectionDebug(target, matches);

            // Analyze all matches for best candidate
            var sortedMatches = matches.OrderByDescending(m => m.score).ToList();

            // Check if we have multiple good candidates
            if (sortedMatches.Count >= 2 &&
                sortedMatches[0].score >= mediumConfidenceThreshold &&
                sortedMatches[1].score >= mediumConfidenceThreshold * 0.9)
            {
                // If the top two candidates are close in score, prefer the one with class and level match
                if (TryScoringClassAndLevel(sortedMatches[0].text, sortedMatches[1].text, target, out int betterPosition))
                {
                    bestPosition = betterPosition == 0 ? sortedMatches[0].position : sortedMatches[1].position;
                    bestScore = betterPosition == 0 ? sortedMatches[0].score : sortedMatches[1].score;
                    bestMatchText = betterPosition == 0 ? sortedMatches[0].text : sortedMatches[1].text;
                }
            }

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
                return;
            }

            // Final fallback
            Console.WriteLine("No good match found - selecting current character");
            SendKeys.SendWait("{ENTER}");
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

                // Get screen bounds (focus on right side where character list appears)
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;

                // Calculate capture area - these values work well for 1920x1080
                int captureWidth = (int)(screenBounds.Width * 0.3);  // 30% of screen width
                int captureHeight = (int)(screenBounds.Height * 0.7); // 70% of screen height
                int captureX = screenBounds.Width - captureWidth;    // Right side
                int captureY = (int)(screenBounds.Height * 0.15);    // Slightly below top

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

                    // Convert to EmguCV Image format
                    using (Image<Bgr, byte> emguImage = bitmap.ToImage<Bgr>())
                    {
                        // Process image with EmguCV for better OCR results
                        var processedImage = PreprocessImageWithEmguCV(emguImage);

                        string processedImagePath = Path.Combine(debugDirectory, $"ocr_processed_{timestamp}.png");
                        processedImage.Save(processedImagePath);

                        // Perform OCR with Tesseract
                        string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                        string result = "";

                        try
                        {
                            using (var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default))
                            {
                                // Configure Tesseract for game text
                                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789- '");
                                engine.SetVariable("tessedit_pageseg_mode", "6"); // Assume a single uniform block of text
                                engine.SetVariable("tessedit_ocr_engine_mode", "2"); // LSTM only

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
            // 1. Convert to grayscale
            Image<Gray, byte> grayImage = originalImage.Convert<Gray, byte>();

            // 2. Adaptive thresholding - works better for text with varying backgrounds
            Image<Gray, byte> thresholdImage = new Image<Gray, byte>(grayImage.Size);
            CvInvoke.AdaptiveThreshold(
                grayImage,
                thresholdImage,
                255.0,
                AdaptiveThresholdType.GaussianC,
                ThresholdType.Binary,
                11, // Block size
                5   // C value
            );

            // 3. Apply morphological operations to clean up the image
            var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle,
                                                      new DrawingSize(3, 3),
                                                      new Point(-1, -1));

            // Opening operation (erosion followed by dilation) to remove noise
            CvInvoke.MorphologyEx(thresholdImage, thresholdImage, MorphOp.Open, element,
                                 new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

            // 4. Apply Gaussian blur to smooth out edges
            CvInvoke.GaussianBlur(thresholdImage, thresholdImage, new DrawingSize(3, 3), 0);

            // 5. Apply contrast enhancement
            Image<Gray, byte> enhancedImage = thresholdImage.Clone();

            // Optional: Apply contrast stretching
            // This helps when characters have low contrast against background
            double min = 0, max = 255;
            Point minLoc = new Point(), maxLoc = new Point();
            CvInvoke.MinMaxLoc(enhancedImage, ref min, ref max, ref minLoc, ref maxLoc);

            if (max > min) // Avoid division by zero
            {
                // Normalize the image to improve contrast
                CvInvoke.Normalize(enhancedImage, enhancedImage, 0, 255, NormType.MinMax);
            }

            return enhancedImage;
        }

        // Improved character matching logic
        private double CalculateCharacterMatchScore(string screenText, Character character)
        {
            if (string.IsNullOrWhiteSpace(screenText)) return 0;

            double score = 0;
            string lowerText = screenText.ToLower();
            string targetName = character.Name.ToLower();
            string targetClass = character.Class?.ToLower() ?? "";

            // Name matching (more strict) - use improved fuzzy matching
            if (lowerText.Contains(targetName))
            {
                score += 0.7; // Strong match for exact name
            }
            else
            {
                // Try fuzzy matching for name
                double nameScore = CalculateFuzzyMatchScore(lowerText, targetName);
                score += nameScore * 0.5; // Weight partial matches
            }

            // Class matching (more strict)
            if (!string.IsNullOrWhiteSpace(character.Class))
            {
                if (lowerText.Contains(targetClass))
                {
                    score += 0.2;
                }
                else
                {
                    // Try matching class abbreviations (e.g., "Dru" for "Druid")
                    string classAbbrev = character.Class.Length > 3
                        ? character.Class.Substring(0, 3).ToLower()
                        : character.Class.ToLower();

                    if (lowerText.Contains(classAbbrev))
                    {
                        score += 0.1;
                    }
                }
            }

            // Level matching (with various formats)
            string[] levelPatterns = {
                $"level {character.Level}",
                $"lvl {character.Level}",
                $"lvl{character.Level}",
                $"level{character.Level}",
                $"lv {character.Level}",
                $"lv{character.Level}",
                $"{character.Level}"
            };

            foreach (var pattern in levelPatterns)
            {
                if (lowerText.Contains(pattern))
                {
                    score += 0.15;
                    break;
                }
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
        private byte[] ImageToByteArray(Image image)
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

                // Load image with EmguCV
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

                using (Bitmap bitmap = new Bitmap(captureBounds.Width, captureBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(
                            new System.Drawing.Point(captureBounds.Left, captureBounds.Top),
                            System.Drawing.Point.Empty,
                            captureBounds.Size);
                    }

                    // Convert to EmguCV Image
                    using (Image<Bgr, byte> emguImage = bitmap.ToImage<Bgr, byte>())
                    {
                        // Convert to grayscale
                        Image<Gray, byte> grayImage = emguImage.Convert<Gray, byte>();

                        // Apply custom parameters
                        var thresholdImage = new Image<Gray, byte>(grayImage.Size);
                        CvInvoke.AdaptiveThreshold(
                            grayImage,
                            thresholdImage,
                            255.0,
                            AdaptiveThresholdType.GaussianC,
                            ThresholdType.Binary,
                            blockSize,
                            cValue
                        );

                        string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR_Debug", "Calibration");
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
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
            private int _launchDelayMs = 5000; // Default to 5 seconds
            private int _characterSelectDelayMs = 8000; // Default to 8 seconds
            private ObservableCollection<Account> _accounts;
            private Server _server; // Reference to parent server

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
                Height = 300; // Increased height for additional field
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

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

                // Name label and textbox
                var nameLabel = new System.Windows.Controls.Label { Content = "Expansion Name:", VerticalAlignment = VerticalAlignment.Center };
                System.Windows.Controls.Grid.SetRow(nameLabel, 0);
                System.Windows.Controls.Grid.SetColumn(nameLabel, 0);

                var nameTextBox = new System.Windows.Controls.TextBox { Margin = new Thickness(5), Text = Expansion.Name };
                nameTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Name") { Source = Expansion, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
                System.Windows.Controls.Grid.SetRow(nameTextBox, 0);
                System.Windows.Controls.Grid.SetColumn(nameTextBox, 1);
                System.Windows.Controls.Grid.SetColumnSpan(nameTextBox, 2);

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
                grid.Children.Add(nameTextBox);
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

    // Extension method to convert Bitmap to EmguCV Image
    public static class BitmapExtensions
    {
        public static Image<TColor, byte> ToImage<TColor>(this Bitmap bitmap)
            where TColor : struct, IColor
        {
            return new Image<TColor, byte>(bitmap);
        }
    }
}
