import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';import { MauiElement, ElementType, ElementProperties, GridDefinition, GridRowDefinition, GridColumnDefinition, GridLength, GridLengthType, Orientation, DEFAULT_ICON_PATH_DATA } from '../models/maui-element';

/**
 * How to restack the selection among its siblings. Later children paint on top,
 * so "front" is the end of the array.
 */
export type ReorderMode = 'front' | 'back' | 'forward' | 'backward';

@Injectable({
  providedIn: 'root'
})
export class ElementService {
  private rootElement: MauiElement;
  private selectedElement: MauiElement | null = null;
  private selectedElements: MauiElement[] = [];
  private elementCounter = 0;

  private selectedElementSubject = new BehaviorSubject<MauiElement | null>(null);
  private selectedElementsSubject = new BehaviorSubject<MauiElement[]>([]);
  private elementsSubject: BehaviorSubject<MauiElement>;

  selectedElement$ = this.selectedElementSubject.asObservable();
  selectedElements$ = this.selectedElementsSubject.asObservable();
  elements$: Observable<MauiElement>;

  // Undo / redo history of serialized snapshots
  private undoStack: string[] = [];
  private redoStack: string[] = [];
  private historySubject = new BehaviorSubject<{ canUndo: boolean; canRedo: boolean }>({ canUndo: false, canRedo: false });
  history$ = this.historySubject.asObservable();
  private readonly MAX_HISTORY = 50;
  private suppressHistory = false;
  private lastEditKey: string | null = null;
  private lastEditTime = 0;
  private readonly COALESCE_WINDOW_MS = 700;

  private static readonly STORAGE_KEY = 'maui-designer.design';

  constructor() {
    this.rootElement = this.createRootElement();
    this.elementsSubject = new BehaviorSubject<MauiElement>(this.rootElement);
    this.elements$ = this.elementsSubject.asObservable();
  }

  private createRootElement(): MauiElement {
    return {
      id: 'root',
      type: ElementType.AbsoluteLayout,
      name: 'Root Layout',
      properties: {
        backgroundColor: '#ffffff',
        width: 800,
        height: 600
      },
      children: []
    };
  }

  createElement(type: ElementType, properties?: Partial<ElementProperties>): MauiElement {
    this.elementCounter++;
    const defaultProperties = this.getDefaultProperties(type);
    const merged = { ...defaultProperties, ...properties };
    // Custom controls are named after their XAML tag, not after "Custom"
    const baseName = type === ElementType.Custom && merged.customTag ? merged.customTag : type;

    return {
      id: `element_${this.elementCounter}`,
      type: type,
      name: `${baseName}${this.elementCounter}`,
      properties: merged,
      children: []
    };
  }

