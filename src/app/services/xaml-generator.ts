import { Injectable } from '@angular/core';
import { MauiElement, ElementType, ElementProperties, Thickness, GridDefinition, GridLength, GridLengthType, Orientation, XamlDocumentMetadata } from '../models/maui-element';

@Injectable({
  providedIn: 'root'
})
export class XamlGeneratorService {

  constructor() { }

  generateXaml(rootElement: MauiElement): string {
    const xamlContent = this.generateElementXaml(rootElement, 0);
    const document = rootElement.properties.document;
    const rootTag = document?.rootTag || 'ContentPage';
    const defaultNamespace = document?.defaultNamespace || 'http://schemas.microsoft.com/dotnet/2021/maui';
    const namespaces = this.collectNamespaceDeclarations(rootElement, document);
    const rootAttributes = document
      ? Object.entries(document.attributes)
          .map(([name, value]) => `\n             ${name}="${this.escapeXml(value)}"`)
          .join('')
      : '\n             x:Class="YourApp.MainPage"';
    const before = this.generateRawDocumentContent(document?.rawBeforeContent || []);
    const after = this.generateRawDocumentContent(document?.rawAfterContent || []);
    const content = document?.contentPropertyTag
      ? `    <${document.contentPropertyTag}>\n${this.indent(xamlContent)}\n    </${document.contentPropertyTag}>`
      : xamlContent;

    return `<?xml version="1.0" encoding="utf-8" ?>
<${rootTag} xmlns="${defaultNamespace}"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"${namespaces}${rootAttributes}>
${before}${content}${after}
</${rootTag}>`;
  }

  /** Only namespaces still referenced by retained markup are declared. */
  private collectNamespaceDeclarations(rootElement: MauiElement, document?: XamlDocumentMetadata): string {
    const elementNamespaces = new Map<string, string>();
    const references = document
      ? [
        document.rootTag,
        document.contentPropertyTag || '',
        ...Object.keys(document.attributes),
        ...Object.values(document.attributes),
        ...document.rawBeforeContent,
        ...document.rawAfterContent
      ]
      : [];

    const walk = (element: MauiElement) => {
      const {
        customPrefix,
        customNamespace,
        rawAttributes,
        customValues,
        bindings,
        rawContentXml
      } = element.properties;
      if (customPrefix && customNamespace) {
        elementNamespaces.set(customPrefix, customNamespace);
      }
      references.push(
        customPrefix ? `${customPrefix}:` : '',
        ...Object.keys(rawAttributes || {}),
        ...Object.values(rawAttributes || {}),
        ...Object.keys(customValues || {}),
        ...Object.values(customValues || {}),
        ...Object.keys(bindings || {}),
        ...Object.values(bindings || {}),
        ...(rawContentXml || [])
      );
      element.children.forEach(walk);
    };
    walk(rootElement);

    const retainedMarkup = references.join(' ');
    const used = new Map<string, string>(
      Object.entries(document?.namespaces || {}).filter(([prefix]) =>
        prefix !== 'x' && retainedMarkup.includes(`${prefix}:`)
      )
    );
    elementNamespaces.forEach((uri, prefix) => used.set(prefix, uri));

    return [...used.entries()]
      .map(([prefix, uri]) => `\n             xmlns:${prefix}="${uri}"`)
      .join('');
  }

  private generateRawDocumentContent(elements: string[]): string {
    return elements.length ? elements.map(element => `    ${element}\n`).join('') : '';
  }

  private indent(xaml: string): string {
    return xaml.split('\n').map(line => `    ${line}`).join('\n');
  }

