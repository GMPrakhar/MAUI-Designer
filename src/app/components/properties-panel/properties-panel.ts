import { Component, OnInit, OnDestroy } from '@angular/core';
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
  FontAttributes,
  BINDABLE_PROPERTIES,
  COMMON_BINDABLE_PROPERTIES
} from '../../models/maui-element';
import { Observable, Subscription } from 'rxjs';
import { AlignmentService, AlignMode } from '../../services/alignment';

@Component({
  selector: 'app-properties-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './properties-panel.html',
  styleUrl: './properties-panel.scss'
})
export class PropertiesPanelComponent implements OnInit, OnDestroy {
  selectedElement$: Observable<MauiElement | null>;
  selection: MauiElement[] = [];
  private subscription = new Subscription();

  readonly orientations = Object.values(Orientation);
  readonly fontAttributes = Object.values(FontAttributes);
  readonly gridLengthTypes = Object.values(GridLengthType);

  constructor(
    private elementService: ElementService,
    private alignmentService: AlignmentService
  ) {
    this.selectedElement$ = this.elementService.selectedElement$;
  }

  ngOnInit() {
    this.subscription.add(
      this.elementService.selectedElements$.subscribe(selection => (this.selection = selection))
    );
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  // --- Multi selection --------------------------------------------------------

  get isMultiSelection(): boolean {
    return this.selection.length > 1;
  }

  get selectionCount(): number {
    return this.selection.length;
  }

  /** Returns the value shared by every selected element, or '' when they differ. */
  sharedValue(property: keyof ElementProperties): any {
    if (this.selection.length === 0) {
      return '';
    }
    const first = this.selection[0].properties[property];
    return this.selection.every(element => element.properties[property] === first) ? first ?? '' : '';
  }

  updateSelection(property: keyof ElementProperties, value: any) {
    this.elementService.updateSelectionProperties({ [property]: value });
  }

  removeSelection() {
    this.elementService.removeSelectedElements();
  }

  duplicateSelection() {
    const targets = [...this.selection];
    this.elementService.runAsSingleChange(() => {
      targets.forEach(element => this.elementService.duplicateElement(element));
    });
  }

  align(mode: AlignMode) {
    this.alignmentService.align(this.selection, mode);
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

  private static readonly PLACEHOLDER_TYPES = [ElementType.Entry, ElementType.Editor, ElementType.SearchBar];
  private static readonly RANGE_TYPES = [ElementType.Slider, ElementType.Stepper];
  private static readonly CONTROL_PROPERTY_TYPES = [
    ElementType.Entry,
    ElementType.Editor,
    ElementType.SearchBar,
    ElementType.CheckBox,
    ElementType.Switch,
    ElementType.Slider,
    ElementType.Stepper,
    ElementType.ProgressBar,
    ElementType.ActivityIndicator,
    ElementType.DatePicker,
    ElementType.Border,
    ElementType.Frame,
    ElementType.CollectionView
  ];

  hasControlProperties(element: MauiElement): boolean {
    return PropertiesPanelComponent.CONTROL_PROPERTY_TYPES.includes(element.type);
  }

  supportsPlaceholder(element: MauiElement): boolean {
    return PropertiesPanelComponent.PLACEHOLDER_TYPES.includes(element.type);
  }

  supportsRange(element: MauiElement): boolean {
    return PropertiesPanelComponent.RANGE_TYPES.includes(element.type);
  }

  supportsCornerRadius(element: MauiElement): boolean {
    return element.type === ElementType.Border || element.type === ElementType.Frame;
  }

  // --- Data bindings ----------------------------------------------------------

  bindableProperties(element: MauiElement): string[] {
    const specific = BINDABLE_PROPERTIES[element.type] || [];
    return [...new Set([...specific, ...COMMON_BINDABLE_PROPERTIES])];
  }

  bindingValue(element: MauiElement, property: string): string {
    return element.properties.bindings?.[property] || '';
  }

  updateBinding(element: MauiElement, property: string, path: string) {
    const bindings = { ...(element.properties.bindings || {}) };
    const trimmed = path.trim();
    if (trimmed) {
      bindings[property] = trimmed;
    } else {
      delete bindings[property];
    }
    this.elementService.updateElementProperties(element, { bindings });
  }

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
