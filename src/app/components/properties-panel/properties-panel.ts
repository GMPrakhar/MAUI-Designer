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
  LAYOUT_OPTIONS,
  BINDABLE_PROPERTIES,
  COMMON_BINDABLE_PROPERTIES
} from '../../models/maui-element';
import { Observable, Subscription } from 'rxjs';
import { AlignmentService, AlignMode } from '../../services/alignment';
import { CustomControlRegistryService } from '../../services/custom-control-registry';
import { CustomControlDefinition, CustomPropertyDefinition } from '../../models/custom-control';
import { AccessibilityService, AccessibilityIssue } from '../../services/accessibility';

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
    private alignmentService: AlignmentService,
    private registry: CustomControlRegistryService,
    private accessibilityService: AccessibilityService
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

  readonly layoutOptions = LAYOUT_OPTIONS;

  updateLayoutOptions(element: MauiElement, axis: 'horizontalOptions' | 'verticalOptions', value: string) {
    const option = LAYOUT_OPTIONS.find(candidate => candidate === value);
    this.elementService.updateElementProperties(element, { [axis]: option });
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
    const specific = element.type === ElementType.Custom
      ? this.registry.bindableProperties(element)
      : BINDABLE_PROPERTIES[element.type] || [];
    return [...new Set([...specific, ...COMMON_BINDABLE_PROPERTIES])];
  }

  // --- Custom (third party) controls ------------------------------------------

  isCustom(element: MauiElement): boolean {
    return element.type === ElementType.Custom;
  }

  customDefinition(element: MauiElement): CustomControlDefinition | null {
    return this.registry.findForElement(element)?.definition || null;
  }

  customPackage(element: MauiElement): string {
    return this.registry.findForElement(element)?.manifest.package || 'Unknown package';
  }

  customProperties(element: MauiElement): CustomPropertyDefinition[] {
    return this.customDefinition(element)?.properties || [];
  }

  customValue(element: MauiElement, property: string): string {
    return element.properties.customValues?.[property] ?? '';
  }

  customBoolean(element: MauiElement, property: string): boolean {
    return /^true$/i.test(this.customValue(element, property));
  }

  updateCustomValue(element: MauiElement, property: string, value: string | number | boolean) {
    const customValues = { ...(element.properties.customValues || {}) };
    const next = typeof value === 'boolean' ? (value ? 'True' : 'False') : String(value);

    if (next === '') {
      delete customValues[property];
    } else {
      customValues[property] = next;
    }

    this.elementService.updateElementProperties(element, { customValues });
  }

  /** Attributes kept from imported XAML that the manifest does not declare. */
  rawAttributeNames(element: MauiElement): string[] {
    return Object.keys(element.properties.rawAttributes || {});
  }

  rawAttributeValue(element: MauiElement, name: string): string {
    return element.properties.rawAttributes?.[name] ?? '';
  }

  updateRawAttribute(element: MauiElement, name: string, value: string) {
    const rawAttributes = { ...(element.properties.rawAttributes || {}) };
    if (value === '') {
      delete rawAttributes[name];
    } else {
      rawAttributes[name] = value;
    }
    this.elementService.updateElementProperties(element, { rawAttributes });
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

  // --- Theming (AppThemeBinding) -----------------------------------------------

  /**
   * The colour properties worth theming for this element. Derived from the
   * element type rather than a fixed list because `Stroke` means the border of
   * a Border but the outline of a Path, and only one applies at a time.
   */
  themeableColors(element: MauiElement): { name: string; label: string; fallback: string }[] {
    const colors = [{ name: 'BackgroundColor', label: 'Background', fallback: '#ffffff' }];

    if (element.properties.text !== undefined) {
      colors.push({ name: 'TextColor', label: 'Text', fallback: '#000000' });
    }
    if (element.type === ElementType.Path) {
      colors.push({ name: 'Fill', label: 'Fill', fallback: '#000000' });
      colors.push({ name: 'Stroke', label: 'Stroke', fallback: '#000000' });
    } else if (element.type === ElementType.Border) {
      colors.push({ name: 'Stroke', label: 'Border', fallback: '#cccccc' });
    }

    return colors;
  }

  themeColor(element: MauiElement, property: string, mode: 'light' | 'dark'): string {
    return element.properties.appTheme?.[property]?.[mode] || '';
  }

  hasThemeColor(element: MauiElement, property: string): boolean {
    const theme = element.properties.appTheme?.[property];
    return !!(theme?.light || theme?.dark);
  }

  updateThemeColor(element: MauiElement, property: string, mode: 'light' | 'dark', value: string) {
    const appTheme = { ...(element.properties.appTheme || {}) };
    const entry = { ...(appTheme[property] || {}), [mode]: value };

    if (!entry.light && !entry.dark) {
      delete appTheme[property];
    } else {
      appTheme[property] = entry;
    }

    this.elementService.updateElementProperties(element, { appTheme });
  }

  clearThemeColor(element: MauiElement, property: string) {
    const appTheme = { ...(element.properties.appTheme || {}) };
    delete appTheme[property];
    this.elementService.updateElementProperties(element, { appTheme });
  }

  // --- Accessibility -----------------------------------------------------------

  readonly headingLevels = ['', 'Level1', 'Level2', 'Level3', 'Level4', 'Level5', 'Level6', 'Level7', 'Level8', 'Level9'];

  accessibilityIssues(element: MauiElement): AccessibilityIssue[] {
    return this.accessibilityService.inspect(element);
  }

  /** The measured contrast, shown so the user can see how close to the line they are. */
  contrastRatio(element: MauiElement): number | null {
    return this.accessibilityService.contrastOf(element);
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