  private getDefaultProperties(type: ElementType): ElementProperties {
    const common = {
      x: 0,
      y: 0,
      width: 100,
      height: 30,
      isVisible: true,
      isEnabled: true
    };

    switch (type) {
      case ElementType.Label:
        return {
          ...common,
          text: 'Label',
          textColor: '#000000',
          fontSize: 14
        };
      case ElementType.Button:
        return {
          ...common,
          text: 'Button',
          backgroundColor: '#007acc',
          textColor: '#ffffff',
          fontSize: 14
        };
      case ElementType.Entry:
        return {
          ...common,
          width: 200,
          text: '',
          placeholder: 'Enter text',
          backgroundColor: '#ffffff',
          textColor: '#000000'
        };
      case ElementType.Editor:
        return {
          ...common,
          width: 200,
          height: 100,
          text: '',
          placeholder: 'Enter text',
          backgroundColor: '#ffffff',
          textColor: '#000000'
        };
      case ElementType.SearchBar:
        return {
          ...common,
          width: 220,
          height: 40,
          placeholder: 'Search',
          backgroundColor: '#ffffff',
          textColor: '#000000'
        };
      case ElementType.CheckBox:
        return {
          ...common,
          width: 40,
          height: 40,
          isChecked: false
        };
      case ElementType.Switch:
        return {
          ...common,
          width: 60,
          height: 32,
          isToggled: false
        };
      case ElementType.Slider:
        return {
          ...common,
          width: 200,
          height: 32,
          minimum: 0,
          maximum: 100,
          value: 50
        };
      case ElementType.Stepper:
        return {
          ...common,
          width: 120,
          height: 40,
          minimum: 0,
          maximum: 100,
          increment: 1,
          value: 0
        };
      case ElementType.ProgressBar:
        return {
          ...common,
          width: 200,
          height: 12,
          progress: 0.5
        };
      case ElementType.ActivityIndicator:
        return {
          ...common,
          width: 40,
          height: 40,
          isRunning: true
        };
      case ElementType.DatePicker:
        return {
          ...common,
          width: 180,
          height: 40,
          date: new Date().toISOString().slice(0, 10),
          backgroundColor: '#ffffff',
          textColor: '#000000'
        };
      case ElementType.Image:
        return {
          ...common,
          width: 100,
          height: 100
        };
      case ElementType.Path:
        return {
          ...common,
          width: 24,
          height: 24,
          pathData: DEFAULT_ICON_PATH_DATA,
          fillColor: '#000000',
          strokeColor: 'Transparent',
          strokeThickness: 0
        };
      case ElementType.StackLayout:
        return {
          ...common,
          width: 200,
          height: 200,
          orientation: Orientation.Vertical,
          spacing: 5
        };
      case ElementType.VerticalStackLayout:
        return {
          ...common,
          width: 200,
          height: 200,
          spacing: 5
        };
      case ElementType.Grid:
        return {
          ...common,
          width: 200,
          height: 200,
          gridDefinition: ElementService.createDefaultGridDefinition()
        };
      case ElementType.AbsoluteLayout:
        return {
          ...common,
          width: 200,
          height: 200
        };
      case ElementType.Frame:
        return {
          ...common,
          backgroundColor: '#f0f0f0',
          width: 150,
          height: 100
        };
      case ElementType.Border:
        return {
          ...common,
          width: 150,
          height: 100,
          backgroundColor: '#ffffff',
          borderColor: '#cccccc',
          borderWidth: 1,
          cornerRadius: 8
        };
      case ElementType.CollectionView:
        return {
          ...common,
          width: 240,
          height: 200,
          itemCount: 3,
          itemTemplateText: 'Item'
        };
      case ElementType.ScrollView:
        return {
          ...common,
          width: 200,
          height: 200
        };
      case ElementType.Custom:
        return {
          ...common,
          width: 120,
          height: 48,
          customValues: {},
          rawAttributes: {}
        };
      default:
        return common;
    }
  }

  addElement(element: MauiElement, parent?: MauiElement): void {
    this.pushHistory();
    const targetParent = parent || this.rootElement;
    element.parent = targetParent;
    targetParent.children.push(element);
    this.elementsSubject.next(this.rootElement);
  }

  removeElement(element: MauiElement): void {
    if (element === this.rootElement) {
      return;
    }

    this.pushHistory();

    if (element.parent) {
      const index = element.parent.children.indexOf(element);
      if (index > -1) {
        element.parent.children.splice(index, 1);
      }
    }

    // Clear the selection when the removed element (or one of its descendants)
    // was selected, otherwise the properties panel keeps editing a detached node.
    if (this.selectedElement && this.isSelfOrDescendant(this.selectedElement, element)) {
      this.selectElement(null);
    }

    this.elementsSubject.next(this.rootElement);
  }

  private isSelfOrDescendant(candidate: MauiElement, ancestor: MauiElement): boolean {
    let current: MauiElement | undefined = candidate;
    while (current) {
      if (current === ancestor) {
        return true;
      }
      current = current.parent;
    }
    return false;
  }

  /** Creates a deep copy of an element (new ids/names) and inserts it next to the original. */
  duplicateElement(element: MauiElement): MauiElement | null {
    if (element === this.rootElement || !element.parent) {
      return null;
    }

    const parent = element.parent;
    this.pushHistory();

    const clone = this.cloneElement(element, parent);
    const index = parent.children.indexOf(element);
    parent.children.splice(index + 1, 0, clone);

    if (parent.type === ElementType.AbsoluteLayout) {
      clone.properties.x = (clone.properties.x || 0) + 10;
      clone.properties.y = (clone.properties.y || 0) + 10;
    }

    this.elementsSubject.next(this.rootElement);
    this.selectElement(clone);
    return clone;
  }

