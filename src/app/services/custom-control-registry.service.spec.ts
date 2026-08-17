import { TestBed } from '@angular/core/testing';
import { CustomControlRegistryService } from './custom-control-registry';
import { COMMUNITY_TOOLKIT_MANIFEST, CustomControlManifest } from '../models/custom-control';
import { ElementService } from './element';
import { ElementType } from '../models/maui-element';

const SYNCFUSION_MANIFEST: CustomControlManifest = {
  id: 'syncfusion-sample',
  package: 'Syncfusion.Maui.Inputs',
  xmlns: { prefix: 'sf', uri: 'clr-namespace:Syncfusion.Maui.Inputs;assembly=Syncfusion.Maui.Inputs' },
  controls: [
    {
      tag: 'SfComboBox',
      displayName: 'Combo box',
      defaultWidth: 200,
      defaultHeight: 40,
      preview: { kind: 'box', label: '{Placeholder}' },
      properties: [
        { name: 'Placeholder', type: 'string', defaultValue: 'Select an item' },
        { name: 'IsEditable', type: 'boolean', defaultValue: false }
      ]
    }
  ]
};

describe('CustomControlRegistryService', () => {
  let service: CustomControlRegistryService;

  beforeEach(() => {
    localStorage.removeItem('maui-designer.custom-controls');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    service = TestBed.inject(CustomControlRegistryService);
  });

  afterEach(() => localStorage.removeItem('maui-designer.custom-controls'));

  it('ships the CommunityToolkit manifest out of the box', () => {
    const lookup = service.find('toolkit', 'AvatarView');

    expect(lookup).toBeTruthy();
    expect(lookup!.manifest.package).toBe(COMMUNITY_TOOLKIT_MANIFEST.package);
    expect(lookup!.definition.properties!.some(property => property.name === 'CornerRadius')).toBeTrue();
  });

  it('imports a manifest and exposes its controls', () => {
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));

    const lookup = service.find('sf', 'SfComboBox');
    expect(lookup).toBeTruthy();
    expect(lookup!.definition.displayName).toBe('Combo box');
    expect(service.controls.length).toBeGreaterThan(COMMUNITY_TOOLKIT_MANIFEST.controls.length);
  });

  it('imports an array of manifests', () => {
    service.import(JSON.stringify([SYNCFUSION_MANIFEST]));
    expect(service.find('sf', 'SfComboBox')).toBeTruthy();
  });

  it('rejects a manifest without a namespace', () => {
    expect(() => service.import(JSON.stringify({ package: 'Broken', controls: [] }))).toThrow();
  });

  it('finds controls case insensitively and without a prefix', () => {
    expect(service.find('toolkit', 'avatarview')).toBeTruthy();
    expect(service.find(undefined, 'AvatarView')).toBeTruthy();
  });

  it('removes a manifest', () => {
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));
    service.remove('syncfusion-sample');

    expect(service.find('sf', 'SfComboBox')).toBeNull();
  });

  it('persists manifests across instances', () => {
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const restored = TestBed.inject(CustomControlRegistryService);

    expect(restored.find('sf', 'SfComboBox')).toBeTruthy();
    expect(restored.find('toolkit', 'AvatarView')).toBeTruthy();
  });

  it('exports the registry as JSON', () => {
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));
    const exported = JSON.parse(service.export()) as CustomControlManifest[];

    expect(exported.some(manifest => manifest.id === 'syncfusion-sample')).toBeTrue();
  });

  it('learns an unknown control from imported XAML', () => {
    const lookup = service.learn('telerik', 'clr-namespace:Telerik.Maui', 'RadCalendar', {
      SelectionMode: 'Single',
      DayCellHeight: '32',
      IsVisible: 'True'
    });

    expect(lookup.definition.tag).toBe('RadCalendar');
    expect(lookup.manifest.learned).toBeTrue();
    expect(lookup.definition.properties!.find(p => p.name === 'DayCellHeight')!.type).toBe('number');
    expect(lookup.definition.properties!.find(p => p.name === 'IsVisible')!.type).toBe('boolean');
    expect(lookup.definition.properties!.find(p => p.name === 'SelectionMode')!.type).toBe('string');
  });

  it('never downgrades a supplied manifest to a learned one', () => {
    service.learn('toolkit', 'http://schemas.microsoft.com/dotnet/2022/maui/toolkit', 'AvatarView', { Text: 'ZZ' });

    const lookup = service.find('toolkit', 'AvatarView')!;
    expect(lookup.manifest.learned).toBeFalsy();
    expect(lookup.definition.displayName).toBe('AvatarView');
  });

  it('adds newly learned properties to an existing learned control', () => {
    service.learn('acme', 'clr-namespace:Acme', 'Gauge', { Value: '10' });
    const lookup = service.learn('acme', 'clr-namespace:Acme', 'Gauge', { Maximum: '100' });

    const names = lookup.definition.properties!.map(property => property.name);
    expect(names).toContain('Value');
    expect(names).toContain('Maximum');
  });

  it('builds default properties from a manifest', () => {
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));
    const lookup = service.find('sf', 'SfComboBox')!;

    const properties = service.defaultProperties(lookup);

    expect(properties.customTag).toBe('SfComboBox');
    expect(properties.customPrefix).toBe('sf');
    expect(properties.customNamespace).toContain('Syncfusion');
    expect(properties.width).toBe(200);
    expect(properties.customValues!['Placeholder']).toBe('Select an item');
  });

  it('interpolates preview placeholders from the element values', () => {
    const elements = TestBed.inject(ElementService);
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));
    const lookup = service.find('sf', 'SfComboBox')!;
    const element = elements.createElement(ElementType.Custom, service.defaultProperties(lookup));

    expect(service.interpolate('{Placeholder}', element)).toBe('Select an item');
    expect(service.interpolate('{Missing}', element)).toBe('');
  });

  it('lists bindable properties including preserved attributes', () => {
    const elements = TestBed.inject(ElementService);
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));
    const lookup = service.find('sf', 'SfComboBox')!;
    const element = elements.createElement(ElementType.Custom, {
      ...service.defaultProperties(lookup),
      rawAttributes: { ItemsSource: 'Items' }
    });

    const bindable = service.bindableProperties(element);
    expect(bindable).toContain('Placeholder');
    expect(bindable).toContain('ItemsSource');
  });

  it('restores the bundled manifests on reset', () => {
    service.import(JSON.stringify(SYNCFUSION_MANIFEST));
    service.resetToBundled();

    expect(service.find('sf', 'SfComboBox')).toBeNull();
    expect(service.find('toolkit', 'AvatarView')).toBeTruthy();
  });
});
