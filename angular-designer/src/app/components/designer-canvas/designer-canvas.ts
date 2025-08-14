import { Component, OnInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDropList, CdkDragDrop } from '@angular/cdk/drag-drop';
import { ElementService } from '../../services/element';
import { DragDropService } from '../../services/drag-drop';
import { LayoutDesignerService } from '../../services/layout-designer';
import { MauiElement, ElementType } from '../../models/maui-element';
import { Observable } from 'rxjs';

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

  onDrop(event: CdkDragDrop<any>) {
    const dragData = this.dragDropService.getDragData();
    if (!dragData) return;

    const rect = this.canvas.nativeElement.getBoundingClientRect();
    const x = event.dropPoint.x - rect.left;
    const y = event.dropPoint.y - rect.top;

    if (dragData.isFromToolbox && dragData.elementType) {
      this.handleToolboxDrop(dragData.elementType, x, y);
    } else if (dragData.element) {
      this.handleElementMove(dragData.element, x, y);
    }
  }

  private handleToolboxDrop(elementType: ElementType, x: number, y: number) {
    const rootElement = this.elementService.getRootElement();
    this.dragDropService.handleToolboxDrop(
      {} as CdkDragDrop<any>, 
      x, 
      y, 
      rootElement
    );
  }

  private handleElementMove(element: MauiElement, x: number, y: number) {
    const rootElement = this.elementService.getRootElement();
    this.dragDropService.handleElementMove(element, x, y, rootElement);
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
    this.elementService.selectElement(element);
  }
}
