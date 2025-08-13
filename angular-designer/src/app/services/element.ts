import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { MauiElement, ElementType, ElementProperties, GridDefinition, GridRowDefinition, GridColumnDefinition, GridLength, GridLengthType } from '../models/maui-element';

@Injectable({
  providedIn: 'root'
})
export class ElementService {
  private rootElement: MauiElement;
  private selectedElement: MauiElement | null = null;
  private elementCounter = 0;

  private selectedElementSubject = new BehaviorSubject<MauiElement | null>(null);
  private elementsSubject = new BehaviorSubject<MauiElement>(this.createRootElement());

  selectedElement$ = this.selectedElementSubject.asObservable();
  elements$ = this.elementsSubject.asObservable();

  constructor() {
    this.rootElement = this.createRootElement();
    this.elementsSubject.next(this.rootElement);
  }

  private createRootElement(): MauiElement {
    return {
      id: 'root',
      type: ElementType.AbsoluteLayout,
      name: 'Root Layout',
      properties: {
        backgroundColor: '#ffffff',
        width: 800,
        height: 600
      },
      children: []
    };
  }

  createElement(type: ElementType, properties?: Partial<ElementProperties>): MauiElement {
    this.elementCounter++;
    const defaultProperties = this.getDefaultProperties(type);
    
    return {
      id: `element_${this.elementCounter}`,
      type: type,
      name: `${type}${this.elementCounter}`,
      properties: { ...defaultProperties, ...properties },
      children: []
    };
  }

  private getDefaultProperties(type: ElementType): ElementProperties {
    const common = {
      x: 50,
      y: 50,
      width: 100,
      height: 30,
      isVisible: true,
      isEnabled: true
    };

    switch (type) {
      case ElementType.Label:
        return {
          ...common,
          text: 'Label',
          textColor: '#000000',
          fontSize: 14
        };
      case ElementType.Button:
        return {
          ...common,
          text: 'Button',
          backgroundColor: '#007acc',
          textColor: '#ffffff',
          fontSize: 14
        };
      case ElementType.Entry:
        return {
          ...common,
          text: '',
          backgroundColor: '#ffffff',
          textColor: '#000000'
        };
      case ElementType.Editor:
        return {
          ...common,
          height: 100,
          text: '',
          backgroundColor: '#ffffff',
          textColor: '#000000'
        };
      case ElementType.Image:
        return {
          ...common,
          width: 100,
          height: 100
        };
      case ElementType.StackLayout:
        return {
          ...common,
          width: 200,
          height: 200,
          orientation: 'Vertical' as any,
          spacing: 5
        };
      case ElementType.Grid:
        return {
          ...common,
          width: 200,
          height: 200
        };
      case ElementType.AbsoluteLayout:
        return {
          ...common,
          width: 200,
          height: 200
        };
      case ElementType.Frame:
        return {
          ...common,
          backgroundColor: '#f0f0f0',
          width: 150,
          height: 100
        };
      case ElementType.ScrollView:
        return {
          ...common,
          width: 200,
          height: 200
        };
      default:
        return common;
    }
  }

  addElement(element: MauiElement, parent?: MauiElement): void {
    const targetParent = parent || this.rootElement;
    element.parent = targetParent;
    targetParent.children.push(element);
    this.elementsSubject.next(this.rootElement);
  }

  removeElement(element: MauiElement): void {
    if (element.parent) {
      const index = element.parent.children.indexOf(element);
      if (index > -1) {
        element.parent.children.splice(index, 1);
      }
    }
    
    if (this.selectedElement === element) {
      this.selectElement(null);
    }
    
    this.elementsSubject.next(this.rootElement);
  }

  selectElement(element: MauiElement | null): void {
    this.selectedElement = element;
    this.selectedElementSubject.next(element);
  }

  updateElementProperties(element: MauiElement, properties: Partial<ElementProperties>): void {
    element.properties = { ...element.properties, ...properties };
    this.elementsSubject.next(this.rootElement);
  }

  findElementById(id: string, root?: MauiElement): MauiElement | null {
    const searchRoot = root || this.rootElement;
    
    if (searchRoot.id === id) {
      return searchRoot;
    }
    
    for (const child of searchRoot.children) {
      const found = this.findElementById(id, child);
      if (found) {
        return found;
      }
    }
    
    return null;
  }

  moveElement(element: MauiElement, newParent: MauiElement, x: number, y: number): void {
    // Remove from current parent
    if (element.parent) {
      const index = element.parent.children.indexOf(element);
      if (index > -1) {
        element.parent.children.splice(index, 1);
      }
    }

    // Add to new parent
    element.parent = newParent;
    element.properties.x = x;
    element.properties.y = y;
    newParent.children.push(element);
    
    this.elementsSubject.next(this.rootElement);
  }

  getRootElement(): MauiElement {
    return this.rootElement;
  }

  getSelectedElement(): MauiElement | null {
    return this.selectedElement;
  }
  
  replaceRootElement(newRootElement: MauiElement): void {
    this.rootElement = newRootElement;
    this.selectedElement = null;
    this.selectedElementSubject.next(null);
    this.elementsSubject.next(this.rootElement);
  }

  // Grid specific methods
  addGridRow(gridElement: MauiElement): void {
    if (gridElement.type === ElementType.Grid) {
      // Implementation for adding grid row
      this.elementsSubject.next(this.rootElement);
    }
  }

  addGridColumn(gridElement: MauiElement): void {
    if (gridElement.type === ElementType.Grid) {
      // Implementation for adding grid column
      this.elementsSubject.next(this.rootElement);
    }
  }

  removeGridRow(gridElement: MauiElement, rowIndex: number): void {
    if (gridElement.type === ElementType.Grid) {
      // Implementation for removing grid row
      this.elementsSubject.next(this.rootElement);
    }
  }

  removeGridColumn(gridElement: MauiElement, columnIndex: number): void {
    if (gridElement.type === ElementType.Grid) {
      // Implementation for removing grid column
      this.elementsSubject.next(this.rootElement);
    }
  }
}
