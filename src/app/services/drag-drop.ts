import { Injectable } from '@angular/core';
import { CdkDragDrop, CdkDragEnd, CdkDragStart, transferArrayItem } from '@angular/cdk/drag-drop';
import { ElementService } from './element';
import { CustomControlRegistryService } from './custom-control-registry';
import { LayoutDesignerService } from './layout-designer';
import { MauiElement, ElementType, ElementProperties } from '../models/maui-element';
import { BehaviorSubject, min } from 'rxjs';

export interface DragData {
  elementType?: ElementType;
  element?: MauiElement;
  isFromToolbox: boolean;
}

/** MIME type used when dragging a control out of the toolbox onto the canvas. */
export const TOOLBOX_DRAG_MIME = 'application/x-maui-element-type';

@Injectable({
  providedIn: 'root'
})
export class DragDropService {
  private currentDragData: DragData | null = null;
  private dragPreview = new BehaviorSubject<{ x: number, y: number, visible: boolean }>({ x: 0, y: 0, visible: false });
  
  dragPreview$ = this.dragPreview.asObservable();

  constructor(
    private elementService: ElementService,
    private layoutDesigner: LayoutDesignerService,
    private registry: CustomControlRegistryService
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

  /**
   * Creates a new element of the given type inside the layout under the drop point.
   * Used by the native drag from the toolbox onto the canvas.
   */
  createElementAtPosition(
    elementType: ElementType,
    dropX: number,
    dropY: number,
    canvasElement: HTMLElement,
    properties?: Partial<ElementProperties>
  ): MauiElement {
    const targetLayout = this.findLayoutAtPosition(dropX, dropY, canvasElement) || this.elementService.getRootElement();

    const newElement = this.elementService.createElement(elementType, properties);
    this.elementService.addElement(newElement, targetLayout);

    const position = this.layoutDesigner.getChildLayoutProperties(
      targetLayout,
      newElement,
      this.resolveLocalPosition(targetLayout, dropX, dropY, canvasElement)
    );
    this.elementService.updateElementProperties(newElement, position, { recordHistory: false });
    this.elementService.selectElement(newElement);
    return newElement;
  }

  private resolveLocalPosition(targetLayout: MauiElement, dropX: number, dropY: number, canvasElement: HTMLElement): { x: number, y: number } {
    const layoutInfo = this.layoutDesigner.getLayoutInfo(targetLayout.type);
    const dom = targetLayout.domElement;

    if (!dom || targetLayout.id === 'root') {
      return { x: dropX, y: dropY };
    }

    const rect = dom.getBoundingClientRect();
    const canvasRect = canvasElement.getBoundingClientRect();
    const localX = dropX - (rect.left - canvasRect.left);
    const localY = dropY - (rect.top - canvasRect.top);

    if (layoutInfo.supportsGridPositioning) {
      const cell = this.layoutDesigner.getGridCellAtPosition(targetLayout, localX, localY, dom);
      return cell ? { x: cell.column, y: cell.row } : { x: 0, y: 0 };
    }

    return { x: localX, y: localY };
  }

  handleElementMove(element: MauiElement, x: number, y: number, targetParent: MauiElement): void {
    // A move is a single undo step even though it touches several properties
    this.elementService.runAsSingleChange(() => this.applyElementMove(element, x, y, targetParent));
  }

  private applyElementMove(element: MauiElement, x: number, y: number, targetParent: MauiElement): void {
    // Get layout info for the target parent
    const layoutInfo = this.layoutDesigner.getLayoutInfo(targetParent.type);
    
    // Calculate appropriate position based on layout type
    let domElement = targetParent.id === 'root' ? null! : targetParent.domElement!;
    const position = this.layoutDesigner.calculateDropPosition(targetParent, { clientX: x, clientY: y } as MouseEvent, domElement!);
    
    // Get layout-specific properties for the child element
    const layoutProperties = this.layoutDesigner.getChildLayoutProperties(targetParent, element, position);
    
    // Update element properties based on layout type
    this.elementService.updateElementProperties(element, layoutProperties);
    
    // For stack layouts, calculate insertion index
    let insertionIndex: number | undefined;
    if (targetParent.type === ElementType.StackLayout || targetParent.type === ElementType.VerticalStackLayout) {
      // For stack layouts, we need the container element to calculate insertion index
      // This is a simplified approach - in practice you'd pass the actual container element
      insertionIndex = targetParent.children.length; // Append to end for now
    }
    
    // Move element to new parent if different
    if (element.parent !== targetParent) {
      this.elementService.moveElement(element, targetParent, 0, 0, insertionIndex);
    } else if(element.parent.type === ElementType.AbsoluteLayout) {
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
    return this.canElementHaveChildren(target);
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
  findLayoutAtPosition(x: number, y: number, canvasElement: HTMLElement): MauiElement | null {
    // Get all layout elements from the DOM
    const layoutElements = canvasElement.querySelectorAll('.layout-element');
    let layoutElementsAtPosition = [];
    let deepestLayout: MauiElement | null = null;

    for (const element of Array.from(layoutElements)) {
      const rect = element.getBoundingClientRect();
      const canvasRect = canvasElement.getBoundingClientRect();
      
      // Convert absolute coordinates to relative coordinates
      const relativeX = x - (rect.left - canvasRect.left);
      const relativeY = y - (rect.top - canvasRect.top);
      
      // Check if point is within this element
      if (relativeX >= 0 && relativeX <= rect.width && relativeY >= 0 && relativeY <= rect.height) {
        layoutElementsAtPosition.push(element);
      }
    }

    if(layoutElementsAtPosition.length === 0) {
      return this.elementService.getRootElement();
    }

    // Find the deepest layout element that can contain children
    let minNesting = 1000;
    let finalElement;
    for (const elem of layoutElementsAtPosition) {
      const numberOfNestings = elem.querySelectorAll('.layout-element').length;
      if(numberOfNestings < minNesting) {
        minNesting = numberOfNestings;
        finalElement = elem;
      }
    }

    deepestLayout = this.getMauiElementFromDOMElement(finalElement!);

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
      const found = this.elementService.findElementById(elementId);
      if (found) {
        // Keep the DOM reference fresh so position maths stay accurate
        found.domElement = domElement as HTMLElement;
      }
      return found;
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

  /** Custom controls declare in their manifest whether they accept children. */
  canElementHaveChildren(element: MauiElement): boolean {
    if (element.type === ElementType.Custom) {
      return this.registry.findForElement(element)?.definition.canHaveChildren ?? false;
    }
    return this.canHaveChildren(element.type);
  }

  canHaveChildren(elementType: ElementType): boolean {
    switch (elementType) {
      case ElementType.StackLayout:
      case ElementType.VerticalStackLayout:
      case ElementType.Grid:
      case ElementType.AbsoluteLayout:
      case ElementType.Frame:
      case ElementType.Border:
      case ElementType.ScrollView:
      case ElementType.CollectionView:
        return true;
      default:
        return false;
    }
  }
}