  private cloneElement(element: MauiElement, parent?: MauiElement): MauiElement {
    this.elementCounter++;
    const clone: MauiElement = {
      id: `element_${this.elementCounter}`,
      type: element.type,
      name: `${element.type}${this.elementCounter}`,
      properties: JSON.parse(JSON.stringify(element.properties)),
      children: [],
      parent
    };
    clone.children = element.children.map(child => this.cloneElement(child, clone));
    return clone;
  }

  /** Serializes a set of elements (used by copy/paste and templates). */
  serializeElements(elements: MauiElement[]): string {
    return JSON.stringify(elements.map(element => ElementService.toPlainObject(element)));
  }

  /**
   * Inserts previously serialized elements into a parent with fresh ids and
   * names, as a single undo step. Returns the inserted elements.
   */
  insertSerializedElements(json: string, parent?: MauiElement, offset = 0): MauiElement[] {
    let plain: any[];
    try {
      plain = JSON.parse(json);
    } catch {
      return [];
    }
    if (!Array.isArray(plain) || plain.length === 0) {
      return [];
    }

    const target = parent || this.rootElement;
    const inserted: MauiElement[] = [];

    this.runAsSingleChange(() => {
      for (const item of plain) {
        const element = ElementService.fromPlainObject(item, target);
        this.assignFreshIds(element);
        if (offset && target.type === ElementType.AbsoluteLayout) {
          element.properties.x = (element.properties.x || 0) + offset;
          element.properties.y = (element.properties.y || 0) + offset;
        }
        target.children.push(element);
        inserted.push(element);
      }
    });

    this.elementsSubject.next(this.rootElement);
    this.setSelection(inserted);
    return inserted;
  }

  private assignFreshIds(element: MauiElement): void {
    this.elementCounter++;
    const previousName = element.name;
    element.id = `element_${this.elementCounter}`;
    element.name = previousName && !/^element_\d+$/.test(previousName)
      ? `${previousName}Copy${this.elementCounter}`
      : `${element.type}${this.elementCounter}`;
    element.domElement = undefined;
    element.children.forEach(child => this.assignFreshIds(child));
  }

  selectElement(element: MauiElement | null): void {
    this.selectedElement = element;
    this.selectedElements = element ? [element] : [];
    this.selectedElementSubject.next(element);
    this.selectedElementsSubject.next(this.selectedElements);
  }

  /** Replaces the whole selection. The last entry becomes the primary selection. */
  setSelection(elements: MauiElement[]): void {
    const unique = elements.filter((element, index) => elements.indexOf(element) === index);
    this.selectedElements = unique;
    this.selectedElement = unique.length ? unique[unique.length - 1] : null;
    this.selectedElementSubject.next(this.selectedElement);
    this.selectedElementsSubject.next(this.selectedElements);
  }

  /** Adds or removes an element from the selection (Ctrl/Shift click). */
  toggleSelection(element: MauiElement): void {
    const next = this.selectedElements.includes(element)
      ? this.selectedElements.filter(candidate => candidate !== element)
      : [...this.selectedElements, element];
    this.setSelection(next);
  }

  isElementSelected(element: MauiElement): boolean {
    return this.selectedElements.includes(element);
  }

  getSelectedElements(): MauiElement[] {
    return [...this.selectedElements];
  }

  /** Deletes every selected element as a single undo step. */
  removeSelectedElements(): void {
    const targets = this.selectedElements.filter(element => element !== this.rootElement && element.parent);
    if (targets.length === 0) {
      return;
    }
    this.runAsSingleChange(() => {
      targets.forEach(element => this.removeElement(element));
    });
    this.selectElement(null);
  }

  /** Applies the same property patch to several elements in one undo step. */
  updateSelectionProperties(properties: Partial<ElementProperties>): void {
    const targets = this.selectedElements;
    if (targets.length === 0) {
      return;
    }
    this.runAsSingleChange(() => {
      targets.forEach(element =>
        this.updateElementProperties(element, properties, { recordHistory: false })
      );
    });
  }

