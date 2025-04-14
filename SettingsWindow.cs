using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WoWServerManager
{
    public class SettingsWindow : Window
    {
        private SettingsViewModel _viewModel;

        public SettingsWindow()
        {
            // Set window properties
            Title = "UI Customization";
            Width = 600;
            Height = 550;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            // Set the background similar to the main window
            Background = new ImageBrush
            {
                ImageSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/wow-background.jpg")),
                Opacity = 0.2,
                Stretch = Stretch.UniformToFill
            };
            
            // Create and set the view model
            _viewModel = new SettingsViewModel();
            
            // Create a scroll viewer for the content
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(20)
            };
            
            // Create the main panel
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(10)
            };
            scrollViewer.Content = mainPanel;
            
            // Add the title
            mainPanel.Children.Add(new TextBlock
            {
                Text = "UI CUSTOMIZATION",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#FFCC00")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 20)
            });
            
            // Theme Section
            AddSettingsSection(mainPanel, "THEME SELECTION", 
                "Choose between dark and light themes for the application.");
            
            var themeComboBox = new ComboBox
            {
                Margin = new Thickness(0, 10, 0, 20),
                ItemsSource = _viewModel.ThemeOptions,
                DisplayMemberPath = "Name",
                Style = Application.Current.Resources["WoWComboBoxStyle"] as Style,
                MinWidth = 200
            };
            themeComboBox.SetBinding(ComboBox.SelectedItemProperty, 
                new System.Windows.Data.Binding("SelectedTheme") 
                { 
                    Source = _viewModel, 
                    Mode = System.Windows.Data.BindingMode.TwoWay 
                });
            mainPanel.Children.Add(themeComboBox);
            
            // Accent Color Section
            AddSettingsSection(mainPanel, "ACCENT COLOR", 
                "Select a color scheme for buttons and highlights.");
            
            var accentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 20)
            };
            
            foreach (var accent in _viewModel.AccentOptions)
            {
                var accentButton = new RadioButton
                {
                    Content = accent.Name,
                    Margin = new Thickness(5),
                    GroupName = "AccentGroup",
                    Tag = accent,
                    Style = Application.Current.Resources["WoWRadioButtonStyle"] as Style
                };
                
                // Create a color preview next to the name
                var colorPreview = new Border
                {
                    Width = 16,
                    Height = 16,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent.ColorValue)),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                var buttonContent = new StackPanel
                {
                    Orientation = Orientation.Horizontal
                };
                buttonContent.Children.Add(new TextBlock { Text = accent.Name, VerticalAlignment = VerticalAlignment.Center });
                buttonContent.Children.Add(colorPreview);
                
                accentButton.Content = buttonContent;
                
                // Set up the checked handler
                accentButton.Checked += (sender, e) =>
                {
                    if (sender is RadioButton rb && rb.Tag is AccentColor accentColor)
                    {
                        _viewModel.SelectedAccent = accentColor;
                    }
                };
                
                // Check the button if it's the selected accent
                if (accent.Key == _viewModel.SelectedAccent.Key)
                {
                    accentButton.IsChecked = true;
                }
                
                accentPanel.Children.Add(accentButton);
            }
            
            mainPanel.Children.Add(accentPanel);
            
            // Font Size Section
            AddSettingsSection(mainPanel, "FONT SIZE", 
                "Adjust text size throughout the application.");
            
            var fontSizePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 20)
            };
            
            foreach (var fontSize in _viewModel.FontSizeOptions)
            {
                var fontSizeButton = new RadioButton
                {
                    Content = fontSize.Name,
                    Margin = new Thickness(5),
                    GroupName = "FontSizeGroup",
                    Tag = fontSize,
                    Style = Application.Current.Resources["WoWRadioButtonStyle"] as Style
                };
                
                // Set up the checked handler
                fontSizeButton.Checked += (sender, e) =>
                {
                    if (sender is RadioButton rb && rb.Tag is FontSize fontSizeOption)
                    {
                        _viewModel.SelectedFontSize = fontSizeOption;
                    }
                };
                
                // Check the button if it's the selected font size
                if (fontSize.Key == _viewModel.SelectedFontSize.Key)
                {
                    fontSizeButton.IsChecked = true;
                }
                
                fontSizePanel.Children.Add(fontSizeButton);
            }
            
            mainPanel.Children.Add(fontSizePanel);
            
            // Layout Options Section
            AddSettingsSection(mainPanel, "LAYOUT OPTIONS", 
                "Choose how items are displayed in lists and panels.");
            
            var layoutPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 20)
            };
            
            foreach (var layout in _viewModel.LayoutOptions)
            {
                var layoutButton = new RadioButton
                {
                    Content = layout.Name,
                    Margin = new Thickness(5),
                    GroupName = "LayoutGroup",
                    Tag = layout,
                    Style = Application.Current.Resources["WoWRadioButtonStyle"] as Style
                };
                
                // Set up the checked handler
                layoutButton.Checked += (sender, e) =>
                {
                    if (sender is RadioButton rb && rb.Tag is LayoutMode layoutOption)
                    {
                        _viewModel.SelectedLayout = layoutOption;
                    }
                };
                
                // Check the button if it's the selected layout
                if (layout.Key == _viewModel.SelectedLayout.Key)
                {
                    layoutButton.IsChecked = true;
                }
                
                layoutPanel.Children.Add(layoutButton);
            }
            
            mainPanel.Children.Add(layoutPanel);
            
            // UI Scaling Section
            AddSettingsSection(mainPanel, "UI SCALING", 
                "Adjust the overall size of the user interface.");
            
            var scaleSlider = new Slider
            {
                Minimum = 0.8,
                Maximum = 1.3,
                TickFrequency = 0.1,
                IsSnapToTickEnabled = true,
                TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                Margin = new Thickness(0, 10, 0, 5),
                Width = 300
            };
            scaleSlider.SetBinding(Slider.ValueProperty, 
                new System.Windows.Data.Binding("UIScale") 
                { 
                    Source = _viewModel, 
                    Mode = System.Windows.Data.BindingMode.TwoWay 
                });
            
            // Scale value indicators
            var scaleValuePanel = new Grid
            {
                Width = 300,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            for (int i = 0; i <= 5; i++)
            {
                var column = new ColumnDefinition();
                scaleValuePanel.ColumnDefinitions.Add(column);
                
                var value = 0.8 + (i * 0.1);
                var valueText = new TextBlock
                {
                    Text = value.ToString("0.0"),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7f8296"))
                };
                
                Grid.SetColumn(valueText, i);
                scaleValuePanel.Children.Add(valueText);
            }
            
            mainPanel.Children.Add(scaleSlider);
            mainPanel.Children.Add(scaleValuePanel);
            
            // Buttons section
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            var saveButton = new Button
            {
                Content = "Save Settings",
                Style = Application.Current.Resources["ButtonPrimaryStyle"] as Style,
                Margin = new Thickness(5),
                Padding = new Thickness(15, 10, 15, 10),
                Command = _viewModel.SaveSettingsCommand
            };
            
            var resetButton = new Button
            {
                Content = "Reset to Defaults",
                Style = Application.Current.Resources["ButtonStyle"] as Style,
                Margin = new Thickness(5),
                Padding = new Thickness(15, 10, 15, 10),
                Command = _viewModel.ResetToDefaultsCommand
            };
            
            var closeButton = new Button
            {
                Content = "Close",
                Style = Application.Current.Resources["ButtonStyle"] as Style,
                Margin = new Thickness(5),
                Padding = new Thickness(15, 10, 15, 10),
                IsCancel = true
            };
            closeButton.Click += (sender, e) => Close();
            
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(resetButton);
            buttonPanel.Children.Add(closeButton);
            
            mainPanel.Children.Add(buttonPanel);
            
            // Set the content
            Content = scrollViewer;
        }
        
        // Helper method to add section headers
        private void AddSettingsSection(StackPanel parent, string title, string description)
        {
            var sectionBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 70, 70, 70)),
                BorderBrush = new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 10, 0, 10),
                CornerRadius = new CornerRadius(3)
            };
            
            var sectionPanel = new StackPanel();
            sectionBorder.Child = sectionPanel;
            
            sectionPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCC00"))
            });
            
            if (!string.IsNullOrEmpty(description))
            {
                sectionPanel.Children.Add(new TextBlock
                {
                    Text = description,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 5, 0, 0)
                });
            }
            
            parent.Children.Add(sectionBorder);
        }
    }
}
