import { Component, OnInit, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDropList, CdkDragDrop, CdkDragEnd, CdkDragMove } from '@angular/cdk/drag-drop';
import { ElementService } from '../../services/element';
import { DragDropService, TOOLBOX_DRAG_MIME } from '../../services/drag-drop';
import { LayoutDesignerService } from '../../services/layout-designer';
import { MauiElement, ElementType } from '../../models/maui-element';
import { Observable } from 'rxjs';

type ResizeDirection = 'nw' | 'ne' | 'sw' | 'se' | 'n' | 's' | 'w' | 'e';

@Component({
  selector: 'app-designer-canvas',
  standalone: true,
  imports: [CommonModule, DragDropModule],
  templateUrl: './designer-canvas.html',
  styleUrl: './designer-canvas.scss'
})
export class DesignerCanvasComponent implements OnInit {
  @ViewChild('canvas', { static: true }) canvas!: ElementRef<HTMLDivElement>;
  
  rootElement$: Observable<MauiElement>;
  selectedElement$: Observable<MauiElement | null>;

  // Resize state
  private isResizing = false;
  private resizeDirection: ResizeDirection | null = null;
  private resizeElement: MauiElement | null = null;
  private startMouseX = 0;
  private startMouseY = 0;
  private startX = 0;
  private startY = 0;
  private startWidth = 0;
  private startHeight = 0;
  
  // Size display during resize
  showSizeDisplay = false;
  sizeDisplayX = 0;
  sizeDisplayY = 0;
  sizeDisplayText = '';

  // Grid cell highlighting state
  highlightedGridCell: { element: MauiElement, row: number, column: number } | null = null;
  
  // Drop zone preview state
  dropZonePreview: { element: MauiElement, visible: boolean } | null = null;

  // True while a toolbox control is dragged over the canvas
  isDragOver = false;

  // Constants
  private readonly MIN_SIZE = 20;

  constructor(
    private elementService: ElementService,
    private dragDropService: DragDropService,
    private layoutDesigner: LayoutDesignerService
  ) {
    this.rootElement$ = this.elementService.elements$;
    this.selectedElement$ = this.elementService.selectedElement$;
  }

  ngOnInit() {
    // Initialize the canvas
  }

  onElementClick(element: MauiElement, event: MouseEvent) {
    event.stopPropagation();
    this.elementService.selectElement(element);
  }

  onCanvasClick() {
    this.elementService.selectElement(null);
  }

  // --- Native drag from the toolbox -----------------------------------------