  /** Moves every selected element that lives in an AbsoluteLayout. */
  nudgeSelection(deltaX: number, deltaY: number): void {
    const targets = this.selectedElements.filter(element => element.parent?.type === ElementType.AbsoluteLayout);
    if (targets.length === 0) {
      return;
    }
    this.runAsSingleChange(() => {
      targets.forEach(element =>
        this.updateElementProperties(element, {
          x: (element.properties.x || 0) + deltaX,
          y: (element.properties.y || 0) + deltaY
        }, { recordHistory: false })
      );
    });
  }

  /**
   * Restacks the selection within its parent.
   *
   * Both the canvas and the generated XAML take their stacking order straight
   * from `children`, so reordering the array is the whole feature -- there is no
   * separate z-index to keep in sync.
   */
  reorderSelection(mode: ReorderMode): boolean {
    const targets = this.selectedElements.filter(element => element !== this.rootElement && element.parent);
    if (targets.length === 0) {
      return false;
    }

    // A selection can span several parents, and "front" means the front of your
    // own parent -- there is no ordering between separate containers.
    const byParent = new Map<MauiElement, MauiElement[]>();
    for (const element of targets) {
      const siblings = byParent.get(element.parent!) ?? [];
      siblings.push(element);
      byParent.set(element.parent!, siblings);
    }

    let changed = false;
    const willChange = [...byParent].some(([parent, group]) => this.wouldReorder(parent, group, mode));
    if (!willChange) {
      // Nothing to do -- bail before opening a history batch, or the selection
      // being already at the edge would cost the user an empty undo step.
      return false;
    }

    this.runAsSingleChange(() => {
      byParent.forEach((group, parent) => {
        if (this.reorderWithinParent(parent, group, mode)) {
          changed = true;
        }
      });
    });

    if (changed) {
      this.elementsSubject.next(this.rootElement);
    }
    return changed;
  }

  /** True when the selection sits in a container that has something to restack against. */
  canReorderSelection(): boolean {
    return this.selectedElements.some(
      element => element !== this.rootElement && (element.parent?.children.length ?? 0) > 1
    );
  }

  /**
   * Whether the move would actually change anything.
   *
   * Moving up is a no-op exactly when the group already occupies a contiguous
   * block at the end -- every member is either at the edge or blocked by another
   * member. Moving down is the mirror image.
   */
  private wouldReorder(parent: MauiElement, group: MauiElement[], mode: ReorderMode): boolean {
    const children = parent.children;
    const indices = group.map(child => children.indexOf(child)).sort((a, b) => a - b);
    const contiguous = indices.every((index, offset) => index === indices[0] + offset);

    if (mode === 'front' || mode === 'forward') {
      return !(contiguous && indices[indices.length - 1] === children.length - 1);
    }
    return !(contiguous && indices[0] === 0);
  }

  private reorderWithinParent(parent: MauiElement, group: MauiElement[], mode: ReorderMode): boolean {
    const children = parent.children;
    const before = [...children];

    // Keep the group's own relative order intact whichever end it moves to.
    const ordered = group.slice().sort((a, b) => children.indexOf(a) - children.indexOf(b));

    if (mode === 'front' || mode === 'back') {
      const rest = children.filter(child => !group.includes(child));
      const next = mode === 'front' ? [...rest, ...ordered] : [...ordered, ...rest];
      children.splice(0, children.length, ...next);
    } else {
      // One step at a time. Walking from the edge the group moves towards stops
      // members of the same group from hopping over each other, which would
      // scramble their relative order instead of moving them as a block.
      const step = mode === 'forward' ? 1 : -1;
      const walk = mode === 'forward' ? ordered.slice().reverse() : ordered;

      for (const element of walk) {
        const index = children.indexOf(element);
        const swapWith = index + step;
        if (swapWith < 0 || swapWith >= children.length || group.includes(children[swapWith])) {
          continue;
        }
        children[index] = children[swapWith];
        children[swapWith] = element;
      }
    }

    return children.some((child, index) => child !== before[index]);
  }

  updateElementProperties(element: MauiElement, properties: Partial<ElementProperties>, options: { recordHistory?: boolean } = {}): void {
    if (options.recordHistory !== false) {
      this.recordCoalescedHistory(element, properties);
    }
    element.properties = { ...element.properties, ...properties };
    this.elementsSubject.next(this.rootElement);
  }

