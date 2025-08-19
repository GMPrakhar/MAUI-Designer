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
      const newElement = this.elementService.createElement(
        this.currentDragData.elementType,
        { x: x, y: y }
      );
      this.elementService.addElement(newElement, targetParent);
      this.elementService.selectElement(newElement);
    }
  }

  handleElementMove(element: MauiElement, x: number, y: number, targetParent: MauiElement): void {
    // Get layout info for the target parent
    const layoutInfo = this.layoutDesigner.getLayoutInfo(targetParent.type);
    
    // Calculate appropriate position based on layout type
    const position = this.layoutDesigner.calculateDropPosition(targetParent, { clientX: x, clientY: y } as MouseEvent, null!);
    
    // Get layout-specific properties for the child element
    const layoutProperties = this.layoutDesigner.getChildLayoutProperties(targetParent, element, position);
    
    // Update element properties based on layout type
    this.elementService.updateElementProperties(element, layoutProperties);
    
    // For stack layouts, calculate insertion index
    let insertionIndex: number | undefined;
    if (targetParent.type === ElementType.StackLayout) {
      // For stack layouts, we need the container element to calculate insertion index
      // This is a simplified approach - in practice you'd pass the actual container element
      insertionIndex = targetParent.children.length; // Append to end for now
    }
    
    // Move element to new parent if different
    if (element.parent !== targetParent) {
      this.elementService.moveElement(element, targetParent, position.x, position.y, insertionIndex);
    } else {
      // Just update position if same parent
      this.elementService.updateElementProperties(element, { x: position.x, y: position.y });
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

  /**
   * Handles dropping an element at a specific position on the canvas
   */
  handleCanvasDrop(draggedElement: MauiElement, dropX: number, dropY: number, canvasElement: HTMLElement): void {
    // Find the layout element at the drop position
    const targetLayout = this.findLayoutAtPosition(dropX, dropY, canvasElement);
    
    if (targetLayout && this.canDropOn(targetLayout, draggedElement)) {
      this.handleElementMove(draggedElement, dropX, dropY, targetLayout);
    }
  }

  /**
   * Finds the layout element at a specific position on the canvas
   */
  private findLayoutAtPosition(x: number, y: number, canvasElement: HTMLElement): MauiElement | null {
    // Get all layout elements from the DOM
    const layoutElements = canvasElement.querySelectorAll('.layout-element');
    let deepestLayout: MauiElement | null = null;
    let maxZIndex = -1;

    for (const element of Array.from(layoutElements)) {
      const rect = element.getBoundingClientRect();
      const canvasRect = canvasElement.getBoundingClientRect();
      
      // Convert absolute coordinates to relative coordinates
      const relativeX = x - (rect.left - canvasRect.left);
      const relativeY = y - (rect.top - canvasRect.top);
      
      // Check if point is within this element
      if (relativeX >= 0 && relativeX <= rect.width && relativeY >= 0 && relativeY <= rect.height) {
        const zIndex = parseInt(window.getComputedStyle(element).zIndex) || 0;
        
        // Get the MauiElement from the DOM element
        const mauiElement = this.getMauiElementFromDOMElement(element);
        
        if (mauiElement && zIndex >= maxZIndex) {
          maxZIndex = zIndex;
          deepestLayout = mauiElement;
        }
      }
    }

    // If no specific layout found, return root element
    return deepestLayout || this.elementService.getRootElement();
  }

  /**
   * Helper to get MauiElement from DOM element (would need proper implementation)
   */
  private getMauiElementFromDOMElement(domElement: Element): MauiElement | null {
    // This is a simplified implementation - in practice you'd need to store
    // element references or use a more sophisticated mapping
    const elementId = domElement.getAttribute('data-element-id');
    if (elementId) {
      return this.elementService.findElementById(elementId);
    }
    return null;
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
}
