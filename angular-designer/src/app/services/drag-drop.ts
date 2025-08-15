import { Injectable } from '@angular/core';
import { CdkDragDrop, CdkDragEnd, CdkDragStart, transferArrayItem } from '@angular/cdk/drag-drop';
import { ElementService } from './element';
import { LayoutDesignerService } from './layout-designer';
import { MauiElement, ElementType } from '../models/maui-element';
import { BehaviorSubject } from 'rxjs';

export interface DragData {
  elementType?: ElementType;
  element?: MauiElement;
  isFromToolbox: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class DragDropService {
  private currentDragData: DragData | null = null;
  private dragPreview = new BehaviorSubject<{ x: number, y: number, visible: boolean }>({ x: 0, y: 0, visible: false });
  
  dragPreview$ = this.dragPreview.asObservable();

  constructor(
    private elementService: ElementService,
    private layoutDesigner: LayoutDesignerService
  ) { }

  startDrag(data: DragData): void {
    this.currentDragData = data;
  }

  endDrag(): void {
    this.currentDragData = null;
    this.hideDragPreview();
  }

  getDragData(): DragData | null {
    return this.currentDragData;
  }

  handleToolboxDrop(event: CdkDragDrop<any>, x: number, y: number, targetParent: MauiElement): void {
    if (this.currentDragData?.isFromToolbox && this.currentDragData.elementType) {
      // Calculate position relative to the target parent
      const position = this.calculateRelativePosition(x, y, targetParent);
      
      // Get layout-specific properties
      const layoutProperties = this.layoutDesigner.getChildLayoutProperties(
        targetParent, 
        { type: this.currentDragData.elementType } as MauiElement, 
        position
      );

      const newElement = this.elementService.createElement(
        this.currentDragData.elementType,
        layoutProperties
      );
      
      this.elementService.addElement(newElement, targetParent);
      this.elementService.selectElement(newElement);
    }
  }

  handleElementMove(element: MauiElement, x: number, y: number, targetParent: MauiElement): void {
    // Calculate position relative to the target parent
    const position = this.calculateRelativePosition(x, y, targetParent);
    
    // Get layout-specific properties
    const layoutProperties = this.layoutDesigner.getChildLayoutProperties(
      targetParent, 
      element, 
      position
    );

    // Only update x,y coordinates if the parent supports absolute positioning
    const layoutInfo = this.layoutDesigner.getLayoutInfo(targetParent.type);
    
    if (layoutInfo.supportsAbsolutePositioning) {
      this.elementService.moveElement(element, targetParent, position.x, position.y);
    } else {
      // For non-absolute layouts, don't update x,y coordinates
      // Instead, just move the element to the new parent
      this.moveElementToParent(element, targetParent, layoutProperties);
    }
  }

  showDragPreview(x: number, y: number): void {
    this.dragPreview.next({ x, y, visible: true });
  }

  hideDragPreview(): void {
    this.dragPreview.next({ x: 0, y: 0, visible: false });
  }

  updateDragPreview(x: number, y: number): void {
    this.dragPreview.next({ x, y, visible: true });
  }

  canDropOn(target: MauiElement, draggedElement?: MauiElement): boolean {
    // Prevent dropping an element on itself or its children
    if (draggedElement) {
      return !this.isChildOf(target, draggedElement) && target !== draggedElement;
    }
    
    // Check if target can accept children
    return this.canHaveChildren(target.type);
  }

  private isChildOf(potential: MauiElement, parent: MauiElement): boolean {
    let current = potential.parent;
    while (current) {
      if (current === parent) {
        return true;
      }
      current = current.parent;
    }
    return false;
  }

  private canHaveChildren(elementType: ElementType): boolean {
    switch (elementType) {
      case ElementType.StackLayout:
      case ElementType.Grid:
      case ElementType.AbsoluteLayout:
      case ElementType.Frame:
      case ElementType.ScrollView:
        return true;
      default:
        return false;
    }
  }

  /**
   * Calculate position relative to the target parent element
   */
  private calculateRelativePosition(x: number, y: number, targetParent: MauiElement): { x: number, y: number } {
    const parentProps = targetParent.properties;
    const parentX = parentProps.x || 0;
    const parentY = parentProps.y || 0;
    
    return {
      x: x - parentX,
      y: y - parentY
    };
  }

  /**
   * Move element to a new parent with layout-specific properties
   */
  private moveElementToParent(element: MauiElement, newParent: MauiElement, layoutProperties: any): void {
    // Remove from current parent
    if (element.parent) {
      const index = element.parent.children.indexOf(element);
      if (index > -1) {
        element.parent.children.splice(index, 1);
      }
    }

    // Update element properties with layout-specific properties
    element.properties = { ...element.properties, ...layoutProperties };
    
    // Add to new parent
    element.parent = newParent;
    newParent.children.push(element);
    
    // Notify element service
    this.elementService.setRootElement(this.elementService.getRootElement());
  }
}