  private generateElementXaml(element: MauiElement, indentLevel: number): string {
    const indent = '    '.repeat(indentLevel + 1);

    const elementName = this.getXamlElementName(element);
    const attributes = this.generateAttributes(element);
    const hasChildren = element.children && element.children.length > 0;
    const isGrid = element.type === ElementType.Grid;
    const rawContent = element.properties.rawContentXml || [];

    if (!hasChildren && !isGrid && rawContent.length === 0) {
      return `${indent}<${elementName}${attributes} />`;
    }

    let xaml = `${indent}<${elementName}${attributes}>`;

    // Add special content for certain layouts
    if (isGrid) {
      xaml += this.generateGridDefinitions(element, indentLevel + 1);
    }

    // Unmodelled property elements are re-emitted verbatim
    for (const raw of rawContent) {
      xaml += `\n${'    '.repeat(indentLevel + 2)}${raw}`;
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
    if (element.type === ElementType.Custom) {
      const tag = element.properties.customTag || 'ContentView';
      return element.properties.customPrefix ? `${element.properties.customPrefix}:${tag}` : tag;
    }

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
    const appTheme = props.appTheme || {};

    /** A bound property wins over the literal designer value. */
    const push = (name: string, value: string | number | undefined) => {
      if (bindings[name]) {
        return;
      }
      if (value !== undefined && value !== null && value !== '') {
        attributes.push(`${name}="${value}"`);
      }
    };

    /**
     * Colours may carry per-theme values. Routing them through here rather than
     * pushing directly also makes them honour bindings: attributes are
     * de-duplicated first-writer-wins and the binding loop runs last, so a
     * literal written directly would silently shadow the binding.
     */
    const pushColor = (name: string, value: string | undefined) => {
      const theme = appTheme[name];
      if (theme && (theme.light || theme.dark)) {
        const parts = [
          theme.light ? `Light=${theme.light}` : '',
          theme.dark ? `Dark=${theme.dark}` : ''
        ].filter(Boolean);
        push(name, `{AppThemeBinding ${parts.join(', ')}}`);
        return;
      }
      push(name, value === undefined ? undefined : this.escapeXml(value));
    };
    
    // Add name attribute
    if (element.name) {
      attributes.push(`x:Name="${element.name}"`);
    }

    // Manifest driven and preserved attributes win over the generic mapping
    if (element.type === ElementType.Custom) {
      for (const [name, value] of Object.entries(props.customValues || {})) {
        push(name, this.escapeXml(String(value)));
      }
    }

    // Attributes the designer does not model are re-emitted verbatim for every
    // element type, so importing XAML never silently deletes markup.
    for (const [name, value] of Object.entries(props.rawAttributes || {})) {
      push(name, this.escapeXml(value));
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
        pushColor('Fill', props.fillColor);
      }
      if (props.strokeColor) {
        pushColor('Stroke', props.strokeColor);
      }
      if (props.strokeThickness !== undefined) {
        attributes.push(`StrokeThickness="${props.strokeThickness}"`);
      }
    }
    
    // Colors
    if (props.backgroundColor || appTheme['BackgroundColor']) {
      pushColor('BackgroundColor', props.backgroundColor);
    }
    if (props.textColor || appTheme['TextColor']) {
      pushColor('TextColor', props.textColor);
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
    
    // Layout alignment within the parent container
    push('HorizontalOptions', props.horizontalOptions);
    push('VerticalOptions', props.verticalOptions);

    // Visibility and enabled state
    if (props.isVisible === false) {
      push('IsVisible', 'False');
    }
    if (props.isEnabled === false) {
      push('IsEnabled', 'False');
    }

    // Accessibility. These are attached properties, so they read as
    // SemanticProperties.X rather than plain attributes.
    if (props.semanticDescription) {
      push('SemanticProperties.Description', this.escapeXml(props.semanticDescription));
    }
    if (props.semanticHint) {
      push('SemanticProperties.Hint', this.escapeXml(props.semanticHint));
    }
    if (props.semanticHeadingLevel) {
      push('SemanticProperties.HeadingLevel', this.escapeXml(props.semanticHeadingLevel));
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

    // An attribute may only appear once: the first writer wins
    const seen = new Set<string>();
    const unique = attributes.filter(attribute => {
      const name = attribute.slice(0, attribute.indexOf('='));
      if (seen.has(name)) {
        return false;
      }
      seen.add(name);
      return true;
    });

    return unique.length > 0 ? ' ' + unique.join(' ') : '';
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
