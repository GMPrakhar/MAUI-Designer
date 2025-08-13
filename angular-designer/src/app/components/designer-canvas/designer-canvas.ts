import { Component, OnInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDropList, CdkDragDrop, CdkDragStart, CdkDragEnd } from '@angular/cdk/drag-drop';
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
    event.preventDefault();
    event.stopPropagation();
    console.log('Element clicked:', element.name, element.type);
    this.elementService.selectElement(element);
  }

  onCanvasClick() {
    console.log('Canvas clicked - deselecting');
    this.elementService.selectElement(null);
  }

  onDragStarted(element: MauiElement, event: CdkDragStart) {
    console.log('Drag started for:', element.name);
    // Start drag operation for the selected element
    this.dragDropService.startDrag({
      element: element,
      isFromToolbox: false
    });
  }

  onDragEnded(element: MauiElement, event: CdkDragEnd) {
    console.log('Drag ended for:', element.name, 'at position:', event.dropPoint);
    // End drag operation
    this.dragDropService.endDrag();
    
    // Update element position based on drop point
    const rect = this.canvas.nativeElement.getBoundingClientRect();
    const x = Math.max(0, event.dropPoint.x - rect.left);
    const y = Math.max(0, event.dropPoint.y - rect.top);
    
    console.log('New position:', x, y);
    
    // Update the element's position
    element.properties.x = x;
    element.properties.y = y;
    
    // Notify the element service of the change
    this.elementService.updateElementProperties(element, {
      x: element.properties.x,
      y: element.properties.y
    });
  }

  getElementStyles(element: MauiElement): any {
    const props = element.properties;
    
    const styles: any = {
      position: 'absolute',
      left: props.x + 'px',
      top: props.y + 'px',
      width: props.width + 'px',
      height: props.height + 'px'
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
}
