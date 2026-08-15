import { Injectable } from '@angular/core';
import { MauiElement, ElementType, ElementProperties, Thickness, GridDefinition, GridLength, GridLengthType, Orientation } from '../models/maui-element';

@Injectable({
  providedIn: 'root'
})
export class XamlGeneratorService {

  constructor() { }

  generateXaml(rootElement: MauiElement): string {
    const xamlContent = this.generateElementXaml(rootElement, 0);
    
    return `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="YourApp.MainPage">
${xamlContent}
</ContentPage>`;
  }

  private generateElementXaml(element: MauiElement, indentLevel: number): string {
    const indent = '    '.repeat(indentLevel + 1);

    const elementName = this.getXamlElementName(element);
    const attributes = this.generateAttributes(element);
    const hasChildren = element.children && element.children.length > 0;
    const isGrid = element.type === ElementType.Grid;

    if (!hasChildren && !isGrid) {
      return `${indent}<${elementName}${attributes} />`;
    }

    let xaml = `${indent}<${elementName}${attributes}>`;

    // Add special content for certain layouts
    if (isGrid) {
      xaml += this.generateGridDefinitions(element, indentLevel + 1);
    }

    // Add children (a CollectionView wraps them in a DataTemplate)
    if (element.type === ElementType.CollectionView) {
      xaml += this.generateItemTemplate(element, indentLevel + 1);
    } else {
      for (const child of element.children) {
        xaml += '\n' + this.generateElementXaml(child, indentLevel + 1);
      }
    }

    xaml += `\n${indent}</${elementName}>`;

    return xaml;
  }

  private generateItemTemplate(element: MauiElement, indentLevel: number): string {
    const indent = '    '.repeat(indentLevel + 1);
    const inner = '    '.repeat(indentLevel + 2);
    let xaml = `\n${indent}<CollectionView.ItemTemplate>`;
    xaml += `\n${inner}<DataTemplate>`;
    for (const child of element.children) {
      xaml += '\n' + this.generateElementXaml(child, indentLevel + 2);
    }
    xaml += `\n${inner}</DataTemplate>`;
    xaml += `\n${indent}</CollectionView.ItemTemplate>`;
    return xaml;
  }

  private getXamlElementName(element: MauiElement): string {
    switch (element.type) {
      case ElementType.StackLayout:
        return element.properties.orientation === Orientation.Horizontal
          ? 'HorizontalStackLayout'
          : 'VerticalStackLayout';
      case ElementType.VerticalStackLayout:
        return 'VerticalStackLayout';
      case ElementType.AbsoluteLayout:
        return 'AbsoluteLayout';
      case ElementType.Grid:
        return 'Grid';
      case ElementType.Frame:
        return 'Frame';
      case ElementType.ScrollView:
        return 'ScrollView';
      case ElementType.Label:
        return 'Label';
      case ElementType.Button:
        return 'Button';
      case ElementType.Entry:
        return 'Entry';
      case ElementType.Editor:
        return 'Editor';
      case ElementType.Image:
        return 'Image';
      case ElementType.Path:
        return 'Path';
      default:
        return element.type;
    }
  }

