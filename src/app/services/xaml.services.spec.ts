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
  });
});