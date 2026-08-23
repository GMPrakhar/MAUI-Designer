import { Injectable } from '@angular/core';
import { MauiElement, ElementType, ElementProperties, Thickness, GridDefinition, GridRowDefinition, GridColumnDefinition, GridLength, GridLengthType, Orientation, DEFAULT_ICON_PATH_DATA, AppThemeColor } from '../models/maui-element';
import { CustomControlRegistryService } from './custom-control-registry';

/** Attributes the designer models itself, so they never end up in rawAttributes. */
const RESERVED_ATTRIBUTES = new Set([
  'x:name',
  'x:class',
  'absolutelayout.layoutbounds',
  'absolutelayout.layoutflags',
  'grid.row',
  'grid.column',
  'grid.rowspan',
  'grid.columnspan',
  'widthrequest',
  'heightrequest',
  'backgroundcolor',
  'margin',
  'padding',
  'isvisible',
  'isenabled'
]);

@Injectable({
  providedIn: 'root'
})
export class XamlParserService {

  /** xmlns prefixes declared on the document being parsed. */
  private namespaces: Record<string, string> = {};

  constructor(private registry: CustomControlRegistryService) { }

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

      this.namespaces = this.collectNamespaces(contentPage);

      if (contentPage.tagName.toLowerCase() === 'svg') {
        return this.parseSvgIcon(contentPage);
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
    // Check if the contentPage itself is a layout element
    const contentPageType = this.getElementTypeFromTag(contentPage.tagName);
    if (this.isLayoutType(contentPageType)) {
      return contentPage;
    }
    
    // Otherwise look for layout elements in children
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
           type === ElementType.Border ||
           type === ElementType.CollectionView ||
           type === ElementType.ScrollView;
  }

  private parseElement(xmlElement: Element, parent: MauiElement | null): MauiElement {
    const elementType = this.getElementTypeFromTag(xmlElement.tagName) ?? ElementType.Custom;
    const properties = this.parseProperties(xmlElement, elementType, parent);
    const fallbackName = elementType === ElementType.Custom
      ? `${properties.customTag}${this.idCounter}`
      : `${elementType}${this.idCounter}`;

    const element: MauiElement = {
      id: this.generateId(),
      type: elementType,
      name: xmlElement.getAttribute('x:Name') || fallbackName,
      properties,
      children: [],
      parent: parent || undefined
    };

    // Property elements of a custom control cannot be modelled, so keep the XML
    if (elementType === ElementType.Custom) {
      const rawContent = this.collectRawPropertyElements(xmlElement);
      if (rawContent.length) {
        element.properties.rawContentXml = rawContent;
      }
    }

    // Parse child elements: unknown tags become custom elements, never dropped
    for (const childXmlElement of this.getChildElements(xmlElement)) {
      const childElement = this.parseElement(childXmlElement, element);
      element.children.push(childElement);
    }

    if (elementType === ElementType.Grid) {
      element.properties.gridDefinition = this.parseGridDefinition(xmlElement);
    }

    return element;
  }

  /**
   * Returns the real child views, skipping property elements such as
   * Grid.RowDefinitions and unwrapping CollectionView.ItemTemplate/DataTemplate.
   */
  private getChildElements(xmlElement: Element): Element[] {
    const children: Element[] = [];

    for (let i = 0; i < xmlElement.children.length; i++) {
      const child = xmlElement.children[i];
      const tag = child.tagName;

      if (tag.includes('.RowDefinitions') || tag.includes('.ColumnDefinitions')) {
        continue;
      }

      if (tag.includes('.ItemTemplate') || tag.toLowerCase() === 'datatemplate') {
        children.push(...this.getChildElements(child));
        continue;
      }

      // Any other property element (Border.StrokeShape, Grid.Resources, ...)
      if (tag.includes('.')) {
        continue;
      }

      children.push(child);
    }

    return children;
  }

