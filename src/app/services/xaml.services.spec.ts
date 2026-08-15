import { TestBed } from '@angular/core/testing';
import { XamlGeneratorService } from '../services/xaml-generator';
import { XamlParserService } from '../services/xaml-parser';
import { ElementService } from '../services/element';
import { ElementType } from '../models/maui-element';

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
        pathData: 'M12 2L2 22h20L12 2Z',
        fillColor: '#111111',
        strokeColor: 'Transparent',
        strokeThickness: 0
      });
      elementService.addElement(icon, root);

      const xaml = xamlGenerator.generateXaml(root);

      expect(xaml).toContain('<Path');
      expect(xaml).toContain('Data="M12 2L2 22h20L12 2Z"');
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
          <Path Data="M12 2L2 22h20L12 2Z"
                Fill="#111111"
                Stroke="Transparent"
                StrokeThickness="0"
                AbsoluteLayout.LayoutBounds="5,6,24,24" />
        </AbsoluteLayout>`;

      const parsed = xamlParser.parseXaml(iconXaml);
      const icon = parsed.children[0];

      expect(icon.type).toBe(ElementType.Path);
      expect(icon.properties.pathData).toBe('M12 2L2 22h20L12 2Z');
      expect(icon.properties.fillColor).toBe('#111111');
      expect(icon.properties.strokeColor).toBe('Transparent');
      expect(icon.properties.strokeThickness).toBe(0);
      expect(icon.properties.x).toBe(5);
      expect(icon.properties.y).toBe(6);
    });

    it('should convert pasted SVG paths into MAUI Path elements', () => {
      const heroIconSvg = `
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
        </svg>`;

      const parsed = xamlParser.parseXaml(heroIconSvg);
      const icon = parsed.children[0];

      expect(parsed.type).toBe(ElementType.AbsoluteLayout);
      expect(icon.type).toBe(ElementType.Path);
      expect(icon.properties.pathData).toBe('M4.5 12.75l6 6 9-13.5');
      expect(icon.properties.viewBox).toBe('0 0 24 24');
      expect(icon.properties.fillColor).toBe('Transparent');
      expect(icon.properties.strokeColor).toBe('#000000');
      expect(icon.properties.strokeThickness).toBe(1.5);
    });
  });
});