import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAUI_CONTROLS, ToolboxItem, ToolboxCategory } from '../../models/toolbox';
import { ElementType } from '../../models/maui-element';
import { ElementService } from '../../services/element';

@Component({
  selector: 'app-toolbox',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toolbox.html',
  styleUrl: './toolbox.scss'
})
export class ToolboxComponent {
  toolboxItems = MAUI_CONTROLS;
  categories = Object.values(ToolboxCategory);

  constructor(private elementService: ElementService) {}

  getItemsByCategory(category: ToolboxCategory): ToolboxItem[] {
    return this.toolboxItems.filter(item => item.category === category);
  }

  onItemClick(item: ToolboxItem) {
    // Add element to center of the canvas when clicked
    const rootElement = this.elementService.getRootElement();
    if (rootElement) {
      const newElement = this.elementService.createElement(
        item.type as ElementType,
        { x: 50, y: 50 } // Default position in center area
      );
      this.elementService.addElement(newElement, rootElement);
      this.elementService.selectElement(newElement);
    }
  }
}
