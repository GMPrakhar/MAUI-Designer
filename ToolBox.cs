﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner
{
    internal class ToolBox
    {
        internal static IDictionary<string, Type> GetAllVisualElementsAlongWithType()
        {
            var visualElements = typeof(Microsoft.Maui.Controls.View).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Microsoft.Maui.Controls.View)));
            var visualElementsWithType = new Dictionary<string, Type>();
            foreach (var visualElement in visualElements)
            {
                visualElementsWithType[visualElement.Name] = visualElement;
            }

            return visualElementsWithType;
        }

        // Get all properties for a given View
        internal static IDictionary<string, View> GetAllPropertiesForView(View view)
        {
            var viewProperties = view.GetType().GetProperties().Where(x => x.CanWrite);
            var properties = new Dictionary<string, View>();
            foreach (var property in viewProperties)
            {
                properties[property.Name] = GetViewForPropertyType(view, property, property.GetValue(view));
            }

            return properties;
        }

        internal static View GetViewForPropertyType(View view, PropertyInfo property, object value)
        {

            // Get the view according to the type of the value. If it is primitive type or string, it should be an editor, if it's enum it should be a picker
            if (value is string || value == null || value.GetType().IsPrimitive)
            {
                var editor = new Entry
                {
                    Text = value?.ToString() ?? "0"
                };

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

                var red = new Entry();
                var green = new Entry();
                var blue = new Entry();
                var alpha = new Entry();

                if(value.GetType() == typeof(Color))
                {
                    var color = (Color)value;
                    red.Text = color.Red.ToString();
                    green.Text = color.Green.ToString();
                    blue.Text = color.Blue.ToString();
                    alpha.Text = color.Alpha.ToString();
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
                        var valueA = byte.Parse(red.Text);
                        var valueB = byte.Parse(green.Text);
                        var valueC = byte.Parse(blue.Text);
                        var valueD = byte.Parse(alpha.Text);
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
                Text = value.ToString()
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
    }
}