  /**
   * Property edits are undoable, but continuous edits (typing in a field,
   * dragging a slider) collapse into a single history entry.
   */
  private recordCoalescedHistory(element: MauiElement, properties: Partial<ElementProperties>): void {
    const key = `${element.id}:${Object.keys(properties).sort().join(',')}`;
    const now = Date.now();
    if (this.lastEditKey !== key || now - this.lastEditTime > this.COALESCE_WINDOW_MS) {
      this.pushHistory();
    }
    this.lastEditKey = key;
    this.lastEditTime = now;
  }

  renameElement(element: MauiElement, name: string): void {
    const trimmed = (name || '').trim();
    if (!trimmed) {
      return;
    }
    element.name = trimmed;
    this.elementsSubject.next(this.rootElement);
  }

  updateElementCoordinatesSilently(element: MauiElement, x: number, y: number): void {
    // Update X and Y coordinates without triggering UI updates
    if(element.parent?.type !== ElementType.AbsoluteLayout) {
      console.warn("updateElementCoordinatesSilently is intended for elements within AbsoluteLayout only.");
      return;
    }

    element.properties.x = x;
    element.properties.y = y;
    // Note: We deliberately do NOT call this.elementsSubject.next() to avoid UI updates
  }

  findElementById(id: string, root?: MauiElement): MauiElement | null {
    const searchRoot = root || this.rootElement;
    
    if (searchRoot.id === id) {
      return searchRoot;
    }
    
    for (const child of searchRoot.children) {
      const found = this.findElementById(id, child);
      if (found) {
        return found;
      }
    }
    
    return null;
  }

  moveElement(element: MauiElement, newParent: MauiElement, x: number, y: number, insertionIndex?: number): void {

    this.pushHistory();

    if(element.parent != newParent)
    {
      // Remove from current parent
      if (element.parent) {
        const index = element.parent.children.indexOf(element);
        if (index > -1) {
          element.parent.children.splice(index, 1);
        }
      }
    
      // Insert at specific index if provided, otherwise append
      if (insertionIndex !== undefined && insertionIndex >= 0 && insertionIndex <= newParent.children.length) {
        newParent.children.splice(insertionIndex, 0, element);
      } else {
        newParent.children.push(element);
      }
    }

    // Add to new parent
    element.parent = newParent;
    
    if(newParent.type !== ElementType.AbsoluteLayout) {
      console.warn("updateElementCoordinatesSilently is intended for elements within AbsoluteLayout only.");
    }
    else{
      element.properties.x = x;
      element.properties.y = y;
    }
    
    this.elementsSubject.next(this.rootElement);
  }

  /**
   * Reorders a child among its siblings.
   *
   * `toIndex` is the destination index after the move (CDK `moveItemInArray`
   * / drop-list `currentIndex`).
   */
  reorderChild(parent: MauiElement, fromIndex: number, toIndex: number): boolean {
    const children = parent.children;
    if (fromIndex === toIndex) {
      return false;
    }
    if (fromIndex < 0 || toIndex < 0 || fromIndex >= children.length || toIndex >= children.length) {
      return false;
    }

    this.pushHistory();
    const [item] = children.splice(fromIndex, 1);
    children.splice(toIndex, 0, item);
    this.elementsSubject.next(this.rootElement);
    return true;
  }

  /** Moves an element one slot among its siblings. */
  moveSibling(element: MauiElement, delta: -1 | 1): boolean {
    const live = this.findElementById(element.id) ?? element;
    if (live === this.rootElement) {
      return false;
    }
    const parent = this.findOwningParent(live);
    if (!parent) {
      return false;
    }
    const from = parent.children.indexOf(live);
    const to = from + delta;
    if (from < 0 || to < 0 || to >= parent.children.length) {
      return false;
    }
    return this.reorderChild(parent, from, to);
  }

  /** The parent whose `children` array actually contains the element. */
  private findOwningParent(element: MauiElement): MauiElement | null {
    if (element.parent?.children.includes(element)) {
      return element.parent;
    }
    const walk = (node: MauiElement): MauiElement | null => {
      if (node.children.includes(element)) {
        return node;
      }
      for (const child of node.children) {
        const found = walk(child);
        if (found) {
          return found;
        }
      }
      return null;
    };
    return walk(this.rootElement);
  }