  onCanvasDragOver(event: DragEvent) {
    if (event.dataTransfer?.types?.includes(TOOLBOX_DRAG_MIME)) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
      this.isDragOver = true;
    }
  }

  onCanvasDragLeave() {
    this.isDragOver = false;
  }

  onCanvasDrop(event: DragEvent) {
    this.isDragOver = false;
    const type = event.dataTransfer?.getData(TOOLBOX_DRAG_MIME) as ElementType;
    if (!type) {
      return;
    }
    event.preventDefault();

    const canvasRect = this.canvas.nativeElement.getBoundingClientRect();
    const x = event.clientX - canvasRect.left;
    const y = event.clientY - canvasRect.top;

    this.dragDropService.createElementAtPosition(type, x, y, this.canvas.nativeElement);
    this.dragDropService.endDrag();
  }

  // --- Keyboard shortcuts ----------------------------------------------------

  @HostListener('document:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent) {
    if (this.isEditingText(event.target)) {
      return;
    }

    const ctrl = event.ctrlKey || event.metaKey;

    if (ctrl && event.key.toLowerCase() === 'z' && !event.shiftKey) {
      event.preventDefault();
      this.elementService.undo();
      return;
    }

    if (ctrl && (event.key.toLowerCase() === 'y' || (event.shiftKey && event.key.toLowerCase() === 'z'))) {
      event.preventDefault();
      this.elementService.redo();
      return;
    }

    const selected = this.elementService.getSelectedElement();
    if (!selected) {
      return;
    }

    if (ctrl && event.key.toLowerCase() === 'd') {
      event.preventDefault();
      this.elementService.duplicateElement(selected);
      return;
    }

    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this.elementService.removeElement(selected);
      return;
    }

    if (event.key === 'Escape') {
      this.elementService.selectElement(null);
      return;
    }

    const step = event.shiftKey ? 10 : 1;
    const nudges: Record<string, { x: number, y: number }> = {
      ArrowUp: { x: 0, y: -step },
      ArrowDown: { x: 0, y: step },
      ArrowLeft: { x: -step, y: 0 },
      ArrowRight: { x: step, y: 0 }
    };

    const nudge = nudges[event.key];
    if (nudge && selected.parent?.type === ElementType.AbsoluteLayout) {
      event.preventDefault();
      this.elementService.updateElementProperties(selected, {
        x: (selected.properties.x || 0) + nudge.x,
        y: (selected.properties.y || 0) + nudge.y
      });
    }
  }

  private isEditingText(target: EventTarget | null): boolean {
    const element = target as HTMLElement | null;
    if (!element) {
      return false;
    }
    const tag = element.tagName?.toLowerCase();
    return tag === 'input' || tag === 'textarea' || tag === 'select' || element.isContentEditable === true;
  }

  // Set DOM element reference for position calculations
  setElementRef(element: MauiElement, domElement: HTMLElement) {
    element.domElement = domElement;
  }

  getElementStyles(element: MauiElement): any {
    const props = element.properties;
    
    const styles: any = {
      width: props.width + 'px',
      height: props.height + 'px',
      zIndex: this.isSelected(element) ? 9999 : 'auto',
      transform: `translate3d(${props.x}px, ${props.y}px, 0px)`
    };

    if (props.backgroundColor) {
      styles.backgroundColor = props.backgroundColor;
    }

    if (props.textColor) {
      styles.color = props.textColor;
    }

    if (props.fontSize) {
      styles.fontSize = props.fontSize + 'px';
    }

    if (props.margin) {
      const m = props.margin;
      styles.margin = `${m.top}px ${m.right}px ${m.bottom}px ${m.left}px`;
    }

    if (props.padding) {
      const p = props.padding;
      styles.padding = `${p.top}px ${p.right}px ${p.bottom}px ${p.left}px`;
    }

    return styles;
  }

  isLayoutElement(element: MauiElement): boolean {
    return [
      ElementType.StackLayout,
      ElementType.Grid,
      ElementType.AbsoluteLayout,
      ElementType.VerticalStackLayout,
      ElementType.Frame,
      ElementType.ScrollView
    ].includes(element.type);
  }

  isSelected(element: MauiElement): boolean {
    const selected = this.elementService.getSelectedElement();
    return selected === element;
  }

  // Select element on pointerdown so cdkDrag will be enabled when drag starts.
  onElementPointerDown(element: MauiElement, event: PointerEvent) {
    // Prevent the pointer event from bubbling to parent elements which may start a drag
    event.stopPropagation();
  }
  
  onDragStarted(element: MauiElement) {
  }

  onDragEnded(element: MauiElement, event: CdkDragEnd) {
    console.log("Drag released for element:", element, event);
    document.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));

    var dropPoint = event.source.getFreeDragPosition();

    this.dragDropService.handleCanvasDrop(element,dropPoint.x,  dropPoint.y, this.canvas.nativeElement);

    //this.elementService.moveElement(element, element.parent!,  element.properties.x! + event.distance.x,  element.properties.y! + event.distance.y)

    this.dragDropService.endDrag();
  }

  onDragMoved(element: MauiElement, event: CdkDragMove) {
    document.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
    // Get layout element over which we are passing currently
    var layoutOver = this.dragDropService.findLayoutAtPosition(element.properties.x! + event.distance.x, element.properties.y! + event.distance.y, this.canvas.nativeElement)!;

    if(layoutOver.type === ElementType.Grid && this.layoutDesigner.getVisualHints(layoutOver).showGrid) {
      const rect = (event.source.getRootElement() as HTMLElement).getBoundingClientRect();
      const parentRect = layoutOver.domElement?.getBoundingClientRect();

      const x = event.pointerPosition.x - (parentRect?.left || 0);
      const y = event.pointerPosition.y - (parentRect?.top || 0);
      const gridCell = this.layoutDesigner.getGridCellAtPosition(layoutOver, x, y, layoutOver.domElement!);
      if(gridCell) {
        this.highlightedGridCell = { element: layoutOver, row: gridCell.row, column: gridCell.column };
        this.dropZonePreview = { element: layoutOver, visible: true };
      } else {
        this.highlightedGridCell = null;
        this.dropZonePreview = null;
      }
    } else {
      this.highlightedGridCell = null;
      this.dropZonePreview = null;
    }

    layoutOver?.domElement?.classList.add('drag-over');
  }

  onElementDroppedOnLayout(targetLayout: MauiElement, event: CdkDragDrop<MauiElement[]>) {
    console.log("Element dropped on layout:", targetLayout, event);
    
    if (event.previousContainer === event.container) {
      // Moving within the same container
      return;
    }
    
    const draggedElement = event.item.data as MauiElement;
    
    if (draggedElement && this.dragDropService.canDropOn(targetLayout, draggedElement)) {
      // Calculate drop position based on the event
      const dropX = event.dropPoint?.x || 0;
      const dropY = event.dropPoint?.y || 0;
      
      // Use the drag-drop service to handle the move
      this.dragDropService.handleElementMove(draggedElement, dropX, dropY, targetLayout);
    }
  }

  // Resize handle interactions
  onResizeStart(event: MouseEvent, direction: ResizeDirection, element: MauiElement) {
    event.preventDefault();
    event.stopPropagation();
    
    this.isResizing = true;
    this.elementService.beginBatch();
    this.resizeDirection = direction;
    this.resizeElement = element;
    this.startMouseX = event.clientX;
    this.startMouseY = event.clientY;
    
    const props = element.properties;
    this.startX = props.x || 0;
    this.startY = props.y || 0;
    this.startWidth = props.width || 100;
    this.startHeight = props.height || 30;
    
    // Show size display
    this.showSizeDisplay = true;
    this.updateSizeDisplay(event);
    
    // Set cursor
    document.body.style.cursor = this.getResizeCursor(direction);
    document.body.style.userSelect = 'none';
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent) {
    if (!this.isResizing || !this.resizeDirection || !this.resizeElement) return;

    const deltaX = event.clientX - this.startMouseX;
    const deltaY = event.clientY - this.startMouseY;

    let newX = this.startX;
    let newY = this.startY;
    let newWidth = this.startWidth;
    let newHeight = this.startHeight;

    // Calculate new dimensions based on resize direction
    switch (this.resizeDirection) {
      case 'nw': // Top-left corner
        newX = this.startX + deltaX;
        newY = this.startY + deltaY;
        newWidth = this.startWidth - deltaX;
        newHeight = this.startHeight - deltaY;
        break;
      case 'ne': // Top-right corner
        newY = this.startY + deltaY;
        newWidth = this.startWidth + deltaX;
        newHeight = this.startHeight - deltaY;
        break;
      case 'sw': // Bottom-left corner
        newX = this.startX + deltaX;
        newWidth = this.startWidth - deltaX;
        newHeight = this.startHeight + deltaY;
        break;
      case 'se': // Bottom-right corner
        newWidth = this.startWidth + deltaX;
        newHeight = this.startHeight + deltaY;
        break;
      case 'n': // Top edge
        newY = this.startY + deltaY;
        newHeight = this.startHeight - deltaY;
        break;
      case 's': // Bottom edge
        newHeight = this.startHeight + deltaY;
        break;
      case 'w': // Left edge
        newX = this.startX + deltaX;
        newWidth = this.startWidth - deltaX;
        break;
      case 'e': // Right edge
        newWidth = this.startWidth + deltaX;
        break;
    }

    // Apply minimum size constraints
    if (newWidth < this.MIN_SIZE) {
      if (this.resizeDirection.includes('w')) {
        newX = this.startX + this.startWidth - this.MIN_SIZE;
      }
      newWidth = this.MIN_SIZE;
    }
    
    if (newHeight < this.MIN_SIZE) {
      if (this.resizeDirection.includes('n')) {
        newY = this.startY + this.startHeight - this.MIN_SIZE;
      }
      newHeight = this.MIN_SIZE;
    }

    // Apply grid cell constraints if element is in a grid
    if (this.resizeElement.parent && this.resizeElement.parent.type === ElementType.Grid) {
      const gridElement = this.resizeElement.parent;
      const gridElementRef = gridElement.domElement;
      
      if (gridElementRef) {
        const maxDimensions = this.layoutDesigner.getGridChildMaxDimensions(
          this.resizeElement,
          gridElement,
          gridElementRef
        );
        
        if (maxDimensions) {
          // Constrain width to grid cell boundaries
          if (newWidth > maxDimensions.maxWidth) {
            if (this.resizeDirection.includes('w')) {
              newX = this.startX + this.startWidth - maxDimensions.maxWidth;
            }
            newWidth = maxDimensions.maxWidth;
          }
          
          // Constrain height to grid cell boundaries
          if (newHeight > maxDimensions.maxHeight) {
            if (this.resizeDirection.includes('n')) {
              newY = this.startY + this.startHeight - maxDimensions.maxHeight;
            }
            newHeight = maxDimensions.maxHeight;
          }
        }
      }
    }

    // Update element properties (folded into a single history entry)
    this.elementService.updateElementProperties(this.resizeElement, {
      x: newX,
      y: newY,
      width: newWidth,
      height: newHeight
    }, { recordHistory: false });

    // Update size display
    this.updateSizeDisplay(event);
  }

  @HostListener('document:mouseup', ['$event'])
  onMouseUp(event: MouseEvent) {
    if (this.isResizing) {
      this.isResizing = false;
      this.elementService.endBatch();
      this.resizeDirection = null;
      this.resizeElement = null;
      this.showSizeDisplay = false;
      
      // Reset cursor
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    }
  }

  private updateSizeDisplay(event: MouseEvent) {
    if (!this.resizeElement) return;
    
    const props = this.resizeElement.properties;
    this.sizeDisplayText = `${Math.round(props.width || 0)} × ${Math.round(props.height || 0)}`;
    this.sizeDisplayX = event.clientX + 10;
    this.sizeDisplayY = event.clientY - 30;
  }

  private getResizeCursor(direction: ResizeDirection): string {
    switch (direction) {
      case 'nw':
      case 'se':
        return 'nw-resize';
      case 'ne':
      case 'sw':
        return 'ne-resize';
      case 'n':
      case 's':
        return 'ns-resize';
      case 'w':
      case 'e':
        return 'ew-resize';
      default:
        return 'default';
    }
  }

  // Layout-specific hover and drop methods
  onLayoutHover(event: MouseEvent, element: MauiElement) {

    if (element.type === ElementType.Grid) {
      const rect = (event.target as HTMLElement).getBoundingClientRect();
      const x = event.clientX - rect.left;
      const y = event.clientY - rect.top;
      
      const gridCell = this.layoutDesigner.getGridCellAtPosition(element, x, y, event.target as HTMLElement);
      if (gridCell) {
        this.highlightedGridCell = { element, row: gridCell.row, column: gridCell.column };
      }
    }
  }

  onLayoutHoverExit(element: MauiElement) {
    if (element.type === ElementType.Grid) {
      this.highlightedGridCell = null;
    }
    this.dropZonePreview = null;
  }

  getGridCellStyles(element: MauiElement, rowIndex: number, columnIndex: number): any {
    if (this.highlightedGridCell && 
        this.highlightedGridCell.element === element &&
        this.highlightedGridCell.row === rowIndex && 
        this.highlightedGridCell.column === columnIndex) {
      return {
        backgroundColor: 'rgba(0, 123, 255, 0.2)',
        border: '2px dashed #007bff'
      };
    }
    return {};
  }

  // Grid-related methods
  isGridElement(element: MauiElement): boolean {
    return element.type === ElementType.Grid;
  }

  shouldShowGrid(element: MauiElement): boolean {
    return this.isGridElement(element) && this.layoutDesigner.getVisualHints(element).showGrid;
  }

  getGridDimensions(element: MauiElement): { rows: number, columns: number } {
    const gridDefinition = element.properties.gridDefinition || {
      rows: [{ height: { value: 1, type: 'Star' } }, { height: { value: 1, type: 'Star' } }],
      columns: [{ width: { value: 1, type: 'Star' } }, { width: { value: 1, type: 'Star' } }]
    };
    
    return {
      rows: gridDefinition.rows.length,
      columns: gridDefinition.columns.length
    };
  }

  getGridCellIndices(element: MauiElement): number[] {
    const dims = this.getGridDimensions(element);
    const totalCells = dims.rows * dims.columns;
    return Array(totalCells).fill(0).map((_, i) => i);
  }

  getGridTemplateColumns(element: MauiElement): string {
    const gridDef = element.properties.gridDefinition;
    if (!gridDef) return 'repeat(2, 1fr)'; // Default 2 equal columns
    return gridDef.columns.map(col => 
      col.width.type === 'Star' ? `${col.width.value}fr` : 
      col.width.type === 'Auto' ? 'auto' : 
      `${col.width.value}px`
    ).join(' ');
  }

  getGridTemplateRows(element: MauiElement): string {
    const gridDef = element.properties.gridDefinition;
    if (!gridDef) return 'repeat(2, 1fr)'; // Default 2 equal rows
    return gridDef.rows.map(row => 
      row.height.type === 'Star' ? `${row.height.value}fr` : 
      row.height.type === 'Auto' ? 'auto' : 
      `${row.height.value}px`
    ).join(' ');
  }

  getGridRowPosition(index: number, element: MauiElement): string {
    const dims = this.getGridDimensions(element);
    const rowIndex = Math.floor(index / dims.columns) + 1;
    return rowIndex.toString();
  }

  getGridColumnPosition(index: number, element: MauiElement): string {
    const dims = this.getGridDimensions(element);
    const colIndex = (index % dims.columns) + 1;
    return colIndex.toString();
  }

  isHighlightedCell(element: MauiElement, index: number): boolean {
    if (!this.highlightedGridCell || this.highlightedGridCell.element !== element) return false;
    const dims = this.getGridDimensions(element);
    const row = Math.floor(index / dims.columns);
    const col = index % dims.columns;
    return this.highlightedGridCell.row === row && this.highlightedGridCell.column === col;
  }
}
