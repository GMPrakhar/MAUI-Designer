
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MAUIDesigner.HelperViews;
using MauiIcons.Fluent;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Xamls = Microsoft.UI.Xaml;

namespace MAUIDesigner
{
    internal class ToolBox
    {
        private static readonly IDictionary<string, string> IconMapping;

        internal static Layout MainDesignerView;

        public static ContextMenu contextMenu = new ContextMenu()
        {
            IsVisible = false
        };

        static ToolBox()
        {
            var projectRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..","..");
            var filePath = Path.Combine(projectRoot, "Resources", "Mappings", "iconMapping.json");

            var json = File.ReadAllText(filePath);
            IconMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        internal static IDictionary<ViewType, List<(string, Type, string)>> GetAllVisualElementsAlongWithType()
        {
            var visualElements = typeof(Microsoft.Maui.Controls.View).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Microsoft.Maui.Controls.View)) && !t.IsAbstract);
            var visualElementsWithType = new ConcurrentDictionary<ViewType, List<(string, Type, string)>>();

            foreach (var visualElement in visualElements)
            {
                var viewType = GetViewTypeForType(visualElement);
                if (!visualElementsWithType.ContainsKey(viewType))
                {
                    visualElementsWithType[viewType] = [];
                }

                var icon = GetIconForElement(visualElement.Name);

                visualElementsWithType[viewType].Add((visualElement.Name, visualElement, icon));
            }
            return visualElementsWithType;
        }

        private static string GetIconForElement(string elementName)
        {
            Debug.WriteLine("Element Name: " + elementName);
            return IconMapping.TryGetValue(elementName, out var icon) ? icon : IconMapping["Default"];
        }

        // Get all properties for a given View
        internal static IDictionary<string, View> GetAllPropertiesForView(View view)
        {
            var viewProperties = view.GetType().GetProperties().Where(x => x.CanWrite);
            var properties = new ConcurrentDictionary<string, View>();
            Parallel.ForEach(viewProperties, property =>
            {
                if (property.GetIndexParameters().Length == 0)
                {
                    properties[property.Name] = GetViewForPropertyType(view, property, property.GetValue(view));
                }
            });

            return properties;
        }

        internal static View GetViewForPropertyType(View view, PropertyInfo property, object value)
        {

            // Get the view according to the type of the value. If it is primitive type or string, it should be an editor, if it's enum it should be a picker
            if (property.PropertyType == typeof(string) ||  property.PropertyType.IsPrimitive)
            {
                var editor = new Entry
                {
                    Text = value?.ToString() ?? "0",
                    FontSize = 10,
                    VerticalOptions = LayoutOptions.Center,
                };

                editor.HeightRequest = 10;

                editor.TextChanged += (s, e) =>
                {
                    try
                    {
                        var propertyType = property.PropertyType;
                        var finalValue = Convert.ChangeType((s as Entry)!.Text, propertyType);
                        property.SetValue(view, finalValue);
                    }
                    catch
                    {

                    }
                };

                return editor;
            }
            else if (property.PropertyType.IsEnum)
            {
                var picker = new Picker
                {
                    ItemsSource = Enum.GetValues(property.PropertyType).Cast<object>().ToList()
                };

                picker.SelectedIndexChanged += (s, e) =>
                {
                    var finalValue = Enum.Parse(property.PropertyType, (s as Picker)!.SelectedItem!.ToString());
                    property.SetValue(view, finalValue);
                };

                return picker;
            }
            else if (property.PropertyType == typeof(ColumnDefinitionCollection))
            {
                var columnDefinitionCollection = (ColumnDefinitionCollection)value;
                var columnDefintionString = string.Join(',',columnDefinitionCollection.Select(columnDefinition => columnDefinition.Width.ToString()));
                var editor = new Entry
                {
                    Text = columnDefintionString,
                    FontSize = 10,
                    VerticalOptions = LayoutOptions.Center,
                    HeightRequest = 10
                };

                editor.TextChanged += (s, e) =>
                {
                    try
                    {
                        var text = (s as Entry)!.Text;
                        var columnDefinitions = text.Split(',').Select(x => {
                            var gridData = x.Split('.');
                            return new ColumnDefinition(new GridLength(double.Parse(gridData[0]), Enum.Parse<GridUnitType>(gridData[1])));
                        });
                        var columnDefinitionCollection = new ColumnDefinitionCollection(columnDefinitions.ToArray());
                        property.SetValue(view, columnDefinitionCollection);
                    }
                    catch
                    {

                    }
                };

                return editor;

            }
            else if (property.PropertyType == typeof(RowDefinitionCollection))
            {
                var RowDefinitionCollection = (RowDefinitionCollection)value;
                var rowDefintionString = string.Join(',', RowDefinitionCollection.Select(RowDefinition => RowDefinition.Height.ToString()));
                var editor = new Entry
                {
                    Text = rowDefintionString,
                    FontSize = 10,
                    VerticalOptions = LayoutOptions.Center,
                    HeightRequest = 10
                };

                editor.TextChanged += (s, e) =>
                {
                    try
                    {
                        var text = (s as Entry)!.Text;
                        var RowDefinitions = text.Split(',').Select(x => {
                            var gridData = x.Split('.');
                            return new RowDefinition(new GridLength(double.Parse(gridData[0]), Enum.Parse<GridUnitType>(gridData[1])));
                        });
                        var RowDefinitionCollection = new RowDefinitionCollection(RowDefinitions.ToArray());
                        property.SetValue(view, RowDefinitionCollection);
                    }
                    catch
                    {

                    }
                };

                return editor;

            }
            else if (property.PropertyType == typeof(Color) || property.PropertyType == typeof(Thickness))
            {
                var colorGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }
                },
                    InputTransparent = true,
                    CascadeInputTransparent = false,
                };

                var red = new Entry() { HeightRequest = 10, FontSize = 10 };
                var green = new Entry() { HeightRequest = 10, FontSize = 10 };
                var blue = new Entry() { HeightRequest = 10, FontSize = 10 };
                var alpha = new Entry() { HeightRequest = 10, FontSize = 10 };

                if (property.PropertyType == typeof(Color))
                {
                    if(value == null) value = new Color();
                    ((Color)value).ToRgba(out var r, out var g, out var b, out var a);
                    red.Text = r.ToString();
                    green.Text = g.ToString();
                    blue.Text = b.ToString();
                    alpha.Text = a.ToString();
                }
                else
                {
                    if (value == null) value = new Thickness();
                    var thickness = (Thickness)value;
                    red.Text = thickness.Left.ToString();
                    green.Text = thickness.Top.ToString();
                    blue.Text = thickness.Right.ToString();
                    alpha.Text = thickness.Bottom.ToString();
                }

                colorGrid.Children.Add(red);
                colorGrid.Children.Add(green);
                colorGrid.Children.Add(blue);
                colorGrid.Children.Add(alpha);

                colorGrid.SetColumn(red, 0);
                colorGrid.SetColumn(green, 1);
                colorGrid.SetColumn(blue, 2);
                colorGrid.SetColumn(alpha, 3);

                red.TextChanged += TextChanged;
                green.TextChanged += TextChanged;
                blue.TextChanged += TextChanged;
                alpha.TextChanged += TextChanged;

                void TextChanged(object? sender, TextChangedEventArgs e)
                {
                    try
                    {
                        var valueA = int.Parse(red.Text);
                        var valueB = int.Parse(green.Text);
                        var valueC = int.Parse(blue.Text);
                        var valueD = int.Parse(alpha.Text);
                        if (property.PropertyType == typeof(Color))
                        {
                            var color = Color.FromRgba(valueA, valueB, valueC, valueD);
                            property.SetValue(view, color);
                        }
                        else
                        {
                            var thickness = new Thickness(valueA, valueB, valueC, valueD);
                            property.SetValue(view, thickness);
                        }
                    }
                    catch { }
                }

                return colorGrid;
            }
            var label = new Label
            {
                Text = value?.ToString() ?? "",
                FontSize = 10,
            };

            return label;
        }

        internal static void ShowContextMenu(object? sender, TappedEventArgs e)
        {
            contextMenu.Show(e, MainDesignerView);
        }

        public static ViewType GetViewTypeForType(Type type)
        {
            return type switch
            {
                Type _ when type.IsSubclassOf(typeof(Microsoft.Maui.Controls.ContentView)) => ViewType.ContentView,
                Type _ when type.IsSubclassOf(typeof(Microsoft.Maui.Controls.ItemsView)) => ViewType.ItemsView,
                Type _ when type.IsSubclassOf(typeof(Microsoft.Maui.Controls.TemplatedView)) => ViewType.TemplatedView,
                Type _ when type.IsSubclassOf(typeof(Microsoft.Maui.Controls.Shapes.Shape)) => ViewType.Shape,
                Type _ when type.IsSubclassOf(typeof(Microsoft.Maui.Controls.Layout)) || type.ToString().Contains("Layout") => ViewType.Layout,
                Type _ when type.IsSubclassOf(typeof(Microsoft.Maui.Controls.View)) => ViewType.View,
                _ => ViewType.Other,
            };
        }

        public static void AddElementsForToolbox(Layout toolbox)
        {
            var allVisualElements = ToolBox.GetAllVisualElementsAlongWithType();

            foreach (var viewType in allVisualElements.Keys)
            {
                var viewsForType = allVisualElements[viewType];

                var label = new Label
                {
                    Text = viewType.ToString(),
                    FontSize = 15,
                    Padding = new Thickness(10, 10, 0, 0),
                    HorizontalOptions = LayoutOptions.Start,
                    FontAttributes = FontAttributes.Bold,
                };

                toolbox.Add(label);

                foreach (var view in viewsForType)
                {
                    var tmpGrid = new Grid
                    {
                        RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
                        ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
                        HorizontalOptions = LayoutOptions.Start
                    };

                    var iconImage = new Image
                    {
                        Source = new FontImageSource
                        {
                            FontFamily = "FluentIcons", // Ensure this matches the font family name in your project
                                                        //Glyph = item3, // Use the specific icon name
                            Glyph = view.Item3,
                            //Size = Constants.ToolBoxItemImageSize,
                            Color = Colors.White
                        },
                        WidthRequest = Constants.ToolBoxItemImageWidth,
                        HeightRequest = Constants.ToolBoxItemImageHeight,
                        VerticalOptions = LayoutOptions.Center
                    };

                    var textLabel = new Label
                    {
                        FontSize = Constants.ToolBoxItemLabelSize,
                        TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
                        Padding = new Thickness(3),
                        BackgroundColor = Color.FromRgba(0, 0, 0, 0),
                        Text = " " + view.Item1,
                        VerticalOptions = LayoutOptions.Center
                    };

                    var horizontalStack = new HorizontalStackLayout
                    {
                        Children = { iconImage, textLabel }
                    };

                    tmpGrid.Children.Add(horizontalStack);
                    Grid.SetColumn(horizontalStack, 0);

                    var gestureRecognizer = new TapGestureRecognizer();
                    gestureRecognizer.Tapped += (s,e) => ElementOperations.CreateElementInDesignerFrame(MainDesignerView, s);
                    var pointerGestureRecognizer = new PointerGestureRecognizer();
                    pointerGestureRecognizer.PointerEntered += RaiseLabel;
                    pointerGestureRecognizer.PointerExited += MakeLabelDefault;
                    horizontalStack.GestureRecognizers.Add(gestureRecognizer);
                    horizontalStack.GestureRecognizers.Add(pointerGestureRecognizer);

                    toolbox.Add(tmpGrid);
                }
            }
        }

        private static void RaiseLabel(object? sender, PointerEventArgs e)
        {
            var senderView = sender as HorizontalStackLayout;
            if (senderView != null && senderView.Children.Count > 1)
            {
                var label = senderView.Children[1] as Label;
                if (label != null)
                {
                    var animation = new Animation(s => label.FontSize = s, Constants.ToolBoxItemLabelSize, Constants.ToolBoxItemLabelAnimateSize);
                    label.Animate("FontSize", animation, 16, 100);
                }
            }
        }

        private static void MakeLabelDefault(object? sender, PointerEventArgs e)
        {
            var senderView = sender as HorizontalStackLayout;
            if (senderView != null && senderView.Children.Count > 1)
            {
                var label = senderView.Children[1] as Label;
                if (label != null)
                {
                    var animation = new Animation(s => label.FontSize = s, Constants.ToolBoxItemLabelAnimateSize, Constants.ToolBoxItemLabelSize);
                    label.Animate("FontSize", animation, 16, 100);
                }
            }
        }
    }
    public enum ViewType
    {
        View,
        ContentView,
        ItemsView,
        TemplatedView,
        Shape,
        Layout,
        Other,
    }
}
