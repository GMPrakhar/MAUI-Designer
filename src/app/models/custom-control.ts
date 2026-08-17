/**
 * Third party controls (Syncfusion, Telerik, CommunityToolkit, in-house NuGet
 * packages, ...) are described by JSON manifests instead of code, so the
 * designer can render, edit and generate them without knowing the package.
 */

export type CustomPropertyType = 'string' | 'number' | 'boolean' | 'color' | 'enum';

export interface CustomPropertyDefinition {
  /** XAML attribute name, e.g. `CornerRadius`. */
  name: string;
  type: CustomPropertyType;
  defaultValue?: string | number | boolean;
  /** Allowed values for `enum` properties. */
  options?: string[];
  /** Defaults to true: the property is offered in the Data Bindings section. */
  bindable?: boolean;
  description?: string;
}

/** How a custom control is drawn on the canvas. */
export type CustomPreviewKind = 'box' | 'text' | 'image' | 'list' | 'slot';

export interface CustomPreview {
  kind: CustomPreviewKind;
  /** Supports `{PropertyName}` placeholders, e.g. `"{Text}"`. */
  label?: string;
  backgroundColor?: string;
  textColor?: string;
  borderColor?: string;
  /** Supports `{PropertyName}` placeholders. */
  cornerRadius?: string;
  /** Material icon name shown in the preview. */
  icon?: string;
}

export interface CustomControlDefinition {
  /** Local XAML tag name without the prefix, e.g. `AvatarView`. */
  tag: string;
  displayName?: string;
  description?: string;
  /** Material icon name for the toolbox entry. */
  icon?: string;
  canHaveChildren?: boolean;
  defaultWidth?: number;
  defaultHeight?: number;
  preview?: CustomPreview;
  properties?: CustomPropertyDefinition[];
}

export interface CustomNamespace {
  /** XML prefix used in the document, e.g. `toolkit`. */
  prefix: string;
  /** Namespace URI or `clr-namespace:` declaration. */
  uri: string;
}

export interface CustomControlManifest {
  id: string;
  /** Package or library name shown as the toolbox group title. */
  package: string;
  version?: string;
  description?: string;
  xmlns: CustomNamespace;
  controls: CustomControlDefinition[];
  /**
   * True when the manifest was inferred from imported XAML rather than
   * supplied by the user, so it can be refreshed as more XAML is seen.
   */
  learned?: boolean;
}

/** A control together with the manifest it came from. */
export interface CustomControlLookup {
  manifest: CustomControlManifest;
  definition: CustomControlDefinition;
}

/** The key used to match an element to its definition: `prefix:Tag`. */
export function customControlKey(prefix: string | undefined, tag: string): string {
  return prefix ? `${prefix}:${tag}` : tag;
}

/** Bundled manifest for the official .NET MAUI Community Toolkit. */
export const COMMUNITY_TOOLKIT_MANIFEST: CustomControlManifest = {
  id: 'communitytoolkit-maui',
  package: 'CommunityToolkit.Maui',
  version: '9.x',
  description: 'Official .NET MAUI Community Toolkit views',
  xmlns: {
    prefix: 'toolkit',
    uri: 'http://schemas.microsoft.com/dotnet/2022/maui/toolkit'
  },
  controls: [
    {
      tag: 'AvatarView',
      displayName: 'AvatarView',
      description: 'Circular avatar with initials or an image',
      icon: 'account_circle',
      defaultWidth: 48,
      defaultHeight: 48,
      preview: { kind: 'text', label: '{Text}', cornerRadius: '{CornerRadius}', backgroundColor: '#512bd4', textColor: '#ffffff' },
      properties: [
        { name: 'Text', type: 'string', defaultValue: 'AB' },
        { name: 'CornerRadius', type: 'number', defaultValue: 24 },
        { name: 'BorderColor', type: 'color', defaultValue: '#512bd4' },
        { name: 'BorderWidth', type: 'number', defaultValue: 1 },
        { name: 'ImageSource', type: 'string' }
      ]
    },
    {
      tag: 'Expander',
      displayName: 'Expander',
      description: 'Collapsible container',
      icon: 'expand_more',
      canHaveChildren: true,
      defaultWidth: 240,
      defaultHeight: 120,
      preview: { kind: 'slot', label: 'Expander', icon: 'expand_more' },
      properties: [
        { name: 'IsExpanded', type: 'boolean', defaultValue: true },
        { name: 'Direction', type: 'enum', options: ['Down', 'Up'], defaultValue: 'Down' }
      ]
    },
    {
      tag: 'DrawingView',
      displayName: 'DrawingView',
      description: 'Free hand drawing surface',
      icon: 'draw',
      defaultWidth: 240,
      defaultHeight: 160,
      preview: { kind: 'box', label: 'DrawingView', icon: 'draw' },
      properties: [
        { name: 'LineColor', type: 'color', defaultValue: '#000000' },
        { name: 'LineWidth', type: 'number', defaultValue: 5 },
        { name: 'IsMultiLineModeEnabled', type: 'boolean', defaultValue: false }
      ]
    },
    {
      tag: 'MediaElement',
      displayName: 'MediaElement',
      description: 'Audio and video playback',
      icon: 'play_circle',
      defaultWidth: 280,
      defaultHeight: 160,
      preview: { kind: 'image', label: 'MediaElement', icon: 'play_circle', backgroundColor: '#101828' },
      properties: [
        { name: 'Source', type: 'string' },
        { name: 'ShouldAutoPlay', type: 'boolean', defaultValue: false },
        { name: 'ShouldShowPlaybackControls', type: 'boolean', defaultValue: true },
        { name: 'Aspect', type: 'enum', options: ['AspectFit', 'AspectFill', 'Fill'], defaultValue: 'AspectFit' }
      ]
    },
    {
      tag: 'Popup',
      displayName: 'Popup',
      description: 'Modal popup content host',
      icon: 'picture_in_picture',
      canHaveChildren: true,
      defaultWidth: 260,
      defaultHeight: 160,
      preview: { kind: 'slot', label: 'Popup', icon: 'picture_in_picture' },
      properties: [
        { name: 'CanBeDismissedByTappingOutsideOfPopup', type: 'boolean', defaultValue: true },
        { name: 'Color', type: 'color', defaultValue: '#ffffff' }
      ]
    },
    {
      tag: 'SemanticOrderView',
      displayName: 'SemanticOrderView',
      description: 'Controls the screen reader order of its children',
      icon: 'format_list_numbered',
      canHaveChildren: true,
      defaultWidth: 240,
      defaultHeight: 120,
      preview: { kind: 'slot', label: 'SemanticOrderView', icon: 'format_list_numbered' },
      properties: []
    }
  ]
};

/** Manifests shipped with the designer. */
export const BUNDLED_MANIFESTS: CustomControlManifest[] = [COMMUNITY_TOOLKIT_MANIFEST];
