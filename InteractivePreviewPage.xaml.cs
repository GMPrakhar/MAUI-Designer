using MAUIDesigner.XamlHelpers;
using System.Diagnostics;
using Extensions = Microsoft.Maui.Controls.Xaml.Extensions;

namespace MAUIDesigner;

[QueryProperty(nameof(XamlContent), "xamlContent")]
public partial class InteractivePreviewPage : ContentPage
{
    private string _xamlContent = string.Empty;

    public string XamlContent
    {
        get => _xamlContent;
        set
        {
            _xamlContent = value;
            if (IsLoaded)
            {
                LoadXamlContent();
            }
        }
    }

    public InteractivePreviewPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadXamlContent();
    }

    private void LoadXamlContent()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_xamlContent))
            {
                PreviewContentView.Content = new Label 
                { 
                    Text = "No content to preview", 
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center 
                };
                return;
            }

            // Create a new ContentPage from the XAML
            var tempPage = new ContentPage();
            Extensions.LoadFromXaml(tempPage, _xamlContent);
            
            // Extract the content and display it
            if (tempPage.Content != null)
            {
                PreviewContentView.Content = tempPage.Content;
            }
            else
            {
                PreviewContentView.Content = new Label 
                { 
                    Text = "Invalid XAML content", 
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center 
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading XAML content: {ex.Message}");
            PreviewContentView.Content = new StackLayout
            {
                Children =
                {
                    new Label 
                    { 
                        Text = "Error loading preview:", 
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.Center 
                    },
                    new Label 
                    { 
                        Text = ex.Message, 
                        HorizontalOptions = LayoutOptions.Center,
                        TextColor = Colors.Red
                    }
                },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
        }
    }

    private async void BackButton_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}