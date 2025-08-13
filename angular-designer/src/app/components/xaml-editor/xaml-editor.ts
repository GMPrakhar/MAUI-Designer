import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ElementService } from '../../services/element';
import { XamlGeneratorService } from '../../services/xaml-generator';
import { MauiElement } from '../../models/maui-element';
import { Observable, map, Subscription } from 'rxjs';

@Component({
  selector: 'app-xaml-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './xaml-editor.html',
  styleUrl: './xaml-editor.scss'
})
export class XamlEditorComponent implements OnInit, OnDestroy {
  xamlContent$: Observable<string>;
  
  editMode: 'generated' | 'input' = 'generated';
  inputXaml = '';
  isValidXaml = true;
  validationMessage = '';
  isApplying = false;
  
  private xamlSubscription?: Subscription;
  
  constructor(
    private elementService: ElementService,
    private xamlGenerator: XamlGeneratorService
  ) {
    this.xamlContent$ = this.elementService.elements$.pipe(
      map(rootElement => this.xamlGenerator.generateXaml(rootElement))
    );
  }

  ngOnInit() {
    // Initialize XAML editor
    this.xamlSubscription = this.xamlContent$.subscribe(xaml => {
      if (this.editMode === 'generated' || !this.inputXaml) {
        this.inputXaml = xaml;
      }
    });
  }
  
  ngOnDestroy() {
    if (this.xamlSubscription) {
      this.xamlSubscription.unsubscribe();
    }
  }
  
  setEditMode(mode: 'generated' | 'input') {
    this.editMode = mode;
    if (mode === 'input' && !this.inputXaml) {
      // Initialize input with current generated XAML
      this.xamlContent$.subscribe(xaml => {
        this.inputXaml = xaml;
        this.validateXaml();
      });
    }
  }
  
  onXamlInput() {
    this.validateXaml();
  }
  
  private validateXaml(): void {
    try {
      if (!this.inputXaml.trim()) {
        this.isValidXaml = false;
        this.validationMessage = 'XAML cannot be empty';
        return;
      }
      
      // Basic XAML validation
      const parser = new DOMParser();
      const xmlDoc = parser.parseFromString(this.inputXaml, 'text/xml');
      
      // Check for parsing errors
      const parseError = xmlDoc.querySelector('parsererror');
      if (parseError) {
        this.isValidXaml = false;
        this.validationMessage = 'Invalid XML format';
        return;
      }
      
      // Check for ContentPage root element
      const contentPage = xmlDoc.querySelector('ContentPage');
      if (!contentPage) {
        this.isValidXaml = false;
        this.validationMessage = 'Root element must be ContentPage';
        return;
      }
      
      // Check for layout container
      const layout = contentPage.querySelector('AbsoluteLayout, StackLayout, Grid');
      if (!layout) {
        this.isValidXaml = false;
        this.validationMessage = 'ContentPage must contain a layout container';
        return;
      }
      
      this.isValidXaml = true;
      this.validationMessage = 'XAML is valid and ready to apply';
      
    } catch (error) {
      this.isValidXaml = false;
      this.validationMessage = 'XAML validation error';
    }
  }
  
  async applyXaml(): Promise<void> {
    if (!this.isValidXaml) {
      return;
    }
    
    this.isApplying = true;
    
    try {
      // Parse the XAML and convert it to element structure
      const newRootElement = await this.parseXamlToElements(this.inputXaml);
      
      if (newRootElement) {
        // Replace the current element tree with the new one
        this.elementService.replaceRootElement(newRootElement);
        this.validationMessage = 'XAML applied successfully!';
        
        // Switch back to generated view to show the result
        setTimeout(() => {
          this.editMode = 'generated';
        }, 1000);
      } else {
        this.validationMessage = 'Failed to parse XAML elements';
        this.isValidXaml = false;
      }
      
    } catch (error) {
      console.error('Error applying XAML:', error);
      this.validationMessage = 'Error applying XAML changes';
      this.isValidXaml = false;
    } finally {
      this.isApplying = false;
    }
  }
  