  private parseGridDefinition(gridXmlElement: Element): GridDefinition {
    const rows: GridRowDefinition[] = [];
    const columns: GridColumnDefinition[] = [];

    for (let i = 0; i < gridXmlElement.children.length; i++) {
      const child = gridXmlElement.children[i];
      if (child.tagName.includes('.RowDefinitions')) {
        for (let j = 0; j < child.children.length; j++) {
          rows.push({ height: this.parseGridLength(child.children[j].getAttribute('Height')) });
        }
      } else if (child.tagName.includes('.ColumnDefinitions')) {
        for (let j = 0; j < child.children.length; j++) {
          columns.push({ width: this.parseGridLength(child.children[j].getAttribute('Width')) });
        }
      }
    }

    // MAUI also accepts a comma separated shorthand - RowDefinitions="Auto,*".
    // It is the form most real XAML uses, and ignoring it silently produced a
    // default 2x2 grid, so an imported layout came back with the wrong shape.
    if (!rows.length) {
      rows.push(...this.parseGridLengthList(gridXmlElement.getAttribute('RowDefinitions'))
        .map(height => ({ height })));
    }

    if (!columns.length) {
      columns.push(...this.parseGridLengthList(gridXmlElement.getAttribute('ColumnDefinitions'))
        .map(width => ({ width })));
    }

    const defaultLength = (): GridLength => ({ value: 1, type: GridLengthType.Star });

    return {
      rows: rows.length ? rows : [{ height: defaultLength() }, { height: defaultLength() }],
      columns: columns.length ? columns : [{ width: defaultLength() }, { width: defaultLength() }]
    };
  }

  private parseGridLengthList(value: string | null): GridLength[] {
    if (!value) {
      return [];
    }

    return value
      .split(',')
      .map(part => part.trim())
      .filter(part => part.length > 0)
      .map(part => this.parseGridLength(part));
  }

