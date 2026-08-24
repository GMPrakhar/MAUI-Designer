import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAUI_CONTROLS, ToolboxItem, ToolboxCategory } from '../../models/toolbox';
import { ElementType, MauiElement } from '../../models/maui-element';
import { ElementService } from '../../services/element';
import { DragDropService, TOOLBOX_DRAG_MIME } from '../../services/drag-drop';
import { ClipboardService, ComponentTemplate, StarterPage } from '../../services/clipboard';
import { CustomControlRegistryService } from '../../services/custom-control-registry';
import { CustomControlDefinition, CustomControlManifest } from '../../models/custom-control';
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
  manifests$: Observable<CustomControlManifest[]>;
  manifestError = '';

  constructor(
    private elementService: ElementService,
    private dragDropService: DragDropService,
    private clipboardService: ClipboardService,
    private registry: CustomControlRegistryService
  ) {
    this.templates$ = this.clipboardService.templates$;
    this.starterPages = this.clipboardService.starterPages;
    this.manifests$ = this.registry.manifests$;
  }

  // --- Custom controls --------------------------------------------------------

  /** Controls of a manifest that survive the current search filter. */
  visibleControls(manifest: CustomControlManifest): CustomControlDefinition[] {
    const term = this.searchTerm.trim().toLowerCase();
    return manifest.controls.filter(control =>
      !term ||
      control.tag.toLowerCase().includes(term) ||
      (control.displayName || '').toLowerCase().includes(term) ||
      manifest.package.toLowerCase().includes(term)
    );
  }

  onCustomItemClick(manifest: CustomControlManifest, control: CustomControlDefinition) {
    const parent = this.resolveTargetParent();
    const element = this.elementService.createElement(
      ElementType.Custom,
      this.registry.defaultProperties({ manifest, definition: control })
    );
    this.elementService.addElement(element, parent);
    this.dragDropService.constrainToParent(element);
    this.elementService.selectElement(element);
  }

  onCustomItemKeydown(event: KeyboardEvent, manifest: CustomControlManifest, control: CustomControlDefinition) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.onCustomItemClick(manifest, control);
    }
  }

  onCustomDragStart(event: DragEvent, manifest: CustomControlManifest, control: CustomControlDefinition) {
    const payload = `Custom:${manifest.xmlns.prefix}:${control.tag}`;
    this.dragDropService.startDrag({ elementType: ElementType.Custom, isFromToolbox: true });
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'copy';
      event.dataTransfer.setData(TOOLBOX_DRAG_MIME, payload);
      event.dataTransfer.setData('text/plain', control.tag);
    }
  }

  removeManifest(manifest: CustomControlManifest) {
    this.registry.remove(manifest.id);
  }

  async importManifest(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    try {
      this.registry.import(await file.text());
      this.manifestError = '';
    } catch (error: any) {
      this.manifestError = error?.message || 'Could not import that manifest.';
    } finally {
      input.value = '';
    }
  }

  exportManifests() {
    const blob = new Blob([this.registry.export()], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'maui-designer-controls.json';
    link.click();
    URL.revokeObjectURL(url);
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
    return (
      this.categories.some(category => this.hasItems(category)) ||
      this.registry.manifests.some(manifest => this.visibleControls(manifest).length > 0)
    );
  }

  clearSearch() {
    this.searchTerm = '';
  }

  onItemClick(item: ToolboxItem) {
    const parent = this.resolveTargetParent();
    const newElement = this.elementService.createElement(item.type as ElementType, { x: 0, y: 0 });
    this.elementService.addElement(newElement, parent);
    this.dragDropService.constrainToParent(newElement);
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
    if (selected && this.dragDropService.canElementHaveChildren(selected)) {
      return selected;
    }
    if (selected?.parent && this.dragDropService.canElementHaveChildren(selected.parent)) {
      return selected.parent;
    }
    return this.elementService.getRootElement();
  }
}
