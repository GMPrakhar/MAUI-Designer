import { Injectable } from '@angular/core';
import { MauiElement, ElementType } from '../models/maui-element';

export interface LayoutInfo {
  canHaveChildren: boolean;
  supportsDragDrop: boolean;
  supportsAbsolutePositioning: boolean;
  supportsGridPositioning: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class LayoutDesignerService {

  constructor() { }

  getLayoutInfo(elementType: ElementType): LayoutInfo {
    switch (elementType) {
      case ElementType.AbsoluteLayout:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: true,
          supportsGridPositioning: false
        };
      
      case ElementType.Grid:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: true
        };
      
      case ElementType.StackLayout:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: false
        };
      
      case ElementType.Frame:
      case ElementType.ScrollView:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: false
        };
      
      default:
        return {
          canHaveChildren: false,
          supportsDragDrop: false,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: false
        };
    }
  }

  calculateDropPosition(element: MauiElement, event: MouseEvent, containerElement: HTMLElement): { x: number, y: number } {
    const rect = containerElement.getBoundingClientRect();
    const x = event.clientX - rect.left;
    const y = event.clientY - rect.top;
    
    const layoutInfo = this.getLayoutInfo(element.type);
    
    if (layoutInfo.supportsAbsolutePositioning) {
      return { x, y };
    } else if (layoutInfo.supportsGridPositioning) {
      // For grid layouts, calculate grid cell
      return this.calculateGridPosition(element, x, y, containerElement);
    } else {
      // For stack layouts, position is managed by the layout
      return { x: 0, y: 0 };
    }
  }

  private calculateGridPosition(gridElement: MauiElement, x: number, y: number, containerElement: HTMLElement): { x: number, y: number } {
    // Simple implementation - assumes 2x2 grid for now
    // This should be enhanced to read actual grid definitions
    const rect = containerElement.getBoundingClientRect();
    const cellWidth = rect.width / 2;
    const cellHeight = rect.height / 2;
    
    const column = Math.floor(x / cellWidth);
    const row = Math.floor(y / cellHeight);
    
    return { x: column, y: row }; // These represent grid coordinates, not pixel coordinates
  }

  getChildLayoutProperties(parent: MauiElement, child: MauiElement, position: { x: number, y: number }): Partial<MauiElement['properties']> {
    const layoutInfo = this.getLayoutInfo(parent.type);
    
    if (layoutInfo.supportsAbsolutePositioning) {
      return {
        x: position.x,
        y: position.y
      };
    } else if (layoutInfo.supportsGridPositioning) {
      return {
        column: position.x,
        row: position.y
      };
    } else {
      // For stack layouts, no special positioning needed
      return {};
    }
  }

  canDropOn(targetElement: MauiElement, droppedElement?: MauiElement): boolean {
    const layoutInfo = this.getLayoutInfo(targetElement.type);
    
    if (!layoutInfo.canHaveChildren) {
      return false;
    }
    
    if (droppedElement) {
      // Prevent dropping an element on itself or its descendants
      return !this.isDescendant(targetElement, droppedElement);
    }
    
    return true;
  }

  private isDescendant(potential: MauiElement, ancestor: MauiElement): boolean {
    let current = potential.parent;
    while (current) {
      if (current === ancestor) {
        return true;
      }
      current = current.parent;
    }
    return false;
  }

  getVisualHints(element: MauiElement): { showGrid: boolean, showDropZones: boolean } {
    const layoutInfo = this.getLayoutInfo(element.type);
    
    return {
      showGrid: layoutInfo.supportsGridPositioning,
      showDropZones: layoutInfo.supportsDragDrop
    };
  }
}
