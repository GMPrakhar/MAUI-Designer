import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CdkDrag, CdkDragDrop, CdkDropList, DragDropModule } from '@angular/cdk/drag-drop';
import { ElementService } from '../../services/element';
import { LayoutDesignerService } from '../../services/layout-designer';
import { DragDropService } from '../../services/drag-drop';
import { MauiElement } from '../../models/maui-element';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-hierarchy-panel',
  standalone: true,
  imports: [CommonModule, DragDropModule],
  templateUrl: './hierarchy-panel.html',
  styleUrl: './hierarchy-panel.scss'
})
export class HierarchyPanelComponent implements OnInit {
  rootElement$: Observable<MauiElement>;
  selectedElement$: Observable<MauiElement | null>;

  constructor(
    private elementService: ElementService,
    private layoutDesigner: LayoutDesignerService,
    private dragDropService: DragDropService
  ) {
    this.rootElement$ = this.elementService.elements$;
    this.selectedElement$ = this.elementService.selectedElement$;
  }

  ngOnInit() {
    // Initialize hierarchy view
  }

  onElementSelect(element: MauiElement) {
    this.elementService.selectElement(element);
  }

  onElementDelete(element: MauiElement, event: Event) {
    event.stopPropagation();
    this.elementService.removeElement(element);
  }

  onMoveSibling(element: MauiElement, delta: -1 | 1, event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.elementService.moveSibling(element, delta);
  }

  onNodeKeydown(event: KeyboardEvent, element: MauiElement) {
    if (!event.altKey) {
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.elementService.moveSibling(element, -1);
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.elementService.moveSibling(element, 1);
    }
  }

  readonly canEnter = (drag: CdkDrag<MauiElement>, drop: CdkDropList<MauiElement>): boolean => {
    return this.layoutDesigner.canDropOn(drop.data, drag.data);
  };

  onDrop(event: CdkDragDrop<MauiElement>, parent: MauiElement) {
    const child = event.item.data as MauiElement;
    if (!child || child.id === 'root') {
      return;
    }

    if (event.previousContainer === event.container) {
      this.elementService.reorderChild(parent, event.previousIndex, event.currentIndex);
      return;
    }

    if (!this.layoutDesigner.canDropOn(parent, child)) {
      return;
    }

    this.elementService.runAsSingleChange(() => {
      this.elementService.moveElement(child, parent, 0, 0, event.currentIndex);
      this.dragDropService.constrainToParent(child);
    });
  }

  isSelected(element: MauiElement): boolean {
    const selected = this.elementService.getSelectedElement();
    return selected === element;
  }

  getElementIcon(element: MauiElement): string {
    switch (element.type) {
      case 'Label': return 'text_fields';
      case 'Button': return 'smart_button';
      case 'Entry': return 'input';
      case 'Editor': return 'edit_note';
      case 'Image': return 'image';
      case 'Path': return 'gesture';
      case 'StackLayout': return 'view_agenda';
      case 'Grid': return 'grid_view';
      case 'AbsoluteLayout': return 'crop_free';
      case 'Frame': return 'crop_portrait';
      case 'ScrollView': return 'unfold_more';
      default: return 'widgets';
    }
  }
}
