import { MauiElement, ElementType } from '../models/maui-element';
import { LayoutDesignerService } from './layout-designer';
import { ElementService } from './element';

/**
 * Interface for layout-specific drop handling behavior
 */
export interface ILayoutDropHandler {
  /**
   * Handles dropping an element onto this layout type
   * @param droppedElement The element being dropped
   * @param targetLayout The layout element being dropped onto
   * @param dropX X coordinate of the drop position relative to the layout
   * @param dropY Y coordinate of the drop position relative to the layout
   * @param containerElement The DOM element representing the layout container
   */
  handleDrop(
    droppedElement: MauiElement, 
    targetLayout: MauiElement, 
    dropX: number, 
    dropY: number, 
    containerElement: HTMLElement | null
  ): void;

  /**
   * Determines if this handler can handle the given layout type
   */
  canHandle(layoutType: ElementType): boolean;
}

/**
 * Drop handler for AbsoluteLayout - elements can be positioned anywhere
 */
export class AbsoluteLayoutDropHandler implements ILayoutDropHandler {
  constructor(
    private elementService: ElementService,
    private layoutDesigner: LayoutDesignerService
  ) {}

  canHandle(layoutType: ElementType): boolean {
    return layoutType === ElementType.AbsoluteLayout;
  }

  handleDrop(
    droppedElement: MauiElement, 
    targetLayout: MauiElement, 
    dropX: number, 
    dropY: number, 
    containerElement: HTMLElement | null
  ): void {
    // For absolute layout, use the exact drop position
    const position = this.layoutDesigner.calculateDropPosition(
      targetLayout, 
      dropX, 
      dropY, 
      containerElement
    );

    // Update element properties for absolute positioning
    const layoutProperties = this.layoutDesigner.getChildLayoutProperties(targetLayout, droppedElement, position);
    this.elementService.updateElementProperties(droppedElement, layoutProperties);

    // Move element to new parent if different
    if (droppedElement.parent !== targetLayout) {
      this.elementService.moveElement(droppedElement, targetLayout, position.x, position.y);
    } else {
      // Just update position if same parent
      this.elementService.updateElementProperties(droppedElement, { x: position.x, y: position.y });
    }
  }
}

/**
 * Drop handler for Grid layout - elements are positioned in grid cells
 */
export class GridLayoutDropHandler implements ILayoutDropHandler {
  constructor(
    private elementService: ElementService,
    private layoutDesigner: LayoutDesignerService
  ) {}

  canHandle(layoutType: ElementType): boolean {
    return layoutType === ElementType.Grid;
  }

  handleDrop(
    droppedElement: MauiElement, 
    targetLayout: MauiElement, 
    dropX: number, 
    dropY: number, 
    containerElement: HTMLElement | null
  ): void {
    // Calculate which grid cell the element was dropped into
    const position = this.layoutDesigner.calculateDropPosition(
      targetLayout, 
      dropX, 
      dropY, 
      containerElement
    );

    // Get layout-specific properties for grid positioning
    const layoutProperties = this.layoutDesigner.getChildLayoutProperties(targetLayout, droppedElement, position);
    this.elementService.updateElementProperties(droppedElement, layoutProperties);

    // Move element to new parent if different
    if (droppedElement.parent !== targetLayout) {
      this.elementService.moveElement(droppedElement, targetLayout, 0, 0); // Grid uses row/column, not x/y
    }
  }
}

/**
 * Drop handler for Stack layouts (StackLayout and VerticalStackLayout)
 */
export class StackLayoutDropHandler implements ILayoutDropHandler {
  constructor(
    private elementService: ElementService,
    private layoutDesigner: LayoutDesignerService
  ) {}

  canHandle(layoutType: ElementType): boolean {
    return layoutType === ElementType.StackLayout || layoutType === ElementType.VerticalStackLayout;
  }

  handleDrop(
    droppedElement: MauiElement, 
    targetLayout: MauiElement, 
    dropX: number, 
    dropY: number, 
    containerElement: HTMLElement | null
  ): void {
    // For stack layouts, determine the insertion index based on drop position
    const insertionIndex = containerElement 
      ? this.layoutDesigner.getStackInsertionIndex(targetLayout, dropX, dropY, containerElement)
      : targetLayout.children.length; // Append at end if no container element

    // Clear absolute positioning properties for stack layout children
    const layoutProperties = this.layoutDesigner.getChildLayoutProperties(targetLayout, droppedElement, { x: 0, y: 0 });
    this.elementService.updateElementProperties(droppedElement, layoutProperties);

    // Move element to new parent if different, with proper insertion index
    if (droppedElement.parent !== targetLayout) {
      this.elementService.moveElement(droppedElement, targetLayout, 0, 0, insertionIndex);
    } else {
      // If same parent, we need to handle reordering within the stack
      this.reorderWithinStack(droppedElement, targetLayout, insertionIndex);
    }
  }

  private reorderWithinStack(element: MauiElement, parent: MauiElement, newIndex: number): void {
    // Remove element from current position
    const currentIndex = parent.children.indexOf(element);
    if (currentIndex > -1) {
      parent.children.splice(currentIndex, 1);
      
      // Adjust insertion index if we removed an element before the target position
      const adjustedIndex = currentIndex < newIndex ? newIndex - 1 : newIndex;
      
      // Insert at new position
      parent.children.splice(adjustedIndex, 0, element);
      
      // Notify of change
      this.elementService.moveElement(element, parent, 0, 0, adjustedIndex);
    }
  }
}

/**
 * Factory for creating appropriate drop handlers
 */
export class LayoutDropHandlerFactory {
  private handlers: ILayoutDropHandler[] = [];

  constructor(
    elementService: ElementService,
    layoutDesigner: LayoutDesignerService
  ) {
    this.handlers = [
      new AbsoluteLayoutDropHandler(elementService, layoutDesigner),
      new GridLayoutDropHandler(elementService, layoutDesigner),
      new StackLayoutDropHandler(elementService, layoutDesigner)
    ];
  }

  getHandler(layoutType: ElementType): ILayoutDropHandler | null {
    return this.handlers.find(handler => handler.canHandle(layoutType)) || null;
  }
}