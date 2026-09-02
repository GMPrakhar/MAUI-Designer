export interface MauiElement {
  id: string;
  type: ElementType;
  name: string;
  properties: ElementProperties;
  children: MauiElement[];
  parent?: MauiElement;
  domElement?: HTMLElement;
}

export enum ElementType {
  Label = 'Label',
  Button = 'Button',
  Entry = 'Entry',
  Editor = 'Editor',
  SearchBar = 'SearchBar',
  CheckBox = 'CheckBox',
  Switch = 'Switch',
  Slider = 'Slider',
  Stepper = 'Stepper',
  ProgressBar = 'ProgressBar',
  ActivityIndicator = 'ActivityIndicator',
  DatePicker = 'DatePicker',
  Image = 'Image',
  Path = 'Path',
  StackLayout = 'StackLayout',
  VerticalStackLayout = 'VerticalStackLayout',
  Grid = 'Grid',
  AbsoluteLayout = 'AbsoluteLayout',
  Frame = 'Frame',
  Border = 'Border',
  ScrollView = 'ScrollView',
  CollectionView = 'CollectionView',
  /** Any control that comes from a NuGet package manifest or imported XAML. */
  Custom = 'Custom'
}

export const DEFAULT_ICON_PATH_DATA = 'M12 2L2 22h20L12 2Z';

/** MAUI `LayoutOptions` values, as accepted by HorizontalOptions/VerticalOptions. */
export const LAYOUT_OPTIONS = ['Start', 'Center', 'End', 'Fill'] as const;

export type LayoutOptions = typeof LAYOUT_OPTIONS[number];

export interface ElementProperties {
  // Layout properties
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  margin?: Thickness;
  padding?: Thickness;
  horizontalOptions?: LayoutOptions;
  verticalOptions?: LayoutOptions;
  
  // Visual properties
  backgroundColor?: string;
  /** CSS preview generated from a MAUI LinearGradientBrush property element. */
  backgroundGradient?: string;
  textColor?: string;
  fontSize?: number;
  fontFamily?: string;
  fontAttributes?: FontAttributes;
  
  // Content properties
  text?: string;
  placeholder?: string;
  pathData?: string;
  fillColor?: string;
  strokeColor?: string;
  strokeThickness?: number;
  
  // Grid-specific properties
  row?: number;
  column?: number;
  rowSpan?: number;
  columnSpan?: number;
  gridDefinition?: GridDefinition;
  
  // Layout-specific properties
  orientation?: Orientation;
  spacing?: number;
  
  // Control state properties
  isChecked?: boolean;
  isToggled?: boolean;
  value?: number;
  minimum?: number;
  maximum?: number;
  increment?: number;
  progress?: number;
  isRunning?: boolean;
  date?: string;

  // Border / frame properties
  cornerRadius?: number;
  borderColor?: string;
  borderWidth?: number;

  // CollectionView preview
  itemCount?: number;
  itemTemplateText?: string;

  // Custom (third party) control properties
  /** Local XAML tag of a custom control, e.g. `AvatarView`. */
  customTag?: string;
  /** XML prefix the control is emitted with, e.g. `toolkit`. */
  customPrefix?: string;
  /** Namespace URI, kept on the element so XAML survives without the manifest. */
  customNamespace?: string;
  /** Values for the properties declared in the control's manifest. */
  customValues?: Record<string, string>;
  /** Attributes the designer does not model, preserved verbatim. */
  rawAttributes?: Record<string, string>;
  /** Property elements of a custom control (e.g. Expander.Header), kept as XML. */
  rawContentXml?: string[];

  /** Original page wrapper and non-visual property elements retained on import. */
  document?: XamlDocumentMetadata;

  // Other properties
  isVisible?: boolean;
  isEnabled?: boolean;

  /** MAUI SemanticProperties.Description -- what a screen reader announces. */
  semanticDescription?: string;
  /** MAUI SemanticProperties.Hint -- extra context about what the control does. */
  semanticHint?: string;
  /** MAUI SemanticProperties.HeadingLevel, e.g. `Level1`. */
  semanticHeadingLevel?: string;

  /**
   * Data bindings keyed by MAUI property name, e.g. `{ Text: 'UserName' }`
   * is generated as Text="{Binding UserName}".
   */
  bindings?: Record<string, string>;

  /**
   * Per-theme colour overrides keyed by MAUI property name, e.g.
   * `{ BackgroundColor: { light: '#FFFFFF', dark: '#1E1E1E' } }` is generated as
   * BackgroundColor="{AppThemeBinding Light=#FFFFFF, Dark=#1E1E1E}".
   */
  appTheme?: Record<string, AppThemeColor>;
}

export interface XamlDocumentMetadata {
  rootTag: string;
  defaultNamespace?: string;
  namespaces: Record<string, string>;
  attributes: Record<string, string>;
  contentPropertyTag?: string;
  contentWidthExplicit: boolean;
  contentHeightExplicit: boolean;
  rawBeforeContent: string[];
  rawAfterContent: string[];
}

/** Light and dark values for a single colour property. */
export interface AppThemeColor {
  light?: string;
  dark?: string;
}

/** Properties that can be bound to a view model, per element type. */
export const BINDABLE_PROPERTIES: Record<string, string[]> = {
  [ElementType.Label]: ['Text', 'TextColor', 'IsVisible'],
  [ElementType.Button]: ['Text', 'Command', 'IsEnabled'],
  [ElementType.Entry]: ['Text', 'Placeholder', 'IsEnabled'],
  [ElementType.Editor]: ['Text', 'Placeholder'],
  [ElementType.SearchBar]: ['Text', 'Placeholder', 'SearchCommand'],
  [ElementType.CheckBox]: ['IsChecked'],
  [ElementType.Switch]: ['IsToggled'],
  [ElementType.Slider]: ['Value'],
  [ElementType.Stepper]: ['Value'],
  [ElementType.ProgressBar]: ['Progress'],
  [ElementType.ActivityIndicator]: ['IsRunning'],
  [ElementType.DatePicker]: ['Date'],
  [ElementType.Image]: ['Source'],
  [ElementType.CollectionView]: ['ItemsSource', 'SelectedItem']
};

/** Every element supports these bindings in addition to the type specific ones. */
export const COMMON_BINDABLE_PROPERTIES = ['IsVisible', 'IsEnabled'];

export interface Thickness {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

export enum FontAttributes {
  None = 'None',
  Bold = 'Bold',
  Italic = 'Italic'
}

export enum Orientation {
  Vertical = 'Vertical',
  Horizontal = 'Horizontal'
}

export interface GridDefinition {
  rows: GridRowDefinition[];
  columns: GridColumnDefinition[];
}

export interface GridRowDefinition {
  height: GridLength;
}

export interface GridColumnDefinition {
  width: GridLength;
}

export interface GridLength {
  value: number;
  type: GridLengthType;
}

export enum GridLengthType {
  Auto = 'Auto',
  Star = 'Star',
  Absolute = 'Absolute'
}