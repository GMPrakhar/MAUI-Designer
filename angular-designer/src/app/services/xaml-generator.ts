import { Injectable } from '@angular/core';
import { MauiElement, ElementType, ElementProperties, Thickness } from '../models/maui-element';

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
    const childIndent = '    '.repeat(indentLevel + 2);
    
    const elementName = this.getXamlElementName(element.type);
    const attributes = this.generateAttributes(element);
    const hasChildren = element.children && element.children.length > 0;
    
    if (!hasChildren) {
      return `${indent}<${elementName}${attributes} />`;
    }
    
    let xaml = `${indent}<${elementName}${attributes}>`;
    
    // Add special content for certain layouts
    if (element.type === ElementType.Grid) {
      xaml += this.generateGridDefinitions(element, indentLevel + 1);
    }
    
    // Add children
    for (const child of element.children) {
      xaml += '\n' + this.generateElementXaml(child, indentLevel + 1);
    }
    
    xaml += `\n${indent}</${elementName}>`;
    
    return xaml;
  }

  private getXamlElementName(type: ElementType): string {
    switch (type) {
      case ElementType.StackLayout:
        return 'VerticalStackLayout'; // or HorizontalStackLayout based on orientation
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
      default:
        return type.toString();
    }
  }

  private generateAttributes(element: MauiElement): string {
    const props = element.properties;
    const attributes: string[] = [];
    
    // Add name attribute
    if (element.name) {
      attributes.push(`x:Name="${element.name}"`);
    }
    
    // Layout attributes
    if (element.type === ElementType.AbsoluteLayout) {
      // For children of AbsoluteLayout
      if (element.parent?.type === ElementType.AbsoluteLayout) {
        if (props.x !== undefined && props.y !== undefined && props.width !== undefined && props.height !== undefined) {
          attributes.push(`AbsoluteLayout.LayoutBounds="${props.x},${props.y},${props.width},${props.height}"`);
          attributes.push(`AbsoluteLayout.LayoutFlags="None"`);
        }
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
    if (props.text !== undefined) {
      attributes.push(`Text="${this.escapeXml(props.text)}"`);
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
      attributes.push(`IsVisible="False"`);
    }
    if (props.isEnabled === false) {
      attributes.push(`IsEnabled="False"`);
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
    
    return attributes.length > 0 ? ' ' + attributes.join(' ') : '';
  }

  private generateGridDefinitions(element: MauiElement, indentLevel: number): string {
    const indent = '    '.repeat(indentLevel + 1);
    const childIndent = '    '.repeat(indentLevel + 2);
    
    // For now, generate simple grid definitions
    // This could be enhanced to read from element properties
    let xaml = `\n${indent}<Grid.RowDefinitions>`;
    xaml += `\n${childIndent}<RowDefinition Height="*" />`;
    xaml += `\n${childIndent}<RowDefinition Height="*" />`;
    xaml += `\n${indent}</Grid.RowDefinitions>`;
    
    xaml += `\n${indent}<Grid.ColumnDefinitions>`;
    xaml += `\n${childIndent}<ColumnDefinition Width="*" />`;
    xaml += `\n${childIndent}<ColumnDefinition Width="*" />`;
    xaml += `\n${indent}</Grid.ColumnDefinitions>`;
    
    return xaml;
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
