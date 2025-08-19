import { Injectable } from '@angular/core';
import { MauiElement, ElementType, ElementProperties, Thickness } from '../models/maui-element';

@Injectable({
  providedIn: 'root'
})
export class XamlParserService {

  constructor() { }

  parseXaml(xamlContent: string): MauiElement {
    try {
      // Create a temporary DOM to parse XML
      const parser = new DOMParser();
      const xmlDoc = parser.parseFromString(xamlContent, 'text/xml');
      
      // Check for parsing errors
      const parserError = xmlDoc.querySelector('parsererror');
      if (parserError) {
        throw new Error(`XML parsing error: ${parserError.textContent}`);
      }
      
      // Find the root layout element (skip ContentPage)
      const contentPage = xmlDoc.documentElement;
      if (!contentPage) {
        throw new Error('No root element found');
      }
      
      // Look for the first child that's a layout
      const rootLayoutElement = this.findRootLayoutElement(contentPage);
      if (!rootLayoutElement) {
        throw new Error('No valid layout element found. Please ensure your XAML contains a layout like AbsoluteLayout, Grid, or StackLayout.');
      }
      
      return this.parseElement(rootLayoutElement, null);
    } catch (error: any) {
      console.error('XAML parsing error:', error);
      throw error;
    }
  }

  private findRootLayoutElement(contentPage: Element): Element | null {
    for (let i = 0; i < contentPage.children.length; i++) {
      const child = contentPage.children[i];
      const elementType = this.getElementTypeFromTag(child.tagName);
      if (this.isLayoutType(elementType)) {
        return child;
      }
    }
    return null;
  }

  private isLayoutType(type: ElementType | null): boolean {
    return type === ElementType.AbsoluteLayout ||
           type === ElementType.Grid ||
           type === ElementType.StackLayout ||
           type === ElementType.VerticalStackLayout ||
           type === ElementType.Frame ||
           type === ElementType.ScrollView;
  }

  private parseElement(xmlElement: Element, parent: MauiElement | null): MauiElement {
    const elementType = this.getElementTypeFromTag(xmlElement.tagName);
    if (!elementType) {
      throw new Error(`Unsupported element type: ${xmlElement.tagName}`);
    }

    const element: MauiElement = {
      id: this.generateId(),
      type: elementType,
      name: xmlElement.getAttribute('x:Name') || `${elementType}${this.idCounter}`,
      properties: this.parseProperties(xmlElement, elementType, parent),
      children: [],
      parent: parent || undefined
    };

    // Parse child elements
    for (let i = 0; i < xmlElement.children.length; i++) {
      const childXmlElement = xmlElement.children[i];
      
      // Skip grid definition elements
      if (childXmlElement.tagName.includes('.RowDefinitions') || 
          childXmlElement.tagName.includes('.ColumnDefinitions')) {
        continue;
      }
      
      const childElementType = this.getElementTypeFromTag(childXmlElement.tagName);
      if (childElementType) {
        const childElement = this.parseElement(childXmlElement, element);
        element.children.push(childElement);
      }
    }

    return element;
  }

  private idCounter = 0;

  private generateId(): string {
    return `element_${++this.idCounter}`;
  }

  private getElementTypeFromTag(tagName: string): ElementType | null {
    switch (tagName.toLowerCase()) {
      case 'absolutelayout':
        return ElementType.AbsoluteLayout;
      case 'grid':
        return ElementType.Grid;
      case 'verticalstacklayout':
        return ElementType.VerticalStackLayout;
      case 'horizontalstacklayout':
      case 'stacklayout':
        return ElementType.StackLayout;
      case 'frame':
        return ElementType.Frame;
      case 'scrollview':
        return ElementType.ScrollView;
      case 'label':
        return ElementType.Label;
      case 'button':
        return ElementType.Button;
      case 'entry':
        return ElementType.Entry;
      case 'editor':
        return ElementType.Editor;
      case 'image':
        return ElementType.Image;
      default:
        return null;
    }
  }

