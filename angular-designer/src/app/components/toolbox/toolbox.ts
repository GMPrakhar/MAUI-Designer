import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDragStart } from '@angular/cdk/drag-drop';
import { MAUI_CONTROLS, ToolboxItem, ToolboxCategory } from '../../models/toolbox';
import { ElementType } from '../../models/maui-element';
import { DragDropService } from '../../services/drag-drop';

@Component({
  selector: 'app-toolbox',
  imports: [CommonModule, DragDropModule],
  templateUrl: './toolbox.html',
  styleUrl: './toolbox.scss'
})
export class ToolboxComponent {
  toolboxItems = MAUI_CONTROLS;
  categories = Object.values(ToolboxCategory);

  constructor(private dragDropService: DragDropService) {}

  getItemsByCategory(category: ToolboxCategory): ToolboxItem[] {
    return this.toolboxItems.filter(item => item.category === category);
  }

  onDragStart(event: CdkDragStart, item: ToolboxItem) {
    this.dragDropService.startDrag({
      elementType: item.type as ElementType,
      isFromToolbox: true
    });
  }

  onDragEnd() {
    this.dragDropService.endDrag();
  }
}
