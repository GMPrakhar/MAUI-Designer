import { TestBed } from '@angular/core/testing';
import { AccessibilityService } from './accessibility';
import { ElementService } from './element';
import { ElementType, MauiElement } from '../models/maui-element';
import { XamlGeneratorService } from './xaml-generator';
import { XamlParserService } from './xaml-parser';

describe('AccessibilityService', () => {
  let service: AccessibilityService;
  let elements: ElementService;
  let root: MauiElement;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AccessibilityService);
    elements = TestBed.inject(ElementService);
    root = elements.getRootElement();
    elements.updateElementProperties(root, { backgroundColor: '#ffffff' });
  });

  function addLabel(properties: Record<string, unknown>): MauiElement {
    const label = elements.createElement(ElementType.Label);
    elements.addElement(label, root);
    elements.updateElementProperties(label, properties as any);
    return label;
  }

  describe('contrast ratio', () => {
    // Checked against the WCAG reference values rather than against our own
    // output, so a mistake in the luminance formula cannot pass unnoticed.
    it('matches the published extremes', () => {
      expect(service.contrastRatio([0, 0, 0], [255, 255, 255])).toBeCloseTo(21, 2);
      expect(service.contrastRatio([255, 255, 255], [255, 255, 255])).toBeCloseTo(1, 2);
    });

    it('matches the canonical boundary example', () => {
      // #767676 on white is the standard 4.5:1 borderline case.
      expect(service.contrastRatio([118, 118, 118], [255, 255, 255])).toBeCloseTo(4.54, 1);
    });
  });

  describe('colour parsing', () => {
    it('reads the hex forms MAUI writes', () => {
      expect(service.toRgb('#ff8000')).toEqual([255, 128, 0]);
      expect(service.toRgb('#f80')).toEqual([255, 136, 0]);
      // MAUI orders the channels #AARRGGBB, so the alpha leads and is dropped.
      expect(service.toRgb('#80ff8000')).toEqual([255, 128, 0]);
    });

    it('reads the common named colours', () => {
      expect(service.toRgb('White')).toEqual([255, 255, 255]);
      expect(service.toRgb('black')).toEqual([0, 0, 0]);
    });

    it('returns null rather than guessing at what it does not know', () => {
      expect(service.toRgb('chartreuse')).toBeNull();
      expect(service.toRgb(undefined)).toBeNull();
      expect(service.toRgb('not-a-colour')).toBeNull();
    });
  });

  describe('auditing', () => {
    it('flags text that is too faint against its background', () => {
      const label = addLabel({ text: 'Hi', textColor: '#eeeeee' });

      expect(service.inspect(label).some(issue => issue.kind === 'low-contrast')).toBe(true);
    });

    it('accepts text that clears the threshold', () => {
      const label = addLabel({ text: 'Hi', textColor: '#000000' });

      expect(service.inspect(label).some(issue => issue.kind === 'low-contrast')).toBe(false);
    });

    it('falls back to the nearest ancestor that paints a background', () => {
      // The label sets no background of its own; the root's white is what the
      // text actually sits on.
      const label = addLabel({ text: 'Hi', textColor: '#000000' });

      expect(service.contrastOf(label)).toBeCloseTo(21, 1);
    });

    it('holds large text to the lower 3:1 threshold', () => {
      const label = addLabel({ text: 'Hi', textColor: '#949494', fontSize: 24 });
      expect(service.inspect(label).some(issue => issue.kind === 'low-contrast')).toBe(false);

      elements.updateElementProperties(label, { fontSize: 12 });
      expect(service.inspect(label).some(issue => issue.kind === 'low-contrast')).toBe(true);
    });

    it('gives no verdict when there is no text to judge', () => {
      const grid = elements.createElement(ElementType.Grid);
      elements.addElement(grid, root);

      expect(service.contrastOf(grid)).toBeNull();
    });

    it('gives no verdict when a colour cannot be resolved', () => {
      const label = addLabel({ text: 'Hi', textColor: 'rebeccapurple' });

      expect(service.contrastOf(label)).toBeNull();
    });

    it('asks for a description on elements that carry meaning', () => {
      const image = elements.createElement(ElementType.Image);
      elements.addElement(image, root);

      expect(service.inspect(image).some(issue => issue.kind === 'missing-description')).toBe(true);

      elements.updateElementProperties(image, { semanticDescription: 'Team photo' });
      expect(service.inspect(image).some(issue => issue.kind === 'missing-description')).toBe(false);
    });

    it('does not demand a description from decorative containers', () => {
      const grid = elements.createElement(ElementType.Grid);
      elements.addElement(grid, root);

      expect(service.inspect(grid)).toEqual([]);
    });

    it('walks the whole tree', () => {
      const image = elements.createElement(ElementType.Image);
      elements.addElement(image, root);
      addLabel({ text: 'Hi', textColor: '#eeeeee' });

      const kinds = service.audit(root).map(issue => issue.kind);

      expect(kinds).toContain('missing-description');
      expect(kinds).toContain('low-contrast');
    });
  });

  describe('SemanticProperties in XAML', () => {
    let generator: XamlGeneratorService;
    let parser: XamlParserService;

    beforeEach(() => {
      generator = TestBed.inject(XamlGeneratorService);
      parser = TestBed.inject(XamlParserService);
    });

    it('emits the attached properties', () => {
      const image = elements.createElement(ElementType.Image);
      elements.addElement(image, root);
      elements.updateElementProperties(image, {
        semanticDescription: 'Team photo',
        semanticHint: 'Opens the profile',
        semanticHeadingLevel: 'Level1'
      });

      const xaml = generator.generateXaml(root);

      expect(xaml).toContain('SemanticProperties.Description="Team photo"');
      expect(xaml).toContain('SemanticProperties.Hint="Opens the profile"');
      expect(xaml).toContain('SemanticProperties.HeadingLevel="Level1"');
    });

    it('round trips them without loss', () => {
      const page = `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
  <AbsoluteLayout>
    <Image x:Name="Photo" SemanticProperties.Description="Team photo" SemanticProperties.HeadingLevel="Level2" />
  </AbsoluteLayout>
</ContentPage>`;

      const parsed = parser.parseXaml(page);
      const image = parsed.children[0];

      expect(image.properties.semanticDescription).toBe('Team photo');
      expect(image.properties.semanticHeadingLevel).toBe('Level2');
      expect(generator.generateXaml(parsed)).toContain('SemanticProperties.Description="Team photo"');
    });
  });
});
