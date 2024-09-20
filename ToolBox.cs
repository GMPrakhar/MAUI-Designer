
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MauiIcons.Fluent;

namespace MAUIDesigner
{
    internal class ToolBox
    {
        internal static IDictionary<ViewType, List<(string, Type, string)>> GetAllVisualElementsAlongWithType()
        {
            var visualElements = typeof(Microsoft.Maui.Controls.View).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Microsoft.Maui.Controls.View)) && !t.IsAbstract);
            var visualElementsWithType = new ConcurrentDictionary<ViewType, List<(string, Type, string)>>();
            foreach (var visualElement in visualElements)
            {
                //Debug.WriteLine("Visual Element: " + visualElement.Name);
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
            return elementName switch
            {
                //"Button" => FluentIcons.ControlButton20,
                //"CheckBox" => "\ue73e",
                //"DataGrid" => FluentIcons.DataArea20,
                //"Grid" => FluentIcons.Grid16,
                //"Image" => FluentIcons.Image16,
                ////"Label" => FluentIcons.Label,
                //"ListBox" => FluentIcons.List16,
                //"RadioButton" => FluentIcons.RadioButton20,
                //"Rectangle" => FluentIcons.RectangleLandscape12,
                //"StackPanel" => FluentIcons.StackPanel,
                //"TabControl" => FluentIcons.TabControl,
                //"TextBlock" => FluentIcons.TextBlock,
                //"TextBox" => FluentIcons.TextBox,
                _ => "\ue724" // Default icon if no match is found
            };
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
            if (value is string || value == null || value.GetType().IsPrimitive)
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
            else if (value.GetType().IsEnum)
            {
                var picker = new Picker
                {
                    ItemsSource = Enum.GetValues(value.GetType()).Cast<object>().ToList()
                };

                picker.SelectedIndexChanged += (s, e) =>
                {
                    var finalValue = Enum.Parse(value.GetType(), (s as Picker)!.SelectedItem.ToString());
                    property.SetValue(view, finalValue);
                };

                return picker;
            }
            else if (value.GetType() == typeof(Color) || value.GetType() == typeof(Thickness))
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

                if (value.GetType() == typeof(Color))
                {
                    ((Color)value).ToRgba(out var r,out var g,out var b,out var a);
                    red.Text = r.ToString();
                    green.Text = g.ToString();
                    blue.Text = b.ToString();
                    alpha.Text = a.ToString();
                }
                else
                {
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
                        if (value.GetType() == typeof(Color))
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
                Text = value.ToString(),
                FontSize = 10,
            };

            return label;
        }

        private static void GestureRecognizer_Tapped(object? sender, TappedEventArgs e)
        {
            Application.Current.MainPage.DisplayAlert("Error", "Invalid XAML", "OK");
        }

        private static void Editor_TextChanged(object? sender, TextChangedEventArgs e)
        {
            throw new NotImplementedException();
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
