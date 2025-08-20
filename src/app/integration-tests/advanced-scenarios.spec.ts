import { TestBed } from '@angular/core/testing';
import { ElementService } from '../services/element';
import { XamlGeneratorService } from '../services/xaml-generator';
import { XamlParserService } from '../services/xaml-parser';
import { ElementType, MauiElement } from '../models/maui-element';

describe('Advanced Integration Scenarios', () => {
  let elementService: ElementService;
  let xamlGenerator: XamlGeneratorService;
  let xamlParser: XamlParserService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    elementService = TestBed.inject(ElementService);
    xamlGenerator = TestBed.inject(XamlGeneratorService);
    xamlParser = TestBed.inject(XamlParserService);
  });

  describe('Complex Workflow Integration Tests', () => {
    it('should handle complete workflow: create elements, update properties, generate and re-parse XAML', () => {
      const root = elementService.getRootElement();
      
      // Step 1: Create a complex layout programmatically
      const headerLabel = elementService.createElement(ElementType.Label, {
        x: 10, y: 10, width: 300, height: 40,
        text: 'My Application',
        fontSize: 24,
        textColor: '#007acc',
        backgroundColor: '#f0f0f0'
      });
      
      const inputEntry = elementService.createElement(ElementType.Entry, {
        x: 10, y: 60, width: 250, height: 35,
        text: 'Enter your name',
        backgroundColor: '#ffffff'
      });
      
      const submitButton = elementService.createElement(ElementType.Button, {
        x: 270, y: 60, width: 80, height: 35,
        text: 'Submit',
        backgroundColor: '#28a745',
        textColor: '#ffffff'
      });
      
      // Add to root
      elementService.addElement(headerLabel, root);
      elementService.addElement(inputEntry, root);
      elementService.addElement(submitButton, root);
      
      // Step 2: Update properties through service
      elementService.selectElement(headerLabel);
      elementService.updateElementProperties(headerLabel, {
        text: 'Updated Application Title',
        fontSize: 28,
        margin: { left: 5, top: 5, right: 5, bottom: 5 }
      });
      
      // Step 3: Generate XAML
      const generatedXaml = xamlGenerator.generateXaml(root);
      
      expect(generatedXaml).toContain('Updated Application Title');
      expect(generatedXaml).toContain('FontSize="28"');
      expect(generatedXaml).toContain('Placeholder="Enter your name"');
      expect(generatedXaml).toContain('Text="Submit"');
      expect(generatedXaml).toContain('Margin="5'); // Accept either "5" or "5,5,5,5" format
      
      // Step 4: Parse the generated XAML back
      const parsedLayout = xamlParser.parseXaml(generatedXaml);
      
      expect(parsedLayout.children.length).toBe(3);
      expect(parsedLayout.children[0].properties.text).toBe('Updated Application Title');
      expect(parsedLayout.children[0].properties.fontSize).toBe(28);
      expect(parsedLayout.children[1].properties.text).toBe('Enter your name'); // Placeholder becomes text
      expect(parsedLayout.children[2].properties.text).toBe('Submit');
      
      // Step 5: Set the parsed layout and verify service state
      elementService.setRootElement(parsedLayout);
      const newRoot = elementService.getRootElement();
      
      expect(newRoot.children.length).toBe(3);
      expect(newRoot.children[0].properties.text).toBe('Updated Application Title');
    });

    it('should handle multiple layout types in a single design', () => {
      const root = elementService.getRootElement();
      
      // Create a StackLayout container
      const stackContainer = elementService.createElement(ElementType.StackLayout, {
        x: 50, y: 50, width: 300, height: 200,
        orientation: 'Vertical' as any,
        spacing: 10
      });
      
      // Create child elements for the stack
      const stackLabel1 = elementService.createElement(ElementType.Label, {
        text: 'Stack Item 1',
        textColor: '#333333'
      });
      
      const stackLabel2 = elementService.createElement(ElementType.Label, {
        text: 'Stack Item 2',
        textColor: '#666666'
      });
      
      // Add stack to root and children to stack
      elementService.addElement(stackContainer, root);
      elementService.addElement(stackLabel1, stackContainer);
      elementService.addElement(stackLabel2, stackContainer);
      
      // Create a separate Frame container
      const frameContainer = elementService.createElement(ElementType.Frame, {
        x: 400, y: 50, width: 200, height: 150,
        backgroundColor: '#e9ecef'
      });
      
      const frameContent = elementService.createElement(ElementType.Label, {
        text: 'Frame Content',
        textColor: '#495057'
      });
      
      elementService.addElement(frameContainer, root);
      elementService.addElement(frameContent, frameContainer);
      
      // Generate and verify XAML
      const xaml = xamlGenerator.generateXaml(root);
      
      expect(xaml).toContain('<VerticalStackLayout'); // Generator converts StackLayout to VerticalStackLayout
      expect(xaml).toContain('Spacing="10"'); // Note: Orientation may not be explicitly included for VerticalStackLayout
      expect(xaml).toContain('<Frame');
      expect(xaml).toContain('Stack Item 1');
      expect(xaml).toContain('Stack Item 2');
      expect(xaml).toContain('Frame Content');
      
      // Parse back and verify structure
      const parsed = xamlParser.parseXaml(xaml);
      expect(parsed.children.length).toBe(2); // StackLayout + Frame
      // Note: Generator converts StackLayout to VerticalStackLayout in XAML, 
      // and parser correctly identifies it as VerticalStackLayout
      expect([ElementType.StackLayout, ElementType.VerticalStackLayout]).toContain(parsed.children[0].type);
      expect(parsed.children[0].children.length).toBe(2); // Two labels in stack
      expect(parsed.children[1].type).toBe(ElementType.Frame);
      expect(parsed.children[1].children.length).toBe(1); // One label in frame
    });

    it('should preserve element hierarchy when updating properties', () => {
      const root = elementService.getRootElement();
      
      // Create nested structure
      const outerContainer = elementService.createElement(ElementType.StackLayout, {
        x: 0, y: 0, width: 400, height: 300
      });
      
      const innerContainer = elementService.createElement(ElementType.Frame, {
        backgroundColor: '#f8f9fa'
      });
      
      const deepElement = elementService.createElement(ElementType.Button, {
        text: 'Deep Button',
        backgroundColor: '#007acc'
      });
      
      // Build hierarchy
      elementService.addElement(outerContainer, root);
      elementService.addElement(innerContainer, outerContainer);
      elementService.addElement(deepElement, innerContainer);
      
      // Verify initial hierarchy
      expect(root.children[0]).toBe(outerContainer);
      expect(outerContainer.children[0]).toBe(innerContainer);
      expect(innerContainer.children[0]).toBe(deepElement);
      expect(deepElement.parent).toBe(innerContainer);
      expect(innerContainer.parent).toBe(outerContainer);
      expect(outerContainer.parent).toBe(root);
      
      // Update properties at different levels
      elementService.updateElementProperties(outerContainer, {
        spacing: 15,
        backgroundColor: '#ffffff'
      });
      
      elementService.updateElementProperties(deepElement, {
        text: 'Updated Deep Button',
        textColor: '#ffffff',
        width: 150,
        height: 40
      });
      
      // Verify hierarchy is preserved after updates
      expect(root.children[0]).toBe(outerContainer);
      expect(outerContainer.children[0]).toBe(innerContainer);
      expect(innerContainer.children[0]).toBe(deepElement);
      expect(deepElement.properties.text).toBe('Updated Deep Button');
      expect(outerContainer.properties.spacing).toBe(15);
      
      // Generate XAML and verify nested structure
      const xaml = xamlGenerator.generateXaml(root);
      expect(xaml).toContain('<VerticalStackLayout');
      expect(xaml).toContain('<Frame');
      expect(xaml).toContain('<Button');
      expect(xaml).toContain('Updated Deep Button');
      expect(xaml).toContain('Spacing="15"');
    });

    it('should handle element removal and selection updates', () => {
      const root = elementService.getRootElement();
      
      // Create several elements
      const label1 = elementService.createElement(ElementType.Label, { text: 'Label 1' });
      const label2 = elementService.createElement(ElementType.Label, { text: 'Label 2' });
      const button1 = elementService.createElement(ElementType.Button, { text: 'Button 1' });
      
      elementService.addElement(label1, root);
      elementService.addElement(label2, root);
      elementService.addElement(button1, root);
      
      expect(root.children.length).toBe(3);
      
      // Select an element
      elementService.selectElement(label2);
      expect(elementService.getSelectedElement()).toBe(label2);
      
      // Remove the selected element
      elementService.removeElement(label2);
      
      expect(root.children.length).toBe(2);
      expect(elementService.getSelectedElement()).toBeNull(); // Selection should be cleared
      expect(root.children).not.toContain(label2);
      expect(root.children).toContain(label1);
      expect(root.children).toContain(button1);
      
      // Verify XAML generation excludes removed element
      const xaml = xamlGenerator.generateXaml(root);
      expect(xaml).toContain('Label 1');
      expect(xaml).not.toContain('Label 2');
      expect(xaml).toContain('Button 1');
    });

    it('should handle empty and minimal XAML correctly', () => {
      // Test minimal XAML
      const minimalXaml = '<AbsoluteLayout />';
      const parsed = xamlParser.parseXaml(minimalXaml);
      
      expect(parsed.type).toBe(ElementType.AbsoluteLayout);
      expect(parsed.children.length).toBe(0);
      
      // Test regeneration of minimal layout
      const regenerated = xamlGenerator.generateXaml(parsed);
      expect(regenerated).toContain('<AbsoluteLayout');
      // For empty layouts, generator creates self-closing tags or regular tags
      expect(regenerated).toMatch(/AbsoluteLayout[^>]*(\/>|>[^<]*<\/AbsoluteLayout>)/);
    });

    it('should handle special characters and escaping in text properties', () => {
      const root = elementService.getRootElement();
      
      const labelWithSpecialChars = elementService.createElement(ElementType.Label, {
        text: 'Special & "quoted" <text> content',
        x: 10, y: 10
      });
      
      elementService.addElement(labelWithSpecialChars, root);
      
      const xaml = xamlGenerator.generateXaml(root);
      
      // Check that special characters are properly escaped in XAML
      expect(xaml).toContain('Special &amp; &quot;quoted&quot; &lt;text&gt; content');
      
      // Parse it back and verify the text is correctly unescaped
      const parsed = xamlParser.parseXaml(xaml);
      expect(parsed.children[0].properties.text).toBe('Special & "quoted" <text> content');
    });
  });
});