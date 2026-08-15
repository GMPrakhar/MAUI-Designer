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
  CollectionView = 'CollectionView'
}

export const DEFAULT_ICON_PATH_DATA = 'M12 2L2 22h20L12 2Z';

export interface ElementProperties {
  // Layout properties
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  margin?: Thickness;
  padding?: Thickness;
  
  // Visual properties
  backgroundColor?: string;
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

  // Other properties
  isVisible?: boolean;
  isEnabled?: boolean;

  /**
   * Data bindings keyed by MAUI property name, e.g. `{ Text: 'UserName' }`
   * is generated as Text="{Binding UserName}".
   */
  bindings?: Record<string, string>;
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