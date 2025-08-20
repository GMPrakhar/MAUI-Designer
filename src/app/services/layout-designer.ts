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
      case ElementType.VerticalStackLayout:
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

  calculateDropPosition(parentElement: MauiElement, dropX: number, dropY: number, containerElement: HTMLElement | null): { x: number, y: number } {
    if (!containerElement) {
      // Fallback for cases where container element is not available
      return { x: dropX, y: dropY };
    }
    
    const layoutInfo = this.getLayoutInfo(parentElement.type);
    
    if (layoutInfo.supportsAbsolutePositioning) {
      return { x: dropX, y: dropY };
    } else if (layoutInfo.supportsGridPositioning) {
      // For grid layouts, calculate grid cell
      return this.calculateGridPosition(parentElement, dropX, dropY, containerElement);
    } else {
      // For stack layouts, position is managed by the layout
      return { x: 0, y: 0 };
    }
  }

  private calculateGridPosition(gridElement: MauiElement, x: number, y: number, containerElement: HTMLElement): { x: number, y: number } {
    // Get grid dimensions from element properties or use defaults
    const gridDefinition = gridElement.properties.gridDefinition || {
      rows: [{ height: { value: 1, type: 'Star' } }, { height: { value: 1, type: 'Star' } }],
      columns: [{ width: { value: 1, type: 'Star' } }, { width: { value: 1, type: 'Star' } }]
    };
    
    const rect = containerElement.getBoundingClientRect();
    const rows = gridDefinition.rows.length;
    const columns = gridDefinition.columns.length;
    
    const cellWidth = rect.width / columns;
    const cellHeight = rect.height / rows;
    
    const column = Math.min(Math.floor(x / cellWidth), columns - 1);
    const row = Math.min(Math.floor(y / cellHeight), rows - 1);
    
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
        row: position.y,
        x: 0, // Reset absolute positioning in grid
        y: 0
      };
    } else {
      // For stack layouts, clear absolute positioning
      return {
        x: 0,
        y: 0
      };
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

  /**
   * Calculate which grid cell would be highlighted during hover
   */
  getGridCellAtPosition(gridElement: MauiElement, x: number, y: number, containerElement: HTMLElement): { row: number, column: number } | null {
    if (!this.getLayoutInfo(gridElement.type).supportsGridPositioning) {
      return null;
    }

    const position = this.calculateGridPosition(gridElement, x, y, containerElement);
    return { row: position.y, column: position.x };
  }

  /**
   * For stack layouts, determine insertion index based on position
   */
  getStackInsertionIndex(stackElement: MauiElement, x: number, y: number, containerElement: HTMLElement): number {
    const layoutInfo = this.getLayoutInfo(stackElement.type);
    if (layoutInfo.supportsAbsolutePositioning || layoutInfo.supportsGridPositioning) {
      return stackElement.children.length; // Append at end for non-stack layouts
    }

    // VerticalStackLayout is always vertical
    const isVertical = stackElement.type === ElementType.VerticalStackLayout || 
                      stackElement.properties.orientation !== 'Horizontal';
    const rect = containerElement.getBoundingClientRect();
    
    let insertionIndex = 0;
    
    if (isVertical) {
      // For vertical stack, use Y position
      const relativeY = y / rect.height;
      insertionIndex = Math.floor(relativeY * (stackElement.children.length + 1));
    } else {
      // For horizontal stack, use X position
      const relativeX = x / rect.width;
      insertionIndex = Math.floor(relativeX * (stackElement.children.length + 1));
    }
    
    return Math.min(insertionIndex, stackElement.children.length);
  }
}
