import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ElementService } from '../../services/element';
import { MauiElement, ElementProperties } from '../../models/maui-element';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-properties-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './properties-panel.html',
  styleUrl: './properties-panel.scss'
})
export class PropertiesPanelComponent implements OnInit {
  selectedElement$: Observable<MauiElement | null>;
  
  constructor(private elementService: ElementService) {
    this.selectedElement$ = this.elementService.selectedElement$;
  }

  ngOnInit() {
    // Initialize properties panel
  }

  updateProperty(element: MauiElement, property: keyof ElementProperties, value: any) {
    this.elementService.updateElementProperties(element, { [property]: value });
  }

  updateMargin(element: MauiElement, side: 'left' | 'top' | 'right' | 'bottom', value: number) {
    const currentMargin = element.properties.margin || { left: 0, top: 0, right: 0, bottom: 0 };
    const newMargin = { ...currentMargin, [side]: value };
    this.elementService.updateElementProperties(element, { margin: newMargin });
  }

  updatePadding(element: MauiElement, side: 'left' | 'top' | 'right' | 'bottom', value: number) {
    const currentPadding = element.properties.padding || { left: 0, top: 0, right: 0, bottom: 0 };
    const newPadding = { ...currentPadding, [side]: value };
    this.elementService.updateElementProperties(element, { padding: newPadding });
  }
}
