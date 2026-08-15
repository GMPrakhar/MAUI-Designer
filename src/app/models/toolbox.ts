export interface ToolboxItem {
  type: string;
  displayName: string;
  icon: string;
  description: string;
  category: ToolboxCategory;
}

export enum ToolboxCategory {
  Controls = 'Controls',
  Inputs = 'Inputs',
  Layouts = 'Layouts',
  Views = 'Views'
}

export const MAUI_CONTROLS: ToolboxItem[] = [
  // Controls
  {
    type: 'Label',
    displayName: 'Label',
    icon: 'text_fields',
    description: 'Displays text',
    category: ToolboxCategory.Controls
  },
  {
    type: 'Button',
    displayName: 'Button',
    icon: 'smart_button',
    description: 'Interactive button',
    category: ToolboxCategory.Controls
  },
  {
    type: 'Entry',
    displayName: 'Entry',
    icon: 'input',
    description: 'Single line text input',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'Editor',
    displayName: 'Editor',
    icon: 'edit_note',
    description: 'Multi-line text input',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'SearchBar',
    displayName: 'SearchBar',
    icon: 'search',
    description: 'Search input with a search button',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'CheckBox',
    displayName: 'CheckBox',
    icon: 'check_box',
    description: 'Boolean check box',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'Switch',
    displayName: 'Switch',
    icon: 'toggle_on',
    description: 'On/off toggle',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'Slider',
    displayName: 'Slider',
    icon: 'tune',
    description: 'Selects a value from a range',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'Stepper',
    displayName: 'Stepper',
    icon: 'exposure',
    description: 'Increments and decrements a value',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'DatePicker',
    displayName: 'DatePicker',
    icon: 'event',
    description: 'Selects a date',
    category: ToolboxCategory.Inputs
  },
  {
    type: 'ProgressBar',
    displayName: 'ProgressBar',
    icon: 'linear_scale',
    description: 'Shows progress of a task',
    category: ToolboxCategory.Controls
  },
  {
    type: 'ActivityIndicator',
    displayName: 'ActivityIndicator',
    icon: 'refresh',
    description: 'Shows that work is in progress',
    category: ToolboxCategory.Controls
  },
  {
    type: 'Image',
    displayName: 'Image',
    icon: 'image',
    description: 'Displays images',
    category: ToolboxCategory.Controls
  },
  {
    type: 'Path',
    displayName: 'Icon',
    icon: 'gesture',
    description: 'Displays SVG/XAML path icons',
    category: ToolboxCategory.Controls
  },
   
  // Layouts
  {
    type: 'StackLayout',
    displayName: 'StackLayout',
    icon: 'view_agenda',
    description: 'Arranges children in a stack',
    category: ToolboxCategory.Layouts
  },
  {
    type: 'VerticalStackLayout',
    displayName: 'VerticalStackLayout',
    icon: 'view_week',
    description: 'Arranges children vertically in rows',
    category: ToolboxCategory.Layouts
  },
  {
    type: 'Grid',
    displayName: 'Grid',
    icon: 'grid_view',
    description: 'Arranges children in rows and columns',
    category: ToolboxCategory.Layouts
  },
  {
    type: 'AbsoluteLayout',
    displayName: 'AbsoluteLayout',
    icon: 'crop_free',
    description: 'Positions children at absolute coordinates',
    category: ToolboxCategory.Layouts
  },
  
  // Views
  {
    type: 'Frame',
    displayName: 'Frame',
    icon: 'crop_portrait',
    description: 'Container with border and background',
    category: ToolboxCategory.Views
  },
  {
    type: 'Border',
    displayName: 'Border',
    icon: 'rounded_corner',
    description: 'Container with a stroke and corner radius',
    category: ToolboxCategory.Views
  },
  {
    type: 'ScrollView',
    displayName: 'ScrollView',
    icon: 'unfold_more',
    description: 'Scrollable container',
    category: ToolboxCategory.Views
  },
  {
    type: 'CollectionView',
    displayName: 'CollectionView',
    icon: 'view_list',
    description: 'Repeats an item template over a bound collection',
    category: ToolboxCategory.Views
  }
];