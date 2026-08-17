import { TestBed } from '@angular/core/testing';
import { XamlParserService } from './xaml-parser';
import { XamlGeneratorService } from './xaml-generator';
import { CustomControlRegistryService } from './custom-control-registry';
import { ElementType } from '../models/maui-element';

const THIRD_PARTY_XAML = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"
             xmlns:sf="clr-namespace:Syncfusion.Maui.Inputs;assembly=Syncfusion.Maui.Inputs"
             x:Class="YourApp.MainPage">
    <AbsoluteLayout x:Name="RootLayout" WidthRequest="800" HeightRequest="600">
        <toolkit:AvatarView x:Name="Avatar" Text="AB" CornerRadius="24"
                            AbsoluteLayout.LayoutBounds="10,10,48,48" WidthRequest="48" HeightRequest="48" />
        <sf:SfComboBox x:Name="Picker" Placeholder="Pick one" ItemsSource="{Binding Items}"
                       AbsoluteLayout.LayoutBounds="10,80,200,40" WidthRequest="200" HeightRequest="40" />
    </AbsoluteLayout>
</ContentPage>`;

describe('Custom controls in XAML', () => {
  let parser: XamlParserService;
  let generator: XamlGeneratorService;
  let registry: CustomControlRegistryService;

  beforeEach(() => {
    localStorage.removeItem('maui-designer.custom-controls');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    parser = TestBed.inject(XamlParserService);
    generator = TestBed.inject(XamlGeneratorService);
    registry = TestBed.inject(CustomControlRegistryService);
  });

  afterEach(() => localStorage.removeItem('maui-designer.custom-controls'));

  it('keeps unknown controls instead of dropping them', () => {
    const root = parser.parseXaml(THIRD_PARTY_XAML);

    expect(root.children.length).toBe(2);
    expect(root.children[0].type).toBe(ElementType.Custom);
    expect(root.children[0].properties.customTag).toBe('AvatarView');
    expect(root.children[0].properties.customPrefix).toBe('toolkit');
    expect(root.children[1].properties.customTag).toBe('SfComboBox');
  });

  it('maps manifest properties and preserves the rest', () => {
    const root = parser.parseXaml(THIRD_PARTY_XAML);
    const avatar = root.children[0];
    const combo = root.children[1];

    expect(avatar.properties.customValues!['Text']).toBe('AB');
    expect(avatar.properties.customValues!['CornerRadius']).toBe('24');
    // SfComboBox is unknown, so its attributes are learned into a manifest
    expect(combo.properties.customValues!['Placeholder']).toBe('Pick one');
  });

  it('captures bindings on custom controls', () => {
    const root = parser.parseXaml(THIRD_PARTY_XAML);

    expect(root.children[1].properties.bindings!['ItemsSource']).toBe('Items');
  });

  it('keeps the designer layout properties of custom controls', () => {
    const root = parser.parseXaml(THIRD_PARTY_XAML);
    const avatar = root.children[0];

    expect(avatar.properties.x).toBe(10);
    expect(avatar.properties.y).toBe(10);
    expect(avatar.properties.width).toBe(48);
    expect(avatar.name).toBe('Avatar');
  });

  it('regenerates the third party tags with their namespaces', () => {
    const root = parser.parseXaml(THIRD_PARTY_XAML);
    const xaml = generator.generateXaml(root);

    expect(xaml).toContain('xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"');
    expect(xaml).toContain('xmlns:sf="clr-namespace:Syncfusion.Maui.Inputs;assembly=Syncfusion.Maui.Inputs"');
    expect(xaml).toContain('<toolkit:AvatarView');
    expect(xaml).toContain('<sf:SfComboBox');
    expect(xaml).toContain('Text="AB"');
    expect(xaml).toContain('CornerRadius="24"');
    expect(xaml).toContain('ItemsSource="{Binding Items}"');
  });

  it('only declares namespaces that are used', () => {
    const root = parser.parseXaml(THIRD_PARTY_XAML);
    root.children = [];

    expect(generator.generateXaml(root)).not.toContain('xmlns:toolkit');
  });

  it('round trips a custom control through parse and generate', () => {
    const first = generator.generateXaml(parser.parseXaml(THIRD_PARTY_XAML));
    const second = generator.generateXaml(parser.parseXaml(first));

    expect(second).toContain('<toolkit:AvatarView');
    expect(second).toContain('Text="AB"');
    expect(second).toContain('Placeholder="Pick one"');
    expect(second).toContain('ItemsSource="{Binding Items}"');
  });

  it('preserves attributes the designer does not model', () => {
    const xaml = THIRD_PARTY_XAML.replace('Text="AB"', 'Text="AB" ImageSource="avatar.png" Rotation="15"');
    const root = parser.parseXaml(xaml);
    const avatar = root.children[0];

    expect(avatar.properties.customValues!['ImageSource']).toBe('avatar.png');
    expect(avatar.properties.rawAttributes!['Rotation']).toBe('15');
    expect(generator.generateXaml(root)).toContain('Rotation="15"');
  });

  it('keeps property elements of custom controls verbatim', () => {
    const xaml = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit">
    <AbsoluteLayout x:Name="RootLayout" WidthRequest="800" HeightRequest="600">
        <toolkit:Expander x:Name="Details" AbsoluteLayout.LayoutBounds="0,0,240,120" WidthRequest="240" HeightRequest="120">
            <toolkit:Expander.Header>
                <Label Text="More" />
            </toolkit:Expander.Header>
        </toolkit:Expander>
    </AbsoluteLayout>
</ContentPage>`;

    const root = parser.parseXaml(xaml);
    const generated = generator.generateXaml(root);

    expect(root.children[0].properties.rawContentXml!.length).toBe(1);
    expect(generated).toContain('<toolkit:Expander.Header>');
    expect(generated).toContain('Text="More"');
  });

  it('nests children of a custom container', () => {
    const xaml = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit">
    <AbsoluteLayout x:Name="RootLayout" WidthRequest="800" HeightRequest="600">
        <toolkit:Expander x:Name="Details" AbsoluteLayout.LayoutBounds="0,0,240,120" WidthRequest="240" HeightRequest="120">
            <Label x:Name="Inner" Text="Body" />
        </toolkit:Expander>
    </AbsoluteLayout>
</ContentPage>`;

    const root = parser.parseXaml(xaml);
    const expander = root.children[0];

    expect(expander.children.length).toBe(1);
    expect(expander.children[0].type).toBe(ElementType.Label);
    expect(generator.generateXaml(root)).toContain('<Label x:Name="Inner"');
  });

  it('registers unknown controls so they appear in the toolbox', () => {
    parser.parseXaml(THIRD_PARTY_XAML);

    const lookup = registry.find('sf', 'SfComboBox');
    expect(lookup).toBeTruthy();
    expect(lookup!.manifest.xmlns.uri).toContain('Syncfusion');
  });
});