  private async parseXamlToElements(xaml: string): Promise<MauiElement | null> {
    try {
      const parser = new DOMParser();
      const xmlDoc = parser.parseFromString(xaml, 'text/xml');
      
      const contentPage = xmlDoc.querySelector('ContentPage');
      if (!contentPage) return null;
      
      // Find the root layout element
      const rootLayoutElement = contentPage.firstElementChild;
      if (!rootLayoutElement) return null;
      
      // Convert XML element to MauiElement recursively
      return this.convertXmlToMauiElement(rootLayoutElement, null);
      
    } catch (error) {
      console.error('Error parsing XAML:', error);
      return null;
    }
  }
  
  private convertXmlToMauiElement(xmlElement: Element, parent: MauiElement | null): MauiElement {
    const tagName = xmlElement.tagName;
    const name = xmlElement.getAttribute('x:Name') || `${tagName}1`;
    
    // Create the basic element structure
    const mauiElement: MauiElement = {
      id: this.generateId(),
      type: tagName as any,
      name: name,
      properties: this.extractProperties(xmlElement),
      children: [],
      parent: parent || undefined
    };
    
    // Process child elements
    for (const child of Array.from(xmlElement.children)) {
      const childElement = this.convertXmlToMauiElement(child, mauiElement);
      mauiElement.children.push(childElement);
    }
    
    return mauiElement;
  }
  
  private extractProperties(xmlElement: Element): any {
    const properties: any = {};
    
    // Extract common properties from attributes
    const attributes = xmlElement.attributes;
    for (let i = 0; i < attributes.length; i++) {
      const attr = attributes[i];
      if (attr.name.startsWith('x:')) continue; // Skip XML namespace attributes
      
      const value = attr.value;
      switch (attr.name) {
        case 'WidthRequest':
          properties.width = parseInt(value) || 100;
          break;
        case 'HeightRequest':
          properties.height = parseInt(value) || 30;
          break;
        case 'Text':
          properties.text = value;
          break;
        case 'BackgroundColor':
          properties.backgroundColor = value;
          break;
        case 'TextColor':
          properties.textColor = value;
          break;
        case 'FontSize':
          properties.fontSize = parseInt(value) || 14;
          break;
        // Add more property mappings as needed
      }
    }
    
    // Default properties
    if (!properties.x) properties.x = 50;
    if (!properties.y) properties.y = 50;
    if (!properties.width) properties.width = 100;
    if (!properties.height) properties.height = 30;
    if (!properties.isVisible) properties.isVisible = true;
    if (!properties.isEnabled) properties.isEnabled = true;
    
    return properties;
  }
  
  private generateId(): string {
    return 'element_' + Math.random().toString(36).substr(2, 9);
  }

  copyToClipboard() {
    const xamlToUse = this.editMode === 'input' ? this.inputXaml : '';
    
    if (this.editMode === 'generated') {
      this.xamlContent$.subscribe(xaml => {
        navigator.clipboard.writeText(xaml).then(() => {
          console.log('XAML copied to clipboard');
        });
      });
    } else {
      navigator.clipboard.writeText(xamlToUse).then(() => {
        console.log('XAML copied to clipboard');
      });
    }
  }

  downloadXaml() {
    const xamlToUse = this.editMode === 'input' ? this.inputXaml : '';
    
    if (this.editMode === 'generated') {
      this.xamlContent$.subscribe(xaml => {
        this.downloadXamlFile(xaml);
      });
    } else {
      this.downloadXamlFile(xamlToUse);
    }
  }
  
  private downloadXamlFile(xaml: string) {
    const blob = new Blob([xaml], { type: 'text/xml' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'MainPage.xaml';
    link.click();
    window.URL.revokeObjectURL(url);
  }
}