  getRootElement(): MauiElement {
    return this.rootElement;
  }

  getSelectedElement(): MauiElement | null {
    return this.selectedElement;
  }

  setRootElement(element: MauiElement): void {
    this.pushHistory();
    this.applyRootElement(element);
  }

  private applyRootElement(element: MauiElement): void {
    this.rootElement = element;
    this.reindexCounter(element);
    this.elementsSubject.next(this.rootElement);
    // Clear selection when setting new root
    this.selectElement(null);
  }

  /** Keeps the id counter ahead of any ids present in an imported tree. */
  private reindexCounter(element: MauiElement): void {
    const match = /^element_(\d+)$/.exec(element.id);
    if (match) {
      this.elementCounter = Math.max(this.elementCounter, parseInt(match[1], 10));
    }
    element.children.forEach(child => this.reindexCounter(child));
  }

  // ---------------------------------------------------------------------------
  // Grid definitions
  // ---------------------------------------------------------------------------

  static createDefaultGridDefinition(): GridDefinition {
    return {
      rows: [
        { height: { value: 1, type: GridLengthType.Star } },
        { height: { value: 1, type: GridLengthType.Star } }
      ],
      columns: [
        { width: { value: 1, type: GridLengthType.Star } },
        { width: { value: 1, type: GridLengthType.Star } }
      ]
    };
  }

  getGridDefinition(gridElement: MauiElement): GridDefinition {
    if (!gridElement.properties.gridDefinition) {
      gridElement.properties.gridDefinition = ElementService.createDefaultGridDefinition();
    }
    return gridElement.properties.gridDefinition;
  }

  addGridRow(gridElement: MauiElement): void {
    if (gridElement.type !== ElementType.Grid) {
      return;
    }
    this.pushHistory();
    const definition = this.getGridDefinition(gridElement);
    definition.rows.push({ height: { value: 1, type: GridLengthType.Star } });
    this.elementsSubject.next(this.rootElement);
  }

  addGridColumn(gridElement: MauiElement): void {
    if (gridElement.type !== ElementType.Grid) {
      return;
    }
    this.pushHistory();
    const definition = this.getGridDefinition(gridElement);
    definition.columns.push({ width: { value: 1, type: GridLengthType.Star } });
    this.elementsSubject.next(this.rootElement);
  }

  removeGridRow(gridElement: MauiElement, rowIndex: number): void {
    if (gridElement.type !== ElementType.Grid) {
      return;
    }
    const definition = this.getGridDefinition(gridElement);
    if (definition.rows.length <= 1 || rowIndex < 0 || rowIndex >= definition.rows.length) {
      return;
    }
    this.pushHistory();
    definition.rows.splice(rowIndex, 1);
    this.clampChildrenToGrid(gridElement, definition);
    this.elementsSubject.next(this.rootElement);
  }

  removeGridColumn(gridElement: MauiElement, columnIndex: number): void {
    if (gridElement.type !== ElementType.Grid) {
      return;
    }
    const definition = this.getGridDefinition(gridElement);
    if (definition.columns.length <= 1 || columnIndex < 0 || columnIndex >= definition.columns.length) {
      return;
    }
    this.pushHistory();
    definition.columns.splice(columnIndex, 1);
    this.clampChildrenToGrid(gridElement, definition);
    this.elementsSubject.next(this.rootElement);
  }

  updateGridRow(gridElement: MauiElement, rowIndex: number, length: GridLength): void {
    const definition = this.getGridDefinition(gridElement);
    if (rowIndex < 0 || rowIndex >= definition.rows.length) {
      return;
    }
    this.pushHistory();
    definition.rows[rowIndex] = { height: length };
    this.elementsSubject.next(this.rootElement);
  }

  updateGridColumn(gridElement: MauiElement, columnIndex: number, length: GridLength): void {
    const definition = this.getGridDefinition(gridElement);
    if (columnIndex < 0 || columnIndex >= definition.columns.length) {
      return;
    }
    this.pushHistory();
    definition.columns[columnIndex] = { width: length };
    this.elementsSubject.next(this.rootElement);
  }

  private clampChildrenToGrid(gridElement: MauiElement, definition: GridDefinition): void {
    for (const child of gridElement.children) {
      child.properties.row = Math.min(child.properties.row || 0, definition.rows.length - 1);
      child.properties.column = Math.min(child.properties.column || 0, definition.columns.length - 1);
    }
  }

