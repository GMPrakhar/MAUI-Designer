import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ElementService } from '../../services/element';
import { MauiElement } from '../../models/maui-element';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-hierarchy-panel',
  imports: [CommonModule],
  templateUrl: './hierarchy-panel.html',
  styleUrl: './hierarchy-panel.scss'
})
export class HierarchyPanelComponent implements OnInit {
  rootElement$: Observable<MauiElement>;
  selectedElement$: Observable<MauiElement | null>;

  constructor(private elementService: ElementService) {
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
      case 'StackLayout': return 'view_agenda';
      case 'Grid': return 'grid_view';
      case 'AbsoluteLayout': return 'crop_free';
      case 'Frame': return 'crop_portrait';
      case 'ScrollView': return 'unfold_more';
      default: return 'widgets';
    }
  }
}
