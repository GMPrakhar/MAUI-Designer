import { TestBed } from '@angular/core/testing';
import { XamlGeneratorService } from '../services/xaml-generator';
import { XamlParserService } from '../services/xaml-parser';
import { ElementService } from '../services/element';
import { DEFAULT_ICON_PATH_DATA, ElementType } from '../models/maui-element';

describe('XAML Services', () => {
  let elementService: ElementService;
  let xamlGenerator: XamlGeneratorService;
  let xamlParser: XamlParserService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    elementService = TestBed.inject(ElementService);
    xamlGenerator = TestBed.inject(XamlGeneratorService);
    xamlParser = TestBed.inject(XamlParserService);
  });

  describe('XamlGeneratorService', () => {
    it('should be created', () => {
      expect(xamlGenerator).toBeTruthy();
    });

    it('should generate XAML for simple elements', () => {
      const root = elementService.getRootElement();
      const label = elementService.createElement(ElementType.Label, {
        x: 10,
        y: 20,
        width: 100,
        height: 30,
        text: 'Test Label'
      });
      elementService.addElement(label, root);

      const xaml = xamlGenerator.generateXaml(root);
      
      expect(xaml).toContain('<AbsoluteLayout');
      expect(xaml).toContain('<Label');
      expect(xaml).toContain('Text="Test Label"');
      expect(xaml).toContain('AbsoluteLayout.LayoutBounds="10,20,100,30"');
    });

    it('should generate XAML Path elements for icons', () => {
      const root = elementService.getRootElement();
      const icon = elementService.createElement(ElementType.Path, {
        x: 5,
        y: 6,
        width: 24,
        height: 24,
        pathData: DEFAULT_ICON_PATH_DATA,
        fillColor: '#111111',
        strokeColor: 'Transparent',
        strokeThickness: 0
      });
      elementService.addElement(icon, root);

      const xaml = xamlGenerator.generateXaml(root);

      expect(xaml).toContain('<Path');
      expect(xaml).toContain(`Data="${DEFAULT_ICON_PATH_DATA}"`);
      expect(xaml).toContain('Fill="#111111"');
      expect(xaml).toContain('Stroke="Transparent"');
      expect(xaml).toContain('AbsoluteLayout.LayoutBounds="5,6,24,24"');
    });
  });

  describe('XamlParserService', () => {
    it('should be created', () => {
      expect(xamlParser).toBeTruthy();
    });

    it('should parse simple XAML', () => {
      const simpleXaml = `
        <AbsoluteLayout BackgroundColor="#ffffff">
          <Label Text="Hello World" 
                 TextColor="#000000" 
                 AbsoluteLayout.LayoutBounds="50,50,200,30" 
                 AbsoluteLayout.LayoutFlags="None" />
        </AbsoluteLayout>`;

      const parsed = xamlParser.parseXaml(simpleXaml);
      
      expect(parsed.type).toBe(ElementType.AbsoluteLayout);
      expect(parsed.children.length).toBe(1);
      expect(parsed.children[0].type).toBe(ElementType.Label);
      expect(parsed.children[0].properties.text).toBe('Hello World');
      expect(parsed.children[0].properties.x).toBe(50);
      expect(parsed.children[0].properties.y).toBe(50);
    });

    it('should parse XAML Path icon elements', () => {
      const iconXaml = `
        <AbsoluteLayout>
          <Path Data="${DEFAULT_ICON_PATH_DATA}"
                Fill="#111111"
                Stroke="Transparent"
                StrokeThickness="0"
                AbsoluteLayout.LayoutBounds="5,6,24,24" />
        </AbsoluteLayout>`;

      const parsed = xamlParser.parseXaml(iconXaml);
      const icon = parsed.children[0];

      expect(icon.type).toBe(ElementType.Path);
      expect(icon.properties.pathData).toBe(DEFAULT_ICON_PATH_DATA);
      expect(icon.properties.fillColor).toBe('#111111');
      expect(icon.properties.strokeColor).toBe('Transparent');
      expect(icon.properties.strokeThickness).toBe(0);
      expect(icon.properties.x).toBe(5);
      expect(icon.properties.y).toBe(6);
    });

    it('should convert pasted SVG paths into MAUI Path elements', () => {
      const heroIconSvg = `
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" color="#222222" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
        </svg>`;

      const parsed = xamlParser.parseXaml(heroIconSvg);
      const icon = parsed.children[0];

      expect(parsed.type).toBe(ElementType.AbsoluteLayout);
      expect(icon.type).toBe(ElementType.Path);
      expect(icon.properties.pathData).toBe('M4.5 12.75l6 6 9-13.5');
      expect(icon.properties.fillColor).toBe('Transparent');
      expect(icon.properties.strokeColor).toBe('#222222');
      expect(icon.properties.strokeThickness).toBe(1.5);
    });
  });

  describe('AppThemeBinding', () => {
    /** Builds a single-child page and returns the generated XAML. */
    function generate(properties: Record<string, unknown>, type = ElementType.Label) {
      const root = elementService.getRootElement();
      const element = elementService.createElement(type, properties as any);
      elementService.addElement(element, root);
      return xamlGenerator.generateXaml(root);
    }

    it('emits a light and dark pair as an AppThemeBinding', () => {
      const xaml = generate({
        text: 'Hi',
        textColor: '#112233',
        appTheme: { TextColor: { light: '#FFFFFF', dark: '#333333' } }
      });

      expect(xaml).toContain('TextColor="{AppThemeBinding Light=#FFFFFF, Dark=#333333}"');
      // The literal must not also be written, or the first one would win.
      expect(xaml).not.toContain('TextColor="#112233"');
    });

    it('emits only the half that is set', () => {
      expect(generate({ appTheme: { BackgroundColor: { light: '#ABCDEF' } } }))
        .toContain('BackgroundColor="{AppThemeBinding Light=#ABCDEF}"');

      elementService.clearDesign();

      expect(generate({ appTheme: { BackgroundColor: { dark: '#101010' } } }))
        .toContain('BackgroundColor="{AppThemeBinding Dark=#101010}"');
    });

    it('lets a data binding win over both the literal and the theme colour', () => {
      const xaml = generate({
        text: 'Hi',
        textColor: '#112233',
        bindings: { TextColor: 'Accent' },
        appTheme: { TextColor: { light: '#FFFFFF' } }
      });

      // Regression guard: colours used to be written directly instead of going
      // through the binding-aware push, and attributes are de-duplicated
      // first-writer-wins, so the literal silently shadowed the binding.
      expect(xaml).toContain('TextColor="{Binding Accent}"');
      expect(xaml).not.toContain('TextColor="#112233"');
      expect(xaml).not.toContain('AppThemeBinding');
    });

    it('parses an AppThemeBinding back into light and dark values', () => {
      const page = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <AbsoluteLayout>
    <Label x:Name="L1" Text="Hi" TextColor="{AppThemeBinding Light=#FFFFFF, Dark=#333333}" />
  </AbsoluteLayout>
</ContentPage>`;

      const label = xamlParser.parseXaml(page).children[0];

      expect(label.properties.appTheme?.['TextColor']).toEqual({ light: '#FFFFFF', dark: '#333333' });
      // The canvas needs something concrete to paint; light is the fallback.
      expect(label.properties.textColor).toBe('#FFFFFF');
    });

    it('round trips an AppThemeBinding without losing either value', () => {
      const page = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <AbsoluteLayout>
    <Label x:Name="L1" Text="Hi" BackgroundColor="{AppThemeBinding Light=#EEEEEE, Dark=#111111}" />
  </AbsoluteLayout>
</ContentPage>`;

      const regenerated = xamlGenerator.generateXaml(xamlParser.parseXaml(page));

      expect(regenerated).toContain('BackgroundColor="{AppThemeBinding Light=#EEEEEE, Dark=#111111}"');
    });

    it('treats Default as the light value', () => {
      expect(XamlParserService.parseAppThemeBinding('{AppThemeBinding Default=#AAAAAA, Dark=#000000}'))
        .toEqual({ light: '#AAAAAA', dark: '#000000' });
    });

    it('ignores anything that is not an AppThemeBinding', () => {
      expect(XamlParserService.parseAppThemeBinding('#FFFFFF')).toBeNull();
      expect(XamlParserService.parseAppThemeBinding('{Binding Accent}')).toBeNull();
    });
  });
});