  // ---------------------------------------------------------------------------
  // Undo / redo
  // ---------------------------------------------------------------------------

  /** Captures the current tree so the next mutation can be reverted. */
  pushHistory(): void {
    if (this.suppressHistory) {
      return;
    }
    this.lastEditKey = null;
    this.undoStack.push(this.serialize());
    if (this.undoStack.length > this.MAX_HISTORY) {
      this.undoStack.shift();
    }
    this.redoStack = [];
    this.emitHistoryState();
  }

  /**
   * Starts a batch: one history entry is recorded now and further mutations
   * are folded into it until endBatch() is called (used while resizing/dragging).
   */
  beginBatch(): void {
    this.pushHistory();
    this.suppressHistory = true;
  }

  endBatch(): void {
    this.suppressHistory = false;
    this.lastEditKey = null;
  }

  /** Runs a multi-step mutation that should be undone as a single step. */
  runAsSingleChange<T>(action: () => T): T {
    this.beginBatch();
    try {
      return action();
    } finally {
      this.endBatch();
    }
  }

  canUndo(): boolean {
    return this.undoStack.length > 0;
  }

  canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  undo(): void {
    const snapshot = this.undoStack.pop();
    if (!snapshot) {
      return;
    }
    this.redoStack.push(this.serialize());
    this.lastEditKey = null;
    this.applyRootElement(ElementService.deserialize(snapshot));
    this.emitHistoryState();
  }

  redo(): void {
    const snapshot = this.redoStack.pop();
    if (!snapshot) {
      return;
    }
    this.undoStack.push(this.serialize());
    this.lastEditKey = null;
    this.applyRootElement(ElementService.deserialize(snapshot));
    this.emitHistoryState();
  }

  private emitHistoryState(): void {
    this.historySubject.next({ canUndo: this.canUndo(), canRedo: this.canRedo() });
  }

  // ---------------------------------------------------------------------------
  // Serialization / persistence
  // ---------------------------------------------------------------------------

  /** JSON representation of the tree, stripped of circular parent/DOM references. */
  serialize(root?: MauiElement): string {
    return JSON.stringify(ElementService.toPlainObject(root || this.rootElement));
  }

  private static toPlainObject(element: MauiElement): any {
    return {
      id: element.id,
      type: element.type,
      name: element.name,
      properties: element.properties,
      children: element.children.map(child => ElementService.toPlainObject(child))
    };
  }

  static deserialize(json: string): MauiElement {
    const plain = JSON.parse(json);
    return ElementService.fromPlainObject(plain, undefined);
  }

  private static fromPlainObject(plain: any, parent?: MauiElement): MauiElement {
    const element: MauiElement = {
      id: plain.id,
      type: plain.type,
      name: plain.name,
      properties: plain.properties || {},
      children: [],
      parent
    };
    element.children = (plain.children || []).map((child: any) => ElementService.fromPlainObject(child, element));
    return element;
  }

  /** Persists the current design in localStorage. Returns false when storage is unavailable. */
  saveToStorage(): boolean {
    try {
      localStorage.setItem(ElementService.STORAGE_KEY, this.serialize());
      return true;
    } catch {
      return false;
    }
  }

  /** Restores a previously saved design. Returns false when nothing valid is stored. */
  loadFromStorage(): boolean {
    try {
      const stored = localStorage.getItem(ElementService.STORAGE_KEY);
      if (!stored) {
        return false;
      }
      const restored = ElementService.deserialize(stored);
      this.setRootElement(restored);
      return true;
    } catch {
      return false;
    }
  }

  hasStoredDesign(): boolean {
    try {
      return !!localStorage.getItem(ElementService.STORAGE_KEY);
    } catch {
      return false;
    }
  }

  clearStorage(): void {
    try {
      localStorage.removeItem(ElementService.STORAGE_KEY);
    } catch {
      // ignore
    }
  }

  /** Removes every child of the root layout. */
  clearDesign(): void {
    this.pushHistory();
    this.rootElement.children = [];
    this.selectElement(null);
    this.elementsSubject.next(this.rootElement);
  }
}
