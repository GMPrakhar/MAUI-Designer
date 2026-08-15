import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ElementService } from '../../services/element';
import {
  MauiElement,
  ElementProperties,
  ElementType,
  GridLength,
  GridLengthType,
  Orientation,
  FontAttributes
} from '../../models/maui-element';
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

  readonly orientations = Object.values(Orientation);
  readonly fontAttributes = Object.values(FontAttributes);
  readonly gridLengthTypes = Object.values(GridLengthType);

  constructor(private elementService: ElementService) {
    this.selectedElement$ = this.elementService.selectedElement$;
  }

  ngOnInit() {
    // Initialize properties panel
  }

  updateProperty(element: MauiElement, property: keyof ElementProperties, value: any) {
    this.elementService.updateElementProperties(element, { [property]: value });
  }

  updateName(element: MauiElement, name: string) {
    this.elementService.renameElement(element, name);
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

  // --- Capability helpers -----------------------------------------------------

  isStackLayout(element: MauiElement): boolean {
    return element.type === ElementType.StackLayout || element.type === ElementType.VerticalStackLayout;
  }

  supportsOrientation(element: MauiElement): boolean {
    return element.type === ElementType.StackLayout;
  }

  isGrid(element: MauiElement): boolean {
    return element.type === ElementType.Grid;
  }

  isGridChild(element: MauiElement): boolean {
    return element.parent?.type === ElementType.Grid;
  }

  gridRowCount(element: MauiElement): number {
    return this.elementService.getGridDefinition(element).rows.length;
  }

  gridColumnCount(element: MauiElement): number {
    return this.elementService.getGridDefinition(element).columns.length;
  }

  gridRows(element: MauiElement) {
    return this.elementService.getGridDefinition(element).rows;
  }

  gridColumns(element: MauiElement) {
    return this.elementService.getGridDefinition(element).columns;
  }

  // --- Grid editing -----------------------------------------------------------

  addRow(element: MauiElement) {
    this.elementService.addGridRow(element);
  }

  addColumn(element: MauiElement) {
    this.elementService.addGridColumn(element);
  }

  removeRow(element: MauiElement, index: number) {
    this.elementService.removeGridRow(element, index);
  }

  removeColumn(element: MauiElement, index: number) {
    this.elementService.removeGridColumn(element, index);
  }

  updateRowType(element: MauiElement, index: number, type: string) {
    const current = this.gridRows(element)[index].height;
    this.elementService.updateGridRow(element, index, { value: current.value, type: type as GridLengthType });
  }

  updateRowValue(element: MauiElement, index: number, value: number) {
    const current = this.gridRows(element)[index].height;
    this.elementService.updateGridRow(element, index, { value, type: current.type });
  }

  updateColumnType(element: MauiElement, index: number, type: string) {
    const current = this.gridColumns(element)[index].width;
    this.elementService.updateGridColumn(element, index, { value: current.value, type: type as GridLengthType });
  }

  updateColumnValue(element: MauiElement, index: number, value: number) {
    const current = this.gridColumns(element)[index].width;
    this.elementService.updateGridColumn(element, index, { value, type: current.type });
  }

  gridLengthLabel(length: GridLength): string {
    if (length.type === GridLengthType.Auto) {
      return 'Auto';
    }
    return length.type === GridLengthType.Star ? `${length.value}*` : `${length.value}px`;
  }

  // --- Element actions --------------------------------------------------------

  duplicate(element: MauiElement) {
    this.elementService.duplicateElement(element);
  }

  remove(element: MauiElement) {
    this.elementService.removeElement(element);
  }

  isRoot(element: MauiElement): boolean {
    return element.id === 'root' || !element.parent;
  }
}
