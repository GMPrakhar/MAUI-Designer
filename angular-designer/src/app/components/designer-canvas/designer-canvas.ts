import { Component, OnInit, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDropList, CdkDragDrop } from '@angular/cdk/drag-drop';
import { ElementService } from '../../services/element';
import { DragDropService } from '../../services/drag-drop';
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
    console.log("Selected element", element);
    this.elementService.selectElement(element);
  }

  onCanvasClick() {
    this.elementService.selectElement(null);
  }

  getElementStyles(element: MauiElement): any {
    const props = element.properties;
    
    const styles: any = {
      position: 'absolute',
      left: props.x + 'px',
      top: props.y + 'px',
      width: props.width + 'px',
      height: props.height + 'px',
      zIndex: this.isSelected(element) ? 9999 : 'auto'
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
    // Do not call preventDefault so interactive children (inputs/buttons) still work
    this.elementService.selectElement(element);
  }
  
  onDragStarted(element: MauiElement) {
    console.log("Drag started for element:", element);
  }

  onDragEnded(element: MauiElement) {
    console.log("Drag released for element:", element);
  }

  // Resize handle interactions
  onResizeStart(event: MouseEvent, direction: ResizeDirection, element: MauiElement) {
    event.preventDefault();
    event.stopPropagation();
    
    this.isResizing = true;
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

    // Update element properties
    this.elementService.updateElementProperties(this.resizeElement, {
      x: newX,
      y: newY,
      width: newWidth,
      height: newHeight
    });

    // Update size display
    this.updateSizeDisplay(event);
  }

  @HostListener('document:mouseup', ['$event'])
  onMouseUp(event: MouseEvent) {
    if (this.isResizing) {
      this.isResizing = false;
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
}