  private parseProperties(xmlElement: Element, elementType: ElementType, parent: MauiElement | null): ElementProperties {
    const properties: ElementProperties = {
      isVisible: true,
      isEnabled: true
    };

    // Parse basic properties
    const text = xmlElement.getAttribute('Text');
    if (text) properties.text = text;

    const widthRequest = xmlElement.getAttribute('WidthRequest');
    if (widthRequest) properties.width = parseFloat(widthRequest);

    const heightRequest = xmlElement.getAttribute('HeightRequest');
    if (heightRequest) properties.height = parseFloat(heightRequest);

    const backgroundColor = xmlElement.getAttribute('BackgroundColor');
    if (backgroundColor) properties.backgroundColor = backgroundColor;

    const textColor = xmlElement.getAttribute('TextColor');
    if (textColor) properties.textColor = textColor;

    const fontSize = xmlElement.getAttribute('FontSize');
    if (fontSize) properties.fontSize = parseFloat(fontSize);

    const fontFamily = xmlElement.getAttribute('FontFamily');
    if (fontFamily) properties.fontFamily = fontFamily;

    const fontAttributes = xmlElement.getAttribute('FontAttributes');
    if (fontAttributes) properties.fontAttributes = fontAttributes as any;

    const margin = xmlElement.getAttribute('Margin');
    if (margin) properties.margin = this.parseThickness(margin);

    const padding = xmlElement.getAttribute('Padding');
    if (padding) properties.padding = this.parseThickness(padding);

    const isVisible = xmlElement.getAttribute('IsVisible');
    if (isVisible) properties.isVisible = isVisible.toLowerCase() === 'true';

    const isEnabled = xmlElement.getAttribute('IsEnabled');
    if (isEnabled) properties.isEnabled = isEnabled.toLowerCase() === 'true';

    // Parse layout-specific properties
    if (parent?.type === ElementType.AbsoluteLayout) {
      const layoutBounds = xmlElement.getAttribute('AbsoluteLayout.LayoutBounds');
      if (layoutBounds) {
        const bounds = layoutBounds.split(',').map(v => parseFloat(v.trim()));
        if (bounds.length >= 4) {
          properties.x = bounds[0];
          properties.y = bounds[1];
          properties.width = bounds[2];
          properties.height = bounds[3];
        }
      }
    }

    if (parent?.type === ElementType.Grid) {
      const row = xmlElement.getAttribute('Grid.Row');
      if (row) properties.row = parseInt(row);

      const column = xmlElement.getAttribute('Grid.Column');
      if (column) properties.column = parseInt(column);

      const rowSpan = xmlElement.getAttribute('Grid.RowSpan');
      if (rowSpan) properties.rowSpan = parseInt(rowSpan);

      const columnSpan = xmlElement.getAttribute('Grid.ColumnSpan');
      if (columnSpan) properties.columnSpan = parseInt(columnSpan);
    }

    if (elementType === ElementType.StackLayout) {
      const spacing = xmlElement.getAttribute('Spacing');
      if (spacing) properties.spacing = parseFloat(spacing);

      // Determine orientation from tag name
      if (xmlElement.tagName.toLowerCase() === 'horizontalstacklayout') {
        properties.orientation = 'Horizontal' as any;
      } else {
        properties.orientation = 'Vertical' as any;
      }
    }

    // Set default values if not specified
    this.setDefaultValues(properties, elementType);

    return properties;
  }

  private parseThickness(value: string): Thickness {
    const values = value.split(',').map(v => parseFloat(v.trim()));
    if (values.length === 1) {
      return { left: values[0], top: values[0], right: values[0], bottom: values[0] };
    } else if (values.length === 4) {
      return { left: values[0], top: values[1], right: values[2], bottom: values[3] };
    }
    return { left: 0, top: 0, right: 0, bottom: 0 };
  }

  private setDefaultValues(properties: ElementProperties, elementType: ElementType) {
    // Set default position and size if not specified
    if (properties.x === undefined) properties.x = 0;
    if (properties.y === undefined) properties.y = 0;

    switch (elementType) {
      case ElementType.Label:
        if (properties.width === undefined) properties.width = 100;
        if (properties.height === undefined) properties.height = 30;
        if (properties.text === undefined) properties.text = 'Label';
        if (properties.textColor === undefined) properties.textColor = '#000000';
        if (properties.fontSize === undefined) properties.fontSize = 14;
        break;
      case ElementType.Button:
        if (properties.width === undefined) properties.width = 100;
        if (properties.height === undefined) properties.height = 30;
        if (properties.text === undefined) properties.text = 'Button';
        if (properties.backgroundColor === undefined) properties.backgroundColor = '#007acc';
        if (properties.textColor === undefined) properties.textColor = '#ffffff';
        if (properties.fontSize === undefined) properties.fontSize = 14;
        break;
      case ElementType.Entry:
        if (properties.width === undefined) properties.width = 100;
        if (properties.height === undefined) properties.height = 30;
        if (properties.backgroundColor === undefined) properties.backgroundColor = '#ffffff';
        if (properties.textColor === undefined) properties.textColor = '#000000';
        break;
      case ElementType.Editor:
        if (properties.width === undefined) properties.width = 100;
        if (properties.height === undefined) properties.height = 100;
        if (properties.backgroundColor === undefined) properties.backgroundColor = '#ffffff';
        if (properties.textColor === undefined) properties.textColor = '#000000';
        break;
      case ElementType.Image:
        if (properties.width === undefined) properties.width = 100;
        if (properties.height === undefined) properties.height = 100;
        break;
      case ElementType.AbsoluteLayout:
        if (properties.width === undefined) properties.width = 800;
        if (properties.height === undefined) properties.height = 600;
        if (properties.backgroundColor === undefined) properties.backgroundColor = '#ffffff';
        break;
      default:
        if (properties.width === undefined) properties.width = 200;
        if (properties.height === undefined) properties.height = 200;
        break;
    }
  }
}