  private generateAttributes(element: MauiElement): string {
    const props = element.properties;
    const attributes: string[] = [];
    const bindings = props.bindings || {};

    /** A bound property wins over the literal designer value. */
    const push = (name: string, value: string | number | undefined) => {
      if (bindings[name]) {
        return;
      }
      if (value !== undefined && value !== null && value !== '') {
        attributes.push(`${name}="${value}"`);
      }
    };
    
    // Add name attribute
    if (element.name) {
      attributes.push(`x:Name="${element.name}"`);
    }
    
    // For children of AbsoluteLayout
    if (element.parent?.type === ElementType.AbsoluteLayout) {
      if (props.x !== undefined && props.y !== undefined && props.width !== undefined && props.height !== undefined) {
        attributes.push(`AbsoluteLayout.LayoutBounds="${props.x},${props.y},${props.width},${props.height}"`);
        attributes.push(`AbsoluteLayout.LayoutFlags="None"`);
      }
    }
    
    if (element.parent?.type === ElementType.Grid) {
      if (props.row !== undefined) {
        attributes.push(`Grid.Row="${props.row}"`);
      }
      if (props.column !== undefined) {
        attributes.push(`Grid.Column="${props.column}"`);
      }
      if (props.rowSpan !== undefined && props.rowSpan > 1) {
        attributes.push(`Grid.RowSpan="${props.rowSpan}"`);
      }
      if (props.columnSpan !== undefined && props.columnSpan > 1) {
        attributes.push(`Grid.ColumnSpan="${props.columnSpan}"`);
      }
    }
    
    // Size attributes
    if (props.width !== undefined) {
      attributes.push(`WidthRequest="${props.width}"`);
    }
    if (props.height !== undefined) {
      attributes.push(`HeightRequest="${props.height}"`);
    }
    
    // Text content
    if (props.text !== undefined && props.text !== '') {
      push('Text', this.escapeXml(props.text));
    }

    if (props.placeholder) {
      push('Placeholder', this.escapeXml(props.placeholder));
    }

    // Control state
    if (props.isChecked !== undefined && element.type === ElementType.CheckBox) {
      push('IsChecked', props.isChecked ? 'True' : 'False');
    }
    if (props.isToggled !== undefined && element.type === ElementType.Switch) {
      push('IsToggled', props.isToggled ? 'True' : 'False');
    }
    if (element.type === ElementType.Slider || element.type === ElementType.Stepper) {
      push('Minimum', props.minimum);
      push('Maximum', props.maximum);
      push('Value', props.value);
      if (element.type === ElementType.Stepper) {
        push('Increment', props.increment);
      }
    }
    if (element.type === ElementType.ProgressBar) {
      push('Progress', props.progress);
    }
    if (element.type === ElementType.ActivityIndicator) {
      push('IsRunning', props.isRunning === false ? 'False' : 'True');
    }
    if (element.type === ElementType.DatePicker && props.date) {
      push('Date', props.date);
    }
    if (element.type === ElementType.Border) {
      push('Stroke', props.borderColor);
      push('StrokeThickness', props.borderWidth);
      if (props.cornerRadius !== undefined) {
        attributes.push(`StrokeShape="RoundRectangle ${props.cornerRadius}"`);
      }
    }
    if (element.type === ElementType.Frame && props.cornerRadius !== undefined) {
      push('CornerRadius', props.cornerRadius);
    }

    if (element.type === ElementType.Path) {
      if (props.pathData) {
        attributes.push(`Data="${this.escapeXml(props.pathData)}"`);
      }
      if (props.fillColor) {
        attributes.push(`Fill="${this.escapeXml(props.fillColor)}"`);
      }
      if (props.strokeColor) {
        attributes.push(`Stroke="${this.escapeXml(props.strokeColor)}"`);
      }
      if (props.strokeThickness !== undefined) {
        attributes.push(`StrokeThickness="${props.strokeThickness}"`);
      }
    }
    
    // Colors
    if (props.backgroundColor) {
      attributes.push(`BackgroundColor="${props.backgroundColor}"`);
    }
    if (props.textColor) {
      attributes.push(`TextColor="${props.textColor}"`);
    }
    
    // Font attributes
    if (props.fontSize !== undefined) {
      attributes.push(`FontSize="${props.fontSize}"`);
    }
    if (props.fontFamily) {
      attributes.push(`FontFamily="${props.fontFamily}"`);
    }
    if (props.fontAttributes && props.fontAttributes !== 'None') {
      attributes.push(`FontAttributes="${props.fontAttributes}"`);
    }
    
    // Margin and Padding
    if (props.margin) {
      const margin = this.thicknessToString(props.margin);
      if (margin) {
        attributes.push(`Margin="${margin}"`);
      }
    }
    if (props.padding) {
      const padding = this.thicknessToString(props.padding);
      if (padding) {
        attributes.push(`Padding="${padding}"`);
      }
    }
    
    // Visibility and enabled state
    if (props.isVisible === false) {
      push('IsVisible', 'False');
    }
    if (props.isEnabled === false) {
      push('IsEnabled', 'False');
    }
    
    // Layout specific attributes
    if (element.type === ElementType.StackLayout) {
      if (props.orientation === 'Horizontal') {
        // Use HorizontalStackLayout instead
      }
      if (props.spacing !== undefined) {
        attributes.push(`Spacing="${props.spacing}"`);
      }
    }
    
    if (element.type === ElementType.VerticalStackLayout) {
      if (props.spacing !== undefined) {
        attributes.push(`Spacing="${props.spacing}"`);
      }
    }
    
    for (const [name, path] of Object.entries(bindings)) {
      if (path) {
        attributes.push(`${name}="{Binding ${this.escapeXml(path)}}"`);
      }
    }

    return attributes.length > 0 ? ' ' + attributes.join(' ') : '';
  }

  private generateGridDefinitions(element: MauiElement, indentLevel: number): string {
    const indent = '    '.repeat(indentLevel + 1);
    const childIndent = '    '.repeat(indentLevel + 2);

    const definition: GridDefinition = element.properties.gridDefinition || {
      rows: [
        { height: { value: 1, type: GridLengthType.Star } },
        { height: { value: 1, type: GridLengthType.Star } }
      ],
      columns: [
        { width: { value: 1, type: GridLengthType.Star } },
        { width: { value: 1, type: GridLengthType.Star } }
      ]
    };

    let xaml = `\n${indent}<Grid.RowDefinitions>`;
    for (const row of definition.rows) {
      xaml += `\n${childIndent}<RowDefinition Height="${this.gridLengthToString(row.height)}" />`;
    }
    xaml += `\n${indent}</Grid.RowDefinitions>`;

    xaml += `\n${indent}<Grid.ColumnDefinitions>`;
    for (const column of definition.columns) {
      xaml += `\n${childIndent}<ColumnDefinition Width="${this.gridLengthToString(column.width)}" />`;
    }
    xaml += `\n${indent}</Grid.ColumnDefinitions>`;

    return xaml;
  }

  private gridLengthToString(length: GridLength): string {
    if (!length) {
      return '*';
    }
    switch (length.type) {
      case GridLengthType.Auto:
        return 'Auto';
      case GridLengthType.Absolute:
        return `${length.value}`;
      case GridLengthType.Star:
      default:
        return length.value === 1 ? '*' : `${length.value}*`;
    }
  }

  private thicknessToString(thickness: Thickness): string {
    if (thickness.left === thickness.top && thickness.top === thickness.right && thickness.right === thickness.bottom) {
      return thickness.left.toString();
    }
    return `${thickness.left},${thickness.top},${thickness.right},${thickness.bottom}`;
  }

  private escapeXml(text: string): string {
    return text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;');
  }
}
