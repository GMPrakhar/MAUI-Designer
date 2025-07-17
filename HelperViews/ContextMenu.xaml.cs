using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;
using MAUIDesigner.HelperViews;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Controls;

namespace MAUIDesigner.HelperViews;

public partial class ContextMenu : ContentView
{
    public ObservableCollection<PropertyViewer> ActionList { get; set; } = new();
    private readonly IDictionary<string, string> IconMapping;

    public ContextMenu()
    {
        InitializeComponent();
        this.IsVisible = false;
        var projectRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var filePath = Path.Combine(projectRoot, "Resources", "Mappings", "iconMapping.json");

        var json = File.ReadAllText(filePath);
        IconMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    internal void UpdateCollectionView()
    {
        PropertySource.ItemsSource = ActionList;
    }

    public void Close()
    {
        this.IsVisible = false;
    }

    private void Show()
    {
        this.IsVisible = true;
        this.Focus();
        this.ZIndex = 10000;
    }

    public void Reset()
    {
        ActionList.Clear();
        UpdateCollectionView();
    }

    public void Show(TappedEventArgs e, Layout designerFrame)
    {
        var location = e.GetPosition(designerFrame).Value;
        this.Margin = new Thickness(location.X, location.Y, 0, 0);
        // Check if the click is on any element
        var targetElement = designerFrame.GetVisualTreeDescendants().FirstOrDefault(desc => desc is ElementDesignerView view && view.Frame.Contains(location)) as ElementDesignerView;
        if (targetElement != null)
        {
            UpdateContextMenuWithElementProperties(targetElement, designerFrame);
        }
        else
        {
            UpdateContextMenuForNonElement(designerFrame);
        }
        Show();
    }

    private void UpdateContextMenuForNonElement(Layout designerFrame)
    {
        Reset();
        var hoverRecognizer = CreateHoverRecognizer();

        AddContextMenuItem("Undo", designerFrame, (s, e) => ContextMenuActions.Undo(designerFrame, this, e), hoverRecognizer);
        AddContextMenuItem("Redo", designerFrame, (s, e) => ContextMenuActions.Redo(designerFrame, this, e), hoverRecognizer);

        foreach (var x in this.ActionList)
        {
            x.View.GestureRecognizers.Add(hoverRecognizer);
        }
    }

    private void UpdateContextMenuWithElementProperties(View targetElement, Layout designerFrame)
    {
        this.ActionList.Clear();
        var hoverRecognizer = CreateHoverRecognizer();

        var actionMethods = typeof(ContextMenuActions)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.GetCustomAttributes(typeof(ContextMenuActionAttribute), false).Length > 0);

        foreach (var method in actionMethods)
        {
            var attr = (ContextMenuActionAttribute)method.GetCustomAttributes(typeof(ContextMenuActionAttribute), false).First();
            string displayName = attr.DisplayName;

            // Create a delegate for the method
            EventHandler<EventArgs> handler = (s, e) =>
            {
                // Assume all methods have the same signature: (View, ContextMenu, EventArgs)
                method.Invoke(null, new object[] { targetElement, this, e });
            };

            AddContextMenuItem(displayName, targetElement, handler, hoverRecognizer);
        }

        foreach (var x in this.ActionList)
        {
            x.View.GestureRecognizers.Add(hoverRecognizer);
        }
    }

    private string GetIconForElement(string elementName)
    {
        elementName = elementName.ToLower();
        Debug.WriteLine("Context Element Name: " + elementName);
        return IconMapping.TryGetValue(elementName, out var icon) ? icon : IconMapping["Default"];
    }

    private void AddContextMenuItem(string text, View targetElement, EventHandler<EventArgs> clickHandler, PointerGestureRecognizer hoverRecognizer)
    {
        var iconImage = new Image
        {
            Source = new FontImageSource
            {
                FontFamily = "FluentIcons", // Ensure this matches the font family name in your project
                                            //Glyph = item3, // Use the specific icon name
                Glyph = GetIconForElement(text),
                //Size = Constants.ToolBoxItemImageSize,
                Color = Colors.White
            },
            WidthRequest = 10,
            HeightRequest = 10,
            VerticalOptions = LayoutOptions.Center,
            
        };

        var label = new Label()
        {
            Text = text,
            TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.DarkGray,
            //BackgroundColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.Black : Colors.White,
            Padding = new Thickness(5, 5, 5, 5),
            FontSize = 10,
            HorizontalOptions = LayoutOptions.Fill,
            HorizontalTextAlignment = TextAlignment.Start
        };

        var horizontalStack = new HorizontalStackLayout
        {
            Children = { iconImage, label },
            BackgroundColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.Gray : Colors.White,
            Padding = new Thickness(1)
        };

        var tapGestureRecognizer = new TapGestureRecognizer();
        tapGestureRecognizer.Tapped += (s, e) => clickHandler(targetElement, e);
        horizontalStack.GestureRecognizers.Add(tapGestureRecognizer);

        horizontalStack.GestureRecognizers.Add(hoverRecognizer);
        this.ActionList.Add(new PropertyViewer() { View = horizontalStack });
    }

    private PointerGestureRecognizer CreateHoverRecognizer()
    {
        var hoverRecognizer = new PointerGestureRecognizer();
        hoverRecognizer.PointerEntered += (s, e) => (s as View).BackgroundColor = Colors.LightGray.WithLuminosity(0.1f);
        hoverRecognizer.PointerExited += (s, e) => (s as View).BackgroundColor = Colors.Gray;
        return hoverRecognizer;
    }
}