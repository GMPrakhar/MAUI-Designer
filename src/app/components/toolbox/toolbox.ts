import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAUI_CONTROLS, ToolboxItem, ToolboxCategory } from '../../models/toolbox';
import { ElementType, MauiElement } from '../../models/maui-element';
import { ElementService } from '../../services/element';
import { DragDropService, TOOLBOX_DRAG_MIME } from '../../services/drag-drop';
import { ClipboardService, ComponentTemplate, StarterPage } from '../../services/clipboard';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-toolbox',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './toolbox.html',
  styleUrl: './toolbox.scss'
})
export class ToolboxComponent {
  toolboxItems = MAUI_CONTROLS;
  categories = Object.values(ToolboxCategory);
  searchTerm = '';

  templates$: Observable<ComponentTemplate[]>;
  starterPages: StarterPage[];

  constructor(
    private elementService: ElementService,
    private dragDropService: DragDropService,
    private clipboardService: ClipboardService
  ) {
    this.templates$ = this.clipboardService.templates$;
    this.starterPages = this.clipboardService.starterPages;
  }

  // --- Templates & starter pages ---------------------------------------------

  matchesSearch(text: string): boolean {
    const term = this.searchTerm.trim().toLowerCase();
    return !term || text.toLowerCase().includes(term);
  }

  insertTemplate(template: ComponentTemplate) {
    this.clipboardService.insertTemplate(template.id, this.resolveTargetParent());
  }

  deleteTemplate(event: Event, template: ComponentTemplate) {
    event.stopPropagation();
    this.clipboardService.deleteTemplate(template.id);
  }

  applyStarterPage(page: StarterPage) {
    this.clipboardService.applyStarterPage(page.id);
  }

  getItemsByCategory(category: ToolboxCategory): ToolboxItem[] {
    const term = this.searchTerm.trim().toLowerCase();
    return this.toolboxItems.filter(item =>
      item.category === category &&
      (!term ||
        item.displayName.toLowerCase().includes(term) ||
        item.type.toLowerCase().includes(term) ||
        item.description.toLowerCase().includes(term))
    );
  }

  /** Categories are hidden while filtering when they have no matching item. */
  hasItems(category: ToolboxCategory): boolean {
    return this.getItemsByCategory(category).length > 0;
  }

  get hasAnyResult(): boolean {
    return this.categories.some(category => this.hasItems(category));
  }

  clearSearch() {
    this.searchTerm = '';
  }

  onItemClick(item: ToolboxItem) {
    const parent = this.resolveTargetParent();
    const newElement = this.elementService.createElement(item.type as ElementType, { x: 0, y: 0 });
    this.elementService.addElement(newElement, parent);
    this.elementService.selectElement(newElement);
  }

  onItemKeydown(event: KeyboardEvent, item: ToolboxItem) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.onItemClick(item);
    }
  }

  onDragStart(event: DragEvent, item: ToolboxItem) {
    this.dragDropService.startDrag({ elementType: item.type as ElementType, isFromToolbox: true });
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'copy';
      event.dataTransfer.setData(TOOLBOX_DRAG_MIME, item.type);
      event.dataTransfer.setData('text/plain', item.type);
    }
  }

  onDragEnd() {
    this.dragDropService.endDrag();
  }

  /**
   * New controls are added to the selected layout when one is selected,
   * so nesting does not require a drag operation.
   */
  resolveTargetParent(): MauiElement {
    const selected = this.elementService.getSelectedElement();
    if (selected && this.dragDropService.canHaveChildren(selected.type)) {
      return selected;
    }
    if (selected?.parent && this.dragDropService.canHaveChildren(selected.parent.type)) {
      return selected.parent;
    }
    return this.elementService.getRootElement();
  }
}
