using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace WoWServerManager
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        // Constants
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsFilePath;
        
        // Theme properties
        private ThemeMode _selectedTheme;
        private AccentColor _selectedAccent;
        private FontSize _selectedFontSize;
        private LayoutMode _selectedLayout;
        private double _uiScale = 1.0;
        
        // Available options
        public ObservableCollection<ThemeMode> ThemeOptions { get; }
        public ObservableCollection<AccentColor> AccentOptions { get; }
        public ObservableCollection<FontSize> FontSizeOptions { get; }
        public ObservableCollection<LayoutMode> LayoutOptions { get; }

        // Commands
        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }
        
        // Constructor
        public SettingsViewModel()
        {
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WoWServerManager",
                SettingsFileName);
            
            // Initialize collections
            ThemeOptions = new ObservableCollection<ThemeMode>
            {
                new ThemeMode { Name = "Dark", Key = "DarkTheme" },
                new ThemeMode { Name = "Light", Key = "LightTheme" }
            };
            
            AccentOptions = new ObservableCollection<AccentColor>
            {
                new AccentColor { Name = "Gold", ColorValue = "#FFCC00", Key = "GoldAccent" },
                new AccentColor { Name = "Blue", ColorValue = "#007BFF", Key = "BlueAccent" },
                new AccentColor { Name = "Green", ColorValue = "#28A745", Key = "GreenAccent" },
                new AccentColor { Name = "Purple", ColorValue = "#6F42C1", Key = "PurpleAccent" },
                new AccentColor { Name = "Red", ColorValue = "#DC3545", Key = "RedAccent" }
            };
            
            FontSizeOptions = new ObservableCollection<FontSize>
            {
                new FontSize { Name = "Small", SizeValue = 0.9, Key = "SmallFont" },
                new FontSize { Name = "Medium", SizeValue = 1.0, Key = "MediumFont" },
                new FontSize { Name = "Large", SizeValue = 1.2, Key = "LargeFont" }
            };
            
            LayoutOptions = new ObservableCollection<LayoutMode>
            {
                new LayoutMode { Name = "Cards", Key = "CardsLayout" },
                new LayoutMode { Name = "List", Key = "ListLayout" },
                new LayoutMode { Name = "Compact", Key = "CompactLayout" }
            };
            
            // Set defaults
            _selectedTheme = ThemeOptions[0]; // Dark theme default
            _selectedAccent = AccentOptions[0]; // Gold accent default
            _selectedFontSize = FontSizeOptions[1]; // Medium font default
            _selectedLayout = LayoutOptions[0]; // Cards layout default
            
            // Initialize commands
            SaveSettingsCommand = new MainViewModel.RelayCommand(_ => SaveSettings());
            ResetToDefaultsCommand = new MainViewModel.RelayCommand(_ => ResetToDefaults());
            
            // Load saved settings
            LoadSettings();
        }

        // Properties with notification
        public ThemeMode SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                    ApplyTheme();
                }
            }
        }

        public AccentColor SelectedAccent
        {
            get => _selectedAccent;
            set
            {
                if (_selectedAccent != value)
                {
                    _selectedAccent = value;
                    OnPropertyChanged();
                    ApplyAccentColor();
                }
            }
        }

        public FontSize SelectedFontSize
        {
            get => _selectedFontSize;
            set
            {
                if (_selectedFontSize != value)
                {
                    _selectedFontSize = value;
                    OnPropertyChanged();
                    ApplyFontSize();
                }
            }
        }

        public LayoutMode SelectedLayout
        {
            get => _selectedLayout;
            set
            {
                if (_selectedLayout != value)
                {
                    _selectedLayout = value;
                    OnPropertyChanged();
                    ApplyLayoutMode();
                }
            }
        }

        public double UIScale
        {
            get => _uiScale;
            set
            {
                if (_uiScale != value)
                {
                    _uiScale = value;
                    OnPropertyChanged();
                    ApplyUIScale();
                    // Remove the MessageBox
                }
            }
        }

        // Methods to apply changes
        private void ApplyTheme()
        {
            // Ensure we're modifying the application-wide resources
            var resources = System.Windows.Application.Current.Resources;

            if (_selectedTheme.Key == "DarkTheme")
            {
                // Dark theme colors
                resources["ColorBackground"] = (Color)ColorConverter.ConvertFromString("#0c0c17");
                resources["ColorSurface"] = (Color)ColorConverter.ConvertFromString("#131426");
                resources["ColorSurfaceRaised"] = (Color)ColorConverter.ConvertFromString("#1a1c36");
                resources["ColorBorder"] = (Color)ColorConverter.ConvertFromString("#2b2d4a");
                resources["ColorTextPrimary"] = (Color)ColorConverter.ConvertFromString("#ffffff");
                resources["ColorTextSecondary"] = (Color)ColorConverter.ConvertFromString("#b8b9cf");
                resources["ColorTextTertiary"] = (Color)ColorConverter.ConvertFromString("#7f8296");
            }
            else // Light theme
            {
                // Light theme colors
                resources["ColorBackground"] = (Color)ColorConverter.ConvertFromString("#f5f7fa");
                resources["ColorSurface"] = (Color)ColorConverter.ConvertFromString("#ffffff");
                resources["ColorSurfaceRaised"] = (Color)ColorConverter.ConvertFromString("#f8f9fa");
                resources["ColorBorder"] = (Color)ColorConverter.ConvertFromString("#dee2e6");
                resources["ColorTextPrimary"] = (Color)ColorConverter.ConvertFromString("#212529");
                resources["ColorTextSecondary"] = (Color)ColorConverter.ConvertFromString("#495057");
                resources["ColorTextTertiary"] = (Color)ColorConverter.ConvertFromString("#6c757d");
            }

            // Ensure brushes are updated
            UpdateBrushes();

            // Force all open windows to update their resources
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                window.Resources = resources;
                window.UpdateLayout();
            }
        }


        private void ApplyAccentColor()
        {
            var resources = System.Windows.Application.Current.Resources;

            // Apply selected accent color
            Color accentColor = (Color)ColorConverter.ConvertFromString(_selectedAccent.ColorValue);
            Color accentDarkColor = AdjustColorBrightness(accentColor, -0.2);

            resources["ColorPrimary"] = accentColor;
            resources["ColorPrimaryDark"] = accentDarkColor;

            // Update the light version of the primary color
            Color accentLightColor = accentColor;
            accentLightColor.A = 51; // 0.2 opacity (51/255)
            resources["PrimaryLightBrush"] = new SolidColorBrush(accentLightColor);

            // Ensure brushes are updated
            UpdateBrushes();

            // Force all open windows to update their resources
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                window.Resources = resources;
                window.UpdateLayout();
            }
        }


        private void ApplyFontSize()
        {
            var resources = System.Windows.Application.Current.Resources;

            // Base font sizes
            double baseSize = _selectedFontSize.SizeValue;

            // Apply font scaling to styles that contain FontSize
            UpdateFontSizeInStyles(resources, baseSize);

            // Force all open windows to update their resources
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                window.Resources = resources;
                window.UpdateLayout();
            }
        }

        private void ApplyLayoutMode()
        {
            var resources = System.Windows.Application.Current.Resources;

            switch (_selectedLayout.Key)
            {
                case "CardsLayout":
                    resources["ModernListBoxItemStyle"] = resources["CardListBoxItemStyle"];
                    break;
                case "ListLayout":
                    resources["ModernListBoxItemStyle"] = resources["BasicListBoxItemStyle"];
                    break;
                case "CompactLayout":
                    resources["ModernListBoxItemStyle"] = resources["CompactListBoxItemStyle"];
                    break;
            }

            // Force all open windows to update their resources
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                window.Resources = resources;
                window.UpdateLayout();
            }
        }

        private void ApplyUIScale()
        {
            try
            {
                // Remove the MessageBox from the UIScale property setter
                // You can move any logging or notification logic here if needed

                // Instead of directly transforming the window, 
                // we'll adjust the font size and element sizes based on the scale
                double scaleFactor = _uiScale;

                // Adjust application-wide resource dictionary
                var resources = Application.Current.Resources;

                // Scale font sizes
                if (resources.Contains("ButtonStyle") && resources["ButtonStyle"] is Style buttonStyle)
                {
                    foreach (Setter setter in buttonStyle.Setters)
                    {
                        if (setter.Property == Control.FontSizeProperty)
                        {
                            setter.Value = 12 * scaleFactor;
                        }
                    }
                }

                // You can add similar scaling for other styles like TextBlock, Label, etc.

                // Alternatively, you could use a global font size resource
                if (resources.Contains("GlobalFontSize"))
                {
                    resources["GlobalFontSize"] = 12 * scaleFactor;
                }

                // Optionally, notify the user about the scale change
                System.Diagnostics.Debug.WriteLine($"UI Scale set to: {scaleFactor}");
            }
            catch (Exception ex)
            {
                // Log the error or show a more detailed error message
                System.Windows.MessageBox.Show(
                    $"Error applying UI scale: {ex.Message}",
                    "Scale Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void UpdateBrushes()
        {
            // Get the application resources
            var resources = Application.Current.Resources;
            
            // Update SolidColorBrushes from Colors
            resources["BackgroundBrush"] = new SolidColorBrush((Color)resources["ColorBackground"]);
            resources["SurfaceBrush"] = new SolidColorBrush((Color)resources["ColorSurface"]);
            resources["SurfaceRaisedBrush"] = new SolidColorBrush((Color)resources["ColorSurfaceRaised"]);
            resources["PrimaryBrush"] = new SolidColorBrush((Color)resources["ColorPrimary"]);
            resources["PrimaryDarkBrush"] = new SolidColorBrush((Color)resources["ColorPrimaryDark"]);
            resources["BorderBrush"] = new SolidColorBrush((Color)resources["ColorBorder"]);
            resources["TextPrimaryBrush"] = new SolidColorBrush((Color)resources["ColorTextPrimary"]);
            resources["TextSecondaryBrush"] = new SolidColorBrush((Color)resources["ColorTextSecondary"]);
            resources["TextTertiaryBrush"] = new SolidColorBrush((Color)resources["ColorTextTertiary"]);
        }

        private void UpdateFontSizeInStyles(ResourceDictionary resources, double baseSize)
        {
            // Update existing styles that have FontSize
            if (resources["ButtonStyle"] is Style buttonStyle)
            {
                foreach (var setter in buttonStyle.Setters)
                {
                    if (setter is Setter fontSetter && fontSetter.Property == Control.FontSizeProperty)
                    {
                        fontSetter.Value = 12 * baseSize;
                    }
                }
            }
            
            // Similar updates for other styles with font sizes
            // This would need to be expanded for all styles in the application
        }

        // Helper method to darken or lighten a color
        private Color AdjustColorBrightness(Color color, double factor)
        {
            // Simple brightness adjustment
            byte r = (byte)Math.Clamp(color.R + (color.R * factor), 0, 255);
            byte g = (byte)Math.Clamp(color.G + (color.G * factor), 0, 255);
            byte b = (byte)Math.Clamp(color.B + (color.B * factor), 0, 255);
            
            return Color.FromArgb(color.A, r, g, b);
        }

        // Save settings to JSON file
        private void SaveSettings()
        {
            try
            {
                var settings = new SettingsData
                {
                    ThemeKey = _selectedTheme.Key,
                    AccentKey = _selectedAccent.Key,
                    FontSizeKey = _selectedFontSize.Key,
                    LayoutKey = _selectedLayout.Key,
                    UIScale = _uiScale
                };

                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);

                // Apply the settings immediately after saving
                ApplyTheme();
                ApplyAccentColor();
                ApplyFontSize();
                ApplyLayoutMode();
                ApplyUIScale();

                System.Windows.MessageBox.Show("Settings saved and applied successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Load settings from JSON file
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<SettingsData>(json);
                    
                    if (settings != null)
                    {
                        // Apply loaded settings
                        _selectedTheme = ThemeOptions.FirstOrDefault(t => t.Key == settings.ThemeKey) ?? ThemeOptions[0];
                        _selectedAccent = AccentOptions.FirstOrDefault(a => a.Key == settings.AccentKey) ?? AccentOptions[0];
                        _selectedFontSize = FontSizeOptions.FirstOrDefault(f => f.Key == settings.FontSizeKey) ?? FontSizeOptions[1];
                        _selectedLayout = LayoutOptions.FirstOrDefault(l => l.Key == settings.LayoutKey) ?? LayoutOptions[0];
                        _uiScale = settings.UIScale;
                        
                        // Apply all settings
                        ApplyTheme();
                        ApplyAccentColor();
                        ApplyFontSize();
                        ApplyLayoutMode();
                        ApplyUIScale();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Continue with defaults if settings can't be loaded
            }
        }

        // Reset settings to defaults
        private void ResetToDefaults()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                SelectedTheme = ThemeOptions[0]; // Dark
                SelectedAccent = AccentOptions[0]; // Gold
                SelectedFontSize = FontSizeOptions[1]; // Medium
                SelectedLayout = LayoutOptions[0]; // Cards
                UIScale = 1.0;
                
                SaveSettings();
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Settings data models
    public class ThemeMode
    {
        public string Name { get; set; }
        public string Key { get; set; }
    }

    public class AccentColor
    {
        public string Name { get; set; }
        public string ColorValue { get; set; }
        public string Key { get; set; }
    }

    public class FontSize
    {
        public string Name { get; set; }
        public double SizeValue { get; set; }
        public string Key { get; set; }
    }

    public class LayoutMode
    {
        public string Name { get; set; }
        public string Key { get; set; }
    }

    // Data class for serialization
    public class SettingsData
    {
        public string ThemeKey { get; set; }
        public string AccentKey { get; set; }
        public string FontSizeKey { get; set; }
        public string LayoutKey { get; set; }
        public double UIScale { get; set; }
    }
}
