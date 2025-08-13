import { Injectable } from '@angular/core';
import { CdkDragDrop, CdkDragEnd, CdkDragStart, transferArrayItem } from '@angular/cdk/drag-drop';
import { ElementService } from './element';
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

  constructor(private elementService: ElementService) { }

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
    this.elementService.moveElement(element, targetParent, x, y);
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
}
