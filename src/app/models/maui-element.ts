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
  Image = 'Image',
  StackLayout = 'StackLayout',
  VerticalStackLayout = 'VerticalStackLayout',
  Grid = 'Grid',
  AbsoluteLayout = 'AbsoluteLayout',
  Frame = 'Frame',
  ScrollView = 'ScrollView'
}

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
  
  // Grid-specific properties
  row?: number;
  column?: number;
  rowSpan?: number;
  columnSpan?: number;
  gridDefinition?: GridDefinition;
  
  // Layout-specific properties
  orientation?: Orientation;
  spacing?: number;
  
  // Other properties
  isVisible?: boolean;
  isEnabled?: boolean;
}

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