  private parseGridLength(value: string | null): GridLength {
    if (!value) {
      return { value: 1, type: GridLengthType.Star };
    }

    const trimmed = value.trim();
    if (trimmed.toLowerCase() === 'auto') {
      return { value: 1, type: GridLengthType.Auto };
    }
    if (trimmed === '*') {
      return { value: 1, type: GridLengthType.Star };
    }
    if (trimmed.endsWith('*')) {
      const starValue = parseFloat(trimmed.slice(0, -1));
      return { value: Number.isNaN(starValue) ? 1 : starValue, type: GridLengthType.Star };
    }

    const absolute = parseFloat(trimmed);
    return Number.isNaN(absolute)
      ? { value: 1, type: GridLengthType.Star }
      : { value: absolute, type: GridLengthType.Absolute };
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
      case 'searchbar':
        return ElementType.SearchBar;
      case 'checkbox':
        return ElementType.CheckBox;
      case 'switch':
        return ElementType.Switch;
      case 'slider':
        return ElementType.Slider;
      case 'stepper':
        return ElementType.Stepper;
      case 'progressbar':
        return ElementType.ProgressBar;
      case 'activityindicator':
        return ElementType.ActivityIndicator;
      case 'datepicker':
        return ElementType.DatePicker;
      case 'border':
        return ElementType.Border;
      case 'collectionview':
        return ElementType.CollectionView;
      case 'image':
        return ElementType.Image;
      case 'path':
        return ElementType.Path;
      default:
        return null;
    }
  }

  private parseSvgIcon(svgElement: Element): MauiElement {
    const size = this.getSvgSize(svgElement);
    const rootElement: MauiElement = {
      id: this.generateId(),
      type: ElementType.AbsoluteLayout,
      name: 'SvgIconLayout',
      properties: {
        x: 0,
        y: 0,
        width: size.width,
        height: size.height,
        backgroundColor: 'Transparent',
        isVisible: true,
        isEnabled: true
      },
      children: []
    };

    const pathElements = Array.from(svgElement.querySelectorAll('path'))
      .filter(pathElement => !!pathElement.getAttribute('d'));

    if (pathElements.length === 0) {
      throw new Error('SVG does not contain any <path d="..."> elements to convert.');
    }

    pathElements.forEach((pathElement, index) => {
      const currentColor = this.getSvgCurrentColor(pathElement);
      const path: MauiElement = {
        id: this.generateId(),
        type: ElementType.Path,
        name: `IconPath${index + 1}`,
        properties: {
          x: 0,
          y: 0,
          width: size.width,
          height: size.height,
          pathData: pathElement.getAttribute('d') || '',
          fillColor: this.normalizeSvgPaint(this.getInheritedAttribute(pathElement, 'fill'), '#000000', currentColor),
          strokeColor: this.normalizeSvgPaint(this.getInheritedAttribute(pathElement, 'stroke'), 'Transparent', currentColor),
          strokeThickness: this.parseNumber(this.getInheritedAttribute(pathElement, 'stroke-width'), 0),
          isVisible: true,
          isEnabled: true
        },
        children: [],
        parent: rootElement
      };
      rootElement.children.push(path);
    });

    return rootElement;
  }

  private getSvgSize(svgElement: Element): { width: number; height: number } {
    const viewBox = svgElement.getAttribute('viewBox');
    if (viewBox) {
      const values = viewBox.split(/[\s,]+/).map(value => parseFloat(value)).filter(value => !Number.isNaN(value));
      if (values.length >= 4 && values[2] > 0 && values[3] > 0) {
        return { width: values[2], height: values[3] };
      }
    }

    const width = this.parseNumber(svgElement.getAttribute('width'), 24);
    const height = this.parseNumber(svgElement.getAttribute('height'), 24);
    return {
      width,
      height
    };
  }

  private getInheritedAttribute(element: Element, attributeName: string): string | null {
    let current: Element | null = element;
    while (current) {
      const value = current.getAttribute(attributeName);
      if (value !== null) {
        return value;
      }
      current = current.parentElement;
    }
    return null;
  }

  private normalizeSvgPaint(value: string | null, fallback: string, currentColor: string): string {
    if (!value) {
      return fallback;
    }
    if (value === 'currentColor') {
      return currentColor;
    }
    if (value.toLowerCase() === 'none') {
      return 'Transparent';
    }
    return value;
  }

  private getSvgCurrentColor(element: Element): string {
    const color = this.getInheritedAttribute(element, 'color');
    return color && color !== 'currentColor' && color.toLowerCase() !== 'none' ? color : '#000000';
  }

  private parseNumber(value: string | null, fallback: number): number {
    if (!value) {
      return fallback;
    }
    const parsed = parseFloat(value);
    return Number.isNaN(parsed) ? fallback : parsed;
  }

  /** Serialises property elements such as `<toolkit:Expander.Header>` verbatim. */
  private collectRawPropertyElements(xmlElement: Element): string[] {
    const serializer = new XMLSerializer();
    const raw: string[] = [];

    for (let i = 0; i < xmlElement.children.length; i++) {
      const child = xmlElement.children[i];
      if (child.tagName.includes('.')) {
        // The serializer repeats every namespace: they are declared on the page
        raw.push(serializer.serializeToString(child).replace(/\s+xmlns(:[\w.-]+)?="[^"]*"/g, ''));
      }
    }

    return raw;
  }

  /** Reads `xmlns:prefix="uri"` declarations from the document root. */
  private collectNamespaces(root: Element): Record<string, string> {
    const namespaces: Record<string, string> = {};
    for (let i = 0; i < root.attributes.length; i++) {
      const attribute = root.attributes[i];
      if (attribute.name.startsWith('xmlns:')) {
        namespaces[attribute.name.slice('xmlns:'.length)] = attribute.value;
      }
    }
    return namespaces;
  }

  /**
   * Keeps an unrecognised control usable: its tag, prefix, namespace and every
   * attribute are preserved, and the control is registered so it can be edited.
   */
  private parseCustomControl(xmlElement: Element, properties: ElementProperties): void {
    const [rawPrefix, rawTag] = xmlElement.tagName.includes(':')
      ? xmlElement.tagName.split(':')
      : ['', xmlElement.tagName];

    const prefix = rawPrefix;
    const tag = rawTag;
    const uri = this.namespaces[prefix] || '';

    const attributes: Record<string, string> = {};
    for (let i = 0; i < xmlElement.attributes.length; i++) {
      const attribute = xmlElement.attributes[i];
      const name = attribute.name;
      if (
        name.startsWith('xmlns') ||
        RESERVED_ATTRIBUTES.has(name.toLowerCase()) ||
        this.isBindingExpression(attribute.value)
      ) {
        continue;
      }
      attributes[name] = attribute.value;
    }

    properties.customTag = tag;
    properties.customPrefix = prefix || undefined;
    properties.customNamespace = uri || undefined;

    const lookup = this.registry.learn(prefix, uri, tag, attributes);
    const declared = new Set((lookup.definition.properties || []).map(property => property.name));

    const customValues: Record<string, string> = {};
    const rawAttributes: Record<string, string> = {};
    for (const [name, value] of Object.entries(attributes)) {
      if (declared.has(name)) {
        customValues[name] = value;
      } else {
        rawAttributes[name] = value;
      }
    }

    properties.customValues = customValues;
    properties.rawAttributes = rawAttributes;
  }

  private parseProperties(xmlElement: Element, elementType: ElementType, parent: MauiElement | null): ElementProperties {
    const properties: ElementProperties = {
      isVisible: true,
      isEnabled: true
    };

    if (elementType === ElementType.Custom) {
      this.parseCustomControl(xmlElement, properties);
    }

    // Bindings are captured separately so they survive a round trip
    const bindings = this.parseBindings(xmlElement);
    if (Object.keys(bindings).length > 0) {
      properties.bindings = bindings;
    }

    const literal = (name: string): string | null => {
      const value = xmlElement.getAttribute(name);
      return value === null || this.isBindingExpression(value) ? null : value;
    };

    /**
     * Reads a colour attribute, unpacking `{AppThemeBinding Light=..., Dark=...}`
     * into `appTheme` and returning the value the canvas should paint with. The
     * light value is the fallback so a design opened in light mode looks right.
     */
    const color = (name: string): string | null => {
      const value = xmlElement.getAttribute(name);
      if (value === null || this.isBindingExpression(value)) {
        return null;
      }

      const theme = XamlParserService.parseAppThemeBinding(value);
      if (!theme) {
        return value;
      }

      properties.appTheme = { ...(properties.appTheme || {}), [name]: theme };
      return theme.light ?? theme.dark ?? null;
    };

    // Parse basic properties
    const text = literal('Text');
    if (text) properties.text = text;

    const placeholder = literal('Placeholder');
    if (placeholder) properties.placeholder = placeholder;

    const isChecked = literal('IsChecked');
    if (isChecked) properties.isChecked = isChecked.toLowerCase() === 'true';

    const isToggled = literal('IsToggled');
    if (isToggled) properties.isToggled = isToggled.toLowerCase() === 'true';

    const minimum = literal('Minimum');
    if (minimum) properties.minimum = parseFloat(minimum);

    const maximum = literal('Maximum');
    if (maximum) properties.maximum = parseFloat(maximum);

    const increment = literal('Increment');
    if (increment) properties.increment = parseFloat(increment);

    const value = literal('Value');
    if (value) properties.value = parseFloat(value);

    const progress = literal('Progress');
    if (progress) properties.progress = parseFloat(progress);

    const isRunning = literal('IsRunning');
    if (isRunning) properties.isRunning = isRunning.toLowerCase() === 'true';

    const date = literal('Date');
    if (date) properties.date = date;

    const cornerRadius = literal('CornerRadius');
    if (cornerRadius) properties.cornerRadius = parseFloat(cornerRadius);

    const strokeShape = literal('StrokeShape');
    if (strokeShape) {
      const radius = parseFloat(strokeShape.replace(/[^0-9.]/g, ''));
      if (!Number.isNaN(radius)) properties.cornerRadius = radius;
    }

    if (elementType === ElementType.Border) {
      const borderColor = color('Stroke');
      if (borderColor) properties.borderColor = borderColor;
      const borderWidth = literal('StrokeThickness');
      if (borderWidth) properties.borderWidth = parseFloat(borderWidth);
    }

    const pathData = xmlElement.getAttribute('Data');
    if (pathData) properties.pathData = pathData;

    const fill = color('Fill');
    if (fill) properties.fillColor = fill;

    const stroke = color('Stroke');
    if (stroke) properties.strokeColor = stroke;

    const strokeThickness = xmlElement.getAttribute('StrokeThickness');
    if (strokeThickness) properties.strokeThickness = parseFloat(strokeThickness);

    const widthRequest = xmlElement.getAttribute('WidthRequest');
    if (widthRequest) properties.width = parseFloat(widthRequest);

    const heightRequest = xmlElement.getAttribute('HeightRequest');
    if (heightRequest) properties.height = parseFloat(heightRequest);

    const backgroundColor = color('BackgroundColor');
    if (backgroundColor) properties.backgroundColor = backgroundColor;

    const textColor = color('TextColor');
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

      // An explicit Orientation attribute wins over the tag name
      const orientation = xmlElement.getAttribute('Orientation');
      if (orientation) {
        properties.orientation = orientation.toLowerCase() === 'horizontal'
          ? Orientation.Horizontal
          : Orientation.Vertical;
      } else if (xmlElement.tagName.toLowerCase() === 'horizontalstacklayout') {
        properties.orientation = Orientation.Horizontal;
      } else {
        properties.orientation = Orientation.Vertical;
      }
    }

    if (elementType === ElementType.VerticalStackLayout) {
      const spacing = xmlElement.getAttribute('Spacing');
      if (spacing) properties.spacing = parseFloat(spacing);
    }

    // Set default values if not specified
    this.setDefaultValues(properties, elementType);

    return properties;
  }

  private isBindingExpression(value: string): boolean {
    return /^\s*\{\s*Binding\b/i.test(value);
  }

  /** Collects every attribute written as `{Binding Path}` into a map. */
  private parseBindings(xmlElement: Element): Record<string, string> {
    const bindings: Record<string, string> = {};
    for (let i = 0; i < xmlElement.attributes.length; i++) {
      const attribute = xmlElement.attributes[i];
      if (!this.isBindingExpression(attribute.value)) {
        continue;
      }
      const match = /\{\s*Binding\s+([^},]*)/i.exec(attribute.value);
      const path = (match?.[1] || '').trim();
      bindings[attribute.name] = path.replace(/^Path\s*=\s*/i, '');
    }
    return bindings;
  }

  /**
   * Reads `{AppThemeBinding Light=#FFF, Dark=#333}`. Returns null for anything
   * that is not an AppThemeBinding so callers can fall back to a plain value.
   * MAUI also allows a bare `Default=`, which is treated as the light value.
   */
  static parseAppThemeBinding(value: string): AppThemeColor | null {
    if (!/^\s*\{\s*AppThemeBinding\b/i.test(value)) {
      return null;
    }

    const body = value.trim().replace(/^\{\s*AppThemeBinding\s*/i, '').replace(/\}\s*$/, '');
    const theme: AppThemeColor = {};

    for (const part of body.split(',')) {
      const match = /^\s*(Light|Dark|Default)\s*=\s*(.+?)\s*$/i.exec(part);
      if (!match) {
        continue;
      }
      const key = match[1].toLowerCase();
      if (key === 'dark') {
        theme.dark = match[2];
      } else if (!theme.light) {
        theme.light = match[2];
      }
    }

    return theme.light || theme.dark ? theme : null;
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
      case ElementType.SearchBar:
        if (properties.width === undefined) properties.width = 200;
        if (properties.height === undefined) properties.height = 40;
        if (properties.backgroundColor === undefined) properties.backgroundColor = '#ffffff';
        if (properties.textColor === undefined) properties.textColor = '#000000';
        break;
      case ElementType.CheckBox:
        if (properties.width === undefined) properties.width = 40;
        if (properties.height === undefined) properties.height = 40;
        if (properties.isChecked === undefined) properties.isChecked = false;
        break;
      case ElementType.Switch:
        if (properties.width === undefined) properties.width = 60;
        if (properties.height === undefined) properties.height = 32;
        if (properties.isToggled === undefined) properties.isToggled = false;
        break;
      case ElementType.Slider:
      case ElementType.Stepper:
        if (properties.width === undefined) properties.width = 200;
        if (properties.height === undefined) properties.height = 32;
        if (properties.minimum === undefined) properties.minimum = 0;
        if (properties.maximum === undefined) properties.maximum = 100;
        if (properties.value === undefined) properties.value = 0;
        break;
      case ElementType.ProgressBar:
        if (properties.width === undefined) properties.width = 200;
        if (properties.height === undefined) properties.height = 12;
        if (properties.progress === undefined) properties.progress = 0;
        break;
      case ElementType.ActivityIndicator:
        if (properties.width === undefined) properties.width = 40;
        if (properties.height === undefined) properties.height = 40;
        if (properties.isRunning === undefined) properties.isRunning = true;
        break;
      case ElementType.DatePicker:
        if (properties.width === undefined) properties.width = 180;
        if (properties.height === undefined) properties.height = 40;
        break;
      case ElementType.Border:
        if (properties.width === undefined) properties.width = 150;
        if (properties.height === undefined) properties.height = 100;
        if (properties.borderColor === undefined) properties.borderColor = '#cccccc';
        if (properties.borderWidth === undefined) properties.borderWidth = 1;
        break;
      case ElementType.CollectionView:
        if (properties.width === undefined) properties.width = 240;
        if (properties.height === undefined) properties.height = 200;
        if (properties.itemCount === undefined) properties.itemCount = 3;
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
      case ElementType.Path:
        if (properties.width === undefined) properties.width = 24;
        if (properties.height === undefined) properties.height = 24;
        if (properties.pathData === undefined) properties.pathData = DEFAULT_ICON_PATH_DATA;
        if (properties.fillColor === undefined) properties.fillColor = '#000000';
        if (properties.strokeColor === undefined) properties.strokeColor = 'Transparent';
        if (properties.strokeThickness === undefined) properties.strokeThickness = 0;
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