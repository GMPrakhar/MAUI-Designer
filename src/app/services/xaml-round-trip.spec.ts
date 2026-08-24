import { TestBed } from '@angular/core/testing';
import { XamlParserService } from './xaml-parser';
import { XamlGeneratorService } from './xaml-generator';
import { ElementType, MauiElement } from '../models/maui-element';

const page = (body: string) => `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="YourApp.MainPage">
    <VerticalStackLayout x:Name="Root">
${body}
    </VerticalStackLayout>
</ContentPage>`;

describe('XAML round trip fidelity', () => {
  let parser: XamlParserService;
  let generator: XamlGeneratorService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    parser = TestBed.inject(XamlParserService);
    generator = TestBed.inject(XamlGeneratorService);
  });

  const firstChild = (xaml: string): MauiElement => parser.parseXaml(xaml).children[0];

  describe('unmodelled attributes', () => {
    it('keeps attributes the designer does not model on a built in control', () => {
      const label = firstChild(page('<Label x:Name="L" Text="Hi" Rotation="15" Opacity="0.5" />'));

      expect(label.type).toBe(ElementType.Label);
      expect(label.properties.rawAttributes).toEqual({ Rotation: '15', Opacity: '0.5' });
    });

    it('re emits preserved attributes so an import export cycle is not lossy', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" Rotation="15" Opacity="0.5" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).toContain('Rotation="15"');
      expect(xaml).toContain('Opacity="0.5"');
    });

    it('does not duplicate attributes the designer already models', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" FontSize="22" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml.match(/FontSize=/g)?.length).toBe(1);
      expect(xaml).toContain('FontSize="22"');
    });

    it('leaves bound attributes to the binding pass rather than preserving them twice', () => {
      const label = firstChild(page('<Label x:Name="L" Text="{Binding UserName}" />'));

      expect(label.properties.bindings).toEqual({ Text: 'UserName' });
      expect(label.properties.rawAttributes).toBeUndefined();
    });

    it('never preserves namespace declarations as element attributes', () => {
      const root = parser.parseXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <VerticalStackLayout x:Name="Root" xmlns:local="clr-namespace:App" Rotation="4">
        <Label x:Name="L" />
    </VerticalStackLayout>
</ContentPage>`);

      expect(root.properties.rawAttributes).toEqual({ Rotation: '4' });
    });

    it('does not preserve a themed colour as a raw attribute as well', () => {
      const markup = '<Label x:Name="L" Text="Hi" TextColor="{AppThemeBinding Light=#111111, Dark=#eeeeee}" />';
      const label = firstChild(page(markup));

      expect(label.properties.appTheme?.['TextColor']).toEqual({ light: '#111111', dark: '#eeeeee' });
      expect(label.properties.rawAttributes?.['TextColor']).toBeUndefined();

      const xaml = generator.generateXaml(parser.parseXaml(page(markup)));
      expect(xaml.match(/TextColor=/g)?.length).toBe(1);
    });

    it('escapes preserved values so injected markup cannot break the document', () => {      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" AutomationId="a&quot;b&amp;c" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).toContain('AutomationId="a&quot;b&amp;c"');
    });
  });

  describe('LayoutOptions', () => {
    it('parses HorizontalOptions and VerticalOptions', () => {
      const label = firstChild(page('<Label x:Name="L" HorizontalOptions="Center" VerticalOptions="Fill" />'));

      expect(label.properties.horizontalOptions).toBe('Center');
      expect(label.properties.verticalOptions).toBe('Fill');
    });

    it('normalises the legacy AndExpand suffix inherited from Xamarin.Forms', () => {
      expect(XamlParserService.parseLayoutOptions('EndAndExpand')).toBe('End');
      expect(XamlParserService.parseLayoutOptions('StartAndExpand')).toBe('Start');
    });

    it('accepts the fully qualified LayoutOptions.Center form', () => {
      expect(XamlParserService.parseLayoutOptions('LayoutOptions.Center')).toBe('Center');
    });

    it('rejects values that are not LayoutOptions', () => {
      expect(XamlParserService.parseLayoutOptions('Middle')).toBeNull();
    });

    it('preserves an unrecognised alignment verbatim instead of dropping it', () => {
      const label = firstChild(page('<Label x:Name="L" HorizontalOptions="{StaticResource Wide}" />'));

      expect(label.properties.horizontalOptions).toBeUndefined();
      expect(label.properties.rawAttributes?.['HorizontalOptions']).toBe('{StaticResource Wide}');
    });

    it('generates the alignment attributes', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" HorizontalOptions="Center" VerticalOptions="End" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).toContain('HorizontalOptions="Center"');
      expect(xaml).toContain('VerticalOptions="End"');
    });

    it('omits the alignment attributes when the element inherits them', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).not.toContain('HorizontalOptions=');
      expect(xaml).not.toContain('VerticalOptions=');
    });

    it('survives a full parse generate parse cycle', () => {
      const original = page('<Label x:Name="L" Text="Hi" HorizontalOptions="Center" VerticalOptions="Fill" />');

      const reparsed = firstChild(generator.generateXaml(parser.parseXaml(original)));

      expect(reparsed.properties.horizontalOptions).toBe('Center');
      expect(reparsed.properties.verticalOptions).toBe('Fill');
    });
  });
});
