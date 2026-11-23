using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.Views;

public enum SavePromptResult
{
    Save,
    Discard,
    Cancel
}

public sealed class SaveConfirmationDialog : Window
{
    public SavePromptResult Result { get; private set; } = SavePromptResult.Cancel;

    public SaveConfirmationDialog(string projectName)
    {
        Title = Strings.SavePromptTitle;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        StackPanel root = new()
        {
            Margin = new Thickness(20),
            Spacing = 16
        };

        TextBlock message = new()
        {
            Text = string.Format(CultureInfo.CurrentCulture, Strings.SavePromptMessage, projectName),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        };
        root.Children.Add(message);

        StackPanel buttonsPanel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        Button yesButton = new()
        {
            Content = Strings.ButtonYes,
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        yesButton.Click += (_, _) =>
        {
            Result = SavePromptResult.Save;
            Close();
        };

        Button noButton = new()
        {
            Content = Strings.ButtonNo,
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        noButton.Click += (_, _) =>
        {
            Result = SavePromptResult.Discard;
            Close();
        };

        Button cancelButton = new()
        {
            Content = Strings.ButtonCancel,
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        cancelButton.Click += (_, _) =>
        {
            Result = SavePromptResult.Cancel;
            Close();
        };

        buttonsPanel.Children.Add(yesButton);
        buttonsPanel.Children.Add(noButton);
        buttonsPanel.Children.Add(cancelButton);

        root.Children.Add(buttonsPanel);
        Content = root;
    }
}
