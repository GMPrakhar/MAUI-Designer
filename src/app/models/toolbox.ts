export interface ToolboxItem {
  type: string;
  displayName: string;
  icon: string;
  description: string;
  category: ToolboxCategory;
}

export enum ToolboxCategory {
  Controls = 'Controls',
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
    category: ToolboxCategory.Controls
  },
  {
    type: 'Editor',
    displayName: 'Editor',
    icon: 'edit_note',
    description: 'Multi-line text input',
    category: ToolboxCategory.Controls
  },
  {
    type: 'Image',
    displayName: 'Image',
    icon: 'image',
    description: 'Displays images',
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
    type: 'ScrollView',
    displayName: 'ScrollView',
    icon: 'unfold_more',
    description: 'Scrollable container',
    category: ToolboxCategory.Views
  }
];