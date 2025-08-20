import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDragStart, CdkDragDrop } from '@angular/cdk/drag-drop';
import { MAUI_CONTROLS, ToolboxItem, ToolboxCategory } from '../../models/toolbox';
import { ElementType } from '../../models/maui-element';
import { ElementService } from '../../services/element';
import { DragDropService } from '../../services/drag-drop';

@Component({
  selector: 'app-toolbox',
  standalone: true,
  imports: [CommonModule, DragDropModule],
  templateUrl: './toolbox.html',
  styleUrl: './toolbox.scss'
})
export class ToolboxComponent {
  toolboxItems = MAUI_CONTROLS;
  categories = Object.values(ToolboxCategory);

  constructor(
    private elementService: ElementService,
    private dragDropService: DragDropService
  ) {}

  getItemsByCategory(category: ToolboxCategory): ToolboxItem[] {
    return this.toolboxItems.filter(item => item.category === category);
  }

  onItemClick(item: ToolboxItem) {
    // Create element at default position when clicked
    const selectedElement = this.elementService.getSelectedElement();
    
    // If a layout element is selected and can accept children, add to it
    // Otherwise, add to root element
    let targetParent = this.elementService.getRootElement();
    
    if (selectedElement && this.dragDropService.canDropOn(selectedElement)) {
      targetParent = selectedElement;
    }
    
    if (targetParent) {
      console.log("Creating element via click, target parent:", targetParent.type);
      
      const newElement = this.elementService.createElement(
        item.type as ElementType,
        { x: 0, y: 0 }
      );
      
      console.log("Created element with properties:", newElement.properties);
      
      // Add element to target parent 
      this.elementService.addElement(newElement, targetParent);
      this.elementService.selectElement(newElement);
      
      console.log("Element added to parent:", newElement.parent?.type);
    }
  }

  onDragStart(item: ToolboxItem, event: CdkDragStart) {
    // Set drag data to indicate this is a toolbox drag
    this.dragDropService.startDrag({
      elementType: item.type as ElementType,
      isFromToolbox: true
    });
  }

  onDragEnd() {
    this.dragDropService.endDrag();
  }

  onToolboxDropped(event: any) {
    // Handle dropped items back to toolbox (not implemented for this use case)
    console.log('Item dropped back to toolbox:', event);
  }
}
