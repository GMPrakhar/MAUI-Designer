import { Component, OnInit, OnDestroy, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDropList, CdkDragDrop, CdkDragEnd, CdkDragMove } from '@angular/cdk/drag-drop';
import { ElementService } from '../../services/element';
import { DragDropService, TOOLBOX_DRAG_MIME } from '../../services/drag-drop';
import { LayoutDesignerService } from '../../services/layout-designer';
import { MauiElement, ElementType } from '../../models/maui-element';
import { AlignmentService, AlignmentGuide } from '../../services/alignment';
import { ClipboardService } from '../../services/clipboard';
import { CustomControlRegistryService } from '../../services/custom-control-registry';
import { CustomPreview } from '../../models/custom-control';
import { ViewportService, ViewportState } from '../../services/viewport';
import { Observable, Subscription } from 'rxjs';

type ResizeDirection = 'nw' | 'ne' | 'sw' | 'se' | 'n' | 's' | 'w' | 'e';

@Component({
  selector: 'app-designer-canvas',
  standalone: true,
  imports: [CommonModule, DragDropModule],
  templateUrl: './designer-canvas.html',
  styleUrl: './designer-canvas.scss'
})
export class DesignerCanvasComponent implements OnInit, OnDestroy {
  @ViewChild('canvas', { static: true }) canvas!: ElementRef<HTMLDivElement>;
  
  rootElement$: Observable<MauiElement>;
  selectedElement$: Observable<MauiElement | null>;

  // Resize state
  private isResizing = false;
  private resizeDirection: ResizeDirection | null = null;
  private resizeElement: MauiElement | null = null;
  private startMouseX = 0;
  private startMouseY = 0;
  private startX = 0;
  private startY = 0;
  private startWidth = 0;
  private startHeight = 0;
  
  // Size display during resize
  showSizeDisplay = false;
  sizeDisplayX = 0;
  sizeDisplayY = 0;
  sizeDisplayText = '';

  // Grid cell highlighting state
  highlightedGridCell: { element: MauiElement, row: number, column: number } | null = null;
  
  // Drop zone preview state
  dropZonePreview: { element: MauiElement, visible: boolean } | null = null;

  // True while a toolbox control is dragged over the canvas
  isDragOver = false;

  // Marquee selection state
  marquee: { x: number, y: number, width: number, height: number } | null = null;
  private marqueeOrigin = { x: 0, y: 0 };
  private isMarqueeSelecting = false;
  private marqueeAdditive = false;
  private dragOrigin: { x: number, y: number } | null = null;
  private suppressNextCanvasClick = false;
  private pointerDownPoint: { x: number, y: number } | null = null;

  // Constants
  private readonly MIN_SIZE = 20;
  /** A click whose pointer travelled further than this came from a drag, not from a click. */
  private readonly CLICK_SLOP = 4;

  // Viewport (zoom / pan / theme / grid) state
  viewport!: ViewportState;
  designWidth = 800;
  designHeight = 600;
  horizontalTicks: { offset: number, label: number }[] = [];
  verticalTicks: { offset: number, label: number }[] = [];
  alignmentGuides: AlignmentGuide[] = [];

  private isPanning = false;
  private panOrigin = { x: 0, y: 0 };
  private spacePressed = false;
  private subscription = new Subscription();

  constructor(
    private elementService: ElementService,
    private dragDropService: DragDropService,
    private layoutDesigner: LayoutDesignerService,
    private alignmentService: AlignmentService,
    private clipboardService: ClipboardService,
    private viewportService: ViewportService,
    private registry: CustomControlRegistryService
  ) {
    this.rootElement$ = this.elementService.elements$;
    this.selectedElement$ = this.elementService.selectedElement$;
    this.viewport = this.viewportService.state;
  }

  ngOnInit() {
    this.subscription.add(
      this.viewportService.state$.subscribe(state => {
        this.viewport = state;
        this.rebuildRulers();
      })
    );

    this.subscription.add(
      this.elementService.elements$.subscribe(root => {
        this.designWidth = root.properties.width || 800;
        this.designHeight = root.properties.height || 600;
        this.rebuildRulers();
      })
    );
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  get canvasTransform(): string {
    const { panX, panY, zoom } = this.viewport;
    return `translate(${panX}px, ${panY}px) scale(${zoom})`;
  }

  /** Ruler ticks are spaced by the grid size, scaled by the current zoom. */
  private rebuildRulers() {
    if (!this.viewport.showRulers) {
      this.horizontalTicks = [];
      this.verticalTicks = [];
      return;
    }

    const step = Math.max(this.viewport.gridSize * 5, 40);
    const zoom = this.viewport.zoom;

    this.horizontalTicks = this.buildTicks(this.designWidth, step, zoom, this.viewport.panX);
    this.verticalTicks = this.buildTicks(this.designHeight, step, zoom, this.viewport.panY);
  }

  private buildTicks(size: number, step: number, zoom: number, pan: number) {
    const ticks: { offset: number, label: number }[] = [];
    for (let value = 0; value <= size; value += step) {
      ticks.push({ offset: Math.round(value * zoom + pan), label: value });
    }
    return ticks;
  }

  /** Converts a client point into unscaled canvas coordinates. */
  private toCanvasPoint(clientX: number, clientY: number): { x: number, y: number } {
    const rect = this.canvas.nativeElement.getBoundingClientRect();
    const zoom = this.viewport.zoom || 1;
    return {
      x: (clientX - rect.left) / zoom,
      y: (clientY - rect.top) / zoom
    };
  }

  // --- Pan & wheel zoom -------------------------------------------------------

  onViewportMouseDown(event: MouseEvent) {
    // Middle button or Space + drag pans the design surface
    if (event.button !== 1 && !(event.button === 0 && this.spacePressed)) {
      return;
    }
    event.preventDefault();
    this.isPanning = true;
    this.panOrigin = { x: event.clientX, y: event.clientY };
  }

  onViewportWheel(event: WheelEvent) {
    if (!event.ctrlKey && !event.metaKey) {
      return;
    }
    event.preventDefault();
    const factor = event.deltaY < 0 ? 1.1 : 0.9;
    this.viewportService.setZoom(this.viewport.zoom * factor);
  }

  onElementClick(element: MauiElement, event: MouseEvent) {
    event.stopPropagation();
    // A marquee that started on the root layout still emits a click on it
    if (this.shouldIgnoreClick(event)) {
      return;
    }
    if (event.shiftKey || event.ctrlKey || event.metaKey) {
      this.elementService.toggleSelection(element);
      return;
    }
    this.elementService.selectElement(element);
  }

  onCanvasClick(event: MouseEvent) {
    if (this.shouldIgnoreClick(event)) {
      return;
    }
    this.elementService.selectElement(null);
  }

  /**
   * True for the trailing click a marquee or a drag leaves behind. After a drop the element
   * has moved, so that click lands on whatever is now under the pointer and would hand the
   * selection to it. The pointer travel is used rather than a flag or a timer because the
   * click can be delivered either before or after the drag ends.
   */
  private shouldIgnoreClick(event: MouseEvent): boolean {
    const origin = this.pointerDownPoint;
    this.pointerDownPoint = null;

    if (this.suppressNextCanvasClick) {
      this.suppressNextCanvasClick = false;
      return true;
    }

    if (!origin) {
      return false;
    }

    return Math.abs(event.clientX - origin.x) > this.CLICK_SLOP ||
      Math.abs(event.clientY - origin.y) > this.CLICK_SLOP;
  }

  // --- Marquee (rubber band) selection ---------------------------------------

  onCanvasMouseDown(event: MouseEvent) {
    if (event.button !== 0) {
      return;
    }

    // A new press means any click still owed by the previous drag will never arrive
    this.suppressNextCanvasClick = false;

    // Every left press inside the canvas is remembered so the click it produces can be
    // told apart from the one a drag leaves behind
    this.pointerDownPoint = { x: event.clientX, y: event.clientY };

    const target = event.target as HTMLElement;
    const owner = target.closest('[data-element-id]') as HTMLElement | null;

    // Only start a marquee on the empty canvas or on the root layout itself
    if (owner && owner.getAttribute('data-element-id') !== 'root') {
      return;
    }

    this.marqueeOrigin = this.toCanvasPoint(event.clientX, event.clientY);
    this.marquee = { x: this.marqueeOrigin.x, y: this.marqueeOrigin.y, width: 0, height: 0 };
    this.isMarqueeSelecting = true;
    this.marqueeAdditive = event.shiftKey || event.ctrlKey || event.metaKey;
  }

  private updateMarquee(event: MouseEvent) {
    const { x: currentX, y: currentY } = this.toCanvasPoint(event.clientX, event.clientY);

    this.marquee = {
      x: Math.min(this.marqueeOrigin.x, currentX),
      y: Math.min(this.marqueeOrigin.y, currentY),
      width: Math.abs(currentX - this.marqueeOrigin.x),
      height: Math.abs(currentY - this.marqueeOrigin.y)
    };
  }

  private finishMarquee() {
    const marquee = this.marquee;
    this.isMarqueeSelecting = false;
    this.marquee = null;

    if (!marquee || (marquee.width < 4 && marquee.height < 4)) {
      return;
    }

    // A marquee drag must not also clear the selection through the click handler
    this.suppressNextCanvasClick = true;

    const canvasRect = this.canvas.nativeElement.getBoundingClientRect();
    const zoom = this.viewport.zoom || 1;
    const hits: MauiElement[] = [];

    this.canvas.nativeElement.querySelectorAll('[data-element-id]').forEach(node => {
      const id = node.getAttribute('data-element-id');
      if (!id || id === 'root') {
        return;
      }

      const rect = (node as HTMLElement).getBoundingClientRect();
      const left = (rect.left - canvasRect.left) / zoom;
      const top = (rect.top - canvasRect.top) / zoom;
      const width = rect.width / zoom;
      const height = rect.height / zoom;
      const intersects =
        left < marquee.x + marquee.width &&
        left + width > marquee.x &&
        top < marquee.y + marquee.height &&
        top + height > marquee.y;

      if (!intersects) {
        return;
      }

      const element = this.elementService.findElementById(id);
      // Selecting a container implicitly covers its children, so skip nested hits
      if (element && !hits.some(hit => this.isDescendantOf(element, hit))) {
        hits.push(element);
      }
    });

    const selection = this.marqueeAdditive
      ? [...this.elementService.getSelectedElements(), ...hits]
      : hits;
    this.elementService.setSelection(selection);
  }

  private isDescendantOf(candidate: MauiElement, ancestor: MauiElement): boolean {
    let current = candidate.parent;
    while (current) {
      if (current === ancestor) {
        return true;
      }
      current = current.parent;
    }
    return false;
  }

  // --- Native drag from the toolbox -----------------------------------------

  onCanvasDragOver(event: DragEvent) {
    if (event.dataTransfer?.types?.includes(TOOLBOX_DRAG_MIME)) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
      this.isDragOver = true;
    }
  }

  onCanvasDragLeave() {
    this.isDragOver = false;
  }

  onCanvasDrop(event: DragEvent) {
    this.isDragOver = false;
    const payload = event.dataTransfer?.getData(TOOLBOX_DRAG_MIME);
    if (!payload) {
      return;
    }
    event.preventDefault();

    const { x, y } = this.toCanvasPoint(event.clientX, event.clientY);
    const snapped = this.applyGridSnap(x, y);

    // Custom controls are dragged as "Custom:<prefix>:<tag>"
    if (payload.startsWith('Custom:')) {
      const [, prefix, tag] = payload.split(':');
      const lookup = this.registry.find(prefix, tag);
      if (lookup) {
        this.dragDropService.createElementAtPosition(
          ElementType.Custom,
          snapped.x,
          snapped.y,
          this.canvas.nativeElement,
          this.registry.defaultProperties(lookup)
        );
      }
      this.dragDropService.endDrag();
      return;
    }

    this.dragDropService.createElementAtPosition(
      payload as ElementType,
      snapped.x,
      snapped.y,
      this.canvas.nativeElement
    );
    this.dragDropService.endDrag();
  }

  // --- Keyboard shortcuts ----------------------------------------------------

  @HostListener('document:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent) {
    if (this.isEditingText(event.target)) {
      return;
    }

    const ctrl = event.ctrlKey || event.metaKey;

    if (event.code === 'Space') {
      this.spacePressed = true;
    }

    if (ctrl && event.key.toLowerCase() === 'c') {
      event.preventDefault();
      this.clipboardService.copy(this.elementService.getSelectedElements());
      return;
    }

    if (ctrl && event.key.toLowerCase() === 'x') {
      event.preventDefault();
      this.clipboardService.cut(this.elementService.getSelectedElements());
      return;
    }

    if (ctrl && event.key.toLowerCase() === 'v') {
      event.preventDefault();
      this.clipboardService.paste();
      return;
    }

    if (ctrl && event.key.toLowerCase() === 'a') {
      event.preventDefault();
      this.selectAll();
      return;
    }

    if (ctrl && event.key.toLowerCase() === 'z' && !event.shiftKey) {
      event.preventDefault();
      this.elementService.undo();
      return;
    }

    if (ctrl && (event.key.toLowerCase() === 'y' || (event.shiftKey && event.key.toLowerCase() === 'z'))) {
      event.preventDefault();
      this.elementService.redo();
      return;
    }

    if (ctrl && (event.key === ']' || event.key === '[')) {
      event.preventDefault();
      const toFront = event.key === ']';
      this.elementService.reorderSelection(
        event.shiftKey ? (toFront ? 'front' : 'back') : (toFront ? 'forward' : 'backward')
      );
      return;
    }

    const selected = this.elementService.getSelectedElement();
    if (!selected) {
      return;
    }

    if (ctrl && event.key.toLowerCase() === 'd') {
      event.preventDefault();
      this.elementService.duplicateElement(selected);
      return;
    }

    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this.elementService.removeSelectedElements();
      return;
    }

    if (event.key === 'Escape') {
      this.elementService.selectElement(null);
      return;
    }

    const step = event.shiftKey ? 10 : 1;
    const nudges: Record<string, { x: number, y: number }> = {
      ArrowUp: { x: 0, y: -step },
      ArrowDown: { x: 0, y: step },
      ArrowLeft: { x: -step, y: 0 },
      ArrowRight: { x: step, y: 0 }
    };

    const nudge = nudges[event.key];
    if (nudge) {
      event.preventDefault();
      this.elementService.nudgeSelection(nudge.x, nudge.y);
    }
  }

  /** Applies grid snapping when it is enabled. */
  private applyGridSnap(x: number, y: number): { x: number, y: number } {
    if (!this.viewport.snapToGrid) {
      return { x: Math.round(x), y: Math.round(y) };
    }
    return {
      x: this.alignmentService.snapToGrid(x, this.viewport.gridSize),
      y: this.alignmentService.snapToGrid(y, this.viewport.gridSize)
    };
  }

  /**
   * Turns a raw cdkDrag position into the final canvas position, applying
   * smart guides first and the grid afterwards.
   */
  /** Applies smart guides and grid snapping to a position expressed in the parent's own coordinates. */
  private resolveDragPosition(element: MauiElement, rawX: number, rawY: number): { x: number, y: number } {
    let x = rawX;
    let y = rawY;

    if (element.parent?.type === ElementType.AbsoluteLayout) {
      if (this.viewport.showGuides) {
        const snapped = this.alignmentService.computeGuides(element, x, y);
        x = snapped.x;
        y = snapped.y;
      }
      const grid = this.applyGridSnap(x, y);
      x = grid.x;
      y = grid.y;
    }

    return { x, y };
  }

  @HostListener('document:keyup', ['$event'])
  onKeyUp(event: KeyboardEvent) {
    if (event.code === 'Space') {
      this.spacePressed = false;
    }
  }

  /** Selects every direct child of the root layout. */
  selectAll() {
    this.elementService.setSelection([...this.elementService.getRootElement().children]);
  }

  private isEditingText(target: EventTarget | null): boolean {
    const element = target as HTMLElement | null;
    if (!element) {
      return false;
    }
    const tag = element.tagName?.toLowerCase();
    return tag === 'input' || tag === 'textarea' || tag === 'select' || element.isContentEditable === true;
  }

  // Set DOM element reference for position calculations
  setElementRef(element: MauiElement, domElement: HTMLElement) {
    element.domElement = domElement;
  }

  getElementStyles(element: MauiElement): any {
    const props = element.properties;
    
    const styles: any = {
      width: props.width + 'px',
      height: props.height + 'px',
      // Only lift the element while it is actively being resized. Lifting it for
      // the whole time it is selected would hide the effect of send-to-back:
      // the user restacks an element, sees nothing move, and assumes it failed.
      // Dragging is covered by `.cdk-drag-dragging` in the stylesheet.
      zIndex: this.isSelected(element) && this.isResizing ? 9999 : 'auto',
      transform: `translate3d(${props.x}px, ${props.y}px, 0px)`
    };

    if (props.backgroundColor) {
      styles.backgroundColor = props.backgroundColor;
    }

    if (props.textColor) {
      styles.color = props.textColor;
    }

    if (props.fontSize) {
      styles.fontSize = props.fontSize + 'px';
    }

    if (props.margin) {
      const m = props.margin;
      styles.margin = `${m.top}px ${m.right}px ${m.bottom}px ${m.left}px`;
    }

    if (props.padding) {
      const p = props.padding;
      styles.padding = `${p.top}px ${p.right}px ${p.bottom}px ${p.left}px`;
    }

    return styles;
  }

  isLayoutElement(element: MauiElement): boolean {
    if (element.type === ElementType.Custom) {
      return this.registry.findForElement(element)?.definition.canHaveChildren ?? false;
    }
    return [
      ElementType.StackLayout,
      ElementType.Grid,
      ElementType.AbsoluteLayout,
      ElementType.VerticalStackLayout,
      ElementType.Frame,
      ElementType.Border,
      ElementType.CollectionView,
      ElementType.ScrollView
    ].includes(element.type);
  }

  // --- Custom control previews ------------------------------------------------

  /** Preview description of a custom control, with sensible fallbacks. */
  customPreview(element: MauiElement): CustomPreview {
    const definition = this.registry.findForElement(element)?.definition;
    return definition?.preview || { kind: 'box', label: element.properties.customTag || 'Custom', icon: 'extension' };
  }

  customLabel(element: MauiElement): string {
    const preview = this.customPreview(element);
    const label = this.registry.interpolate(preview.label, element).trim();
    return label || element.properties.customTag || 'Custom';
  }

  customCornerRadius(element: MauiElement): number | null {
    const radius = Number(this.registry.interpolate(this.customPreview(element).cornerRadius, element));
    return Number.isFinite(radius) && radius > 0 ? radius : null;
  }

  // --- Control previews -------------------------------------------------------

  sliderPercent(element: MauiElement): number {
    const props = element.properties;
    const min = props.minimum ?? 0;
    const max = props.maximum ?? 100;
    const value = props.value ?? min;
    if (max === min) {
      return 0;
    }
    return Math.min(100, Math.max(0, ((value - min) / (max - min)) * 100));
  }

  progressPercent(element: MauiElement): number {
    return Math.min(100, Math.max(0, (element.properties.progress ?? 0) * 100));
  }

  collectionPreviewItems(element: MauiElement): number[] {
    const count = Math.min(20, Math.max(1, element.properties.itemCount ?? 3));
    return Array.from({ length: count }, (_, index) => index);
  }

  collectionItemLabel(element: MauiElement): string {
    return element.properties.itemTemplateText || 'Item';
  }

  isSelected(element: MauiElement): boolean {
    return this.elementService.isElementSelected(element);
  }

  /** Handles (and dragging) are only offered for a single selected element. */
  isPrimarySelection(element: MauiElement): boolean {
    return this.elementService.getSelectedElement() === element
      && this.elementService.getSelectedElements().length === 1;
  }

  // Select element on pointerdown so cdkDrag will be enabled when drag starts.
  onElementPointerDown(element: MauiElement, event: PointerEvent) {
    // Prevent the pointer event from bubbling to parent elements which may start a drag
    event.stopPropagation();
  }
  
  onDragStarted(element: MauiElement) {
    // Remember where the element started so the drop position is origin + travelled distance
    this.dragOrigin = { x: element.properties.x || 0, y: element.properties.y || 0 };
  }

  onDragEnded(element: MauiElement, event: CdkDragEnd) {
    document.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
    this.alignmentGuides = [];

    const zoom = this.viewport.zoom || 1;
    const origin = this.dragOrigin ?? { x: element.properties.x || 0, y: element.properties.y || 0 };
    this.dragOrigin = null;

    // The pointer position decides which layout receives the element
    const pointer = this.toCanvasPoint(event.dropPoint.x, event.dropPoint.y);

    let target: { x: number, y: number };
    if (this.parentUsesAbsolutePositioning(element)) {
      // cdkDrag reports the travelled distance in client pixels; the model stores unscaled pixels
      const local = this.resolveDragPosition(
        element,
        origin.x + event.distance.x / zoom,
        origin.y + event.distance.y / zoom
      );
      target = this.toCanvasSpace(element.parent, local.x, local.y);
    } else {
      // Stack and grid children have no pixel position of their own, so follow the pointer
      target = pointer;
    }

    // Drop the transform cdkDrag applied - the element is repositioned through its own styles,
    // and a leftover translation would offset every following drag.
    event.source.reset();


    // The browser still delivers a click after the drop. By then the element has moved, so the
    // click lands on whatever is now under the pointer and would steal the selection. The flag is
    // cleared by the next press rather than by a timer, because the click can be delivered late.
    this.suppressNextCanvasClick = true;

    this.dragDropService.handleCanvasDrop(element, target.x, target.y, this.canvas.nativeElement, pointer);
    this.dragDropService.endDrag();
  }

  private parentUsesAbsolutePositioning(element: MauiElement): boolean {
    const parent = element.parent;
    return !!parent && this.layoutDesigner.getLayoutInfo(parent.type).supportsAbsolutePositioning;
  }

  /** Converts a point expressed in a layout's own coordinates into canvas space. */
  private toCanvasSpace(parent: MauiElement | undefined, localX: number, localY: number): { x: number, y: number } {
    const dom = parent?.domElement;
    if (!parent || parent.id === 'root' || !dom) {
      return { x: localX, y: localY };
    }

    const zoom = this.viewport.zoom || 1;
    const rect = dom.getBoundingClientRect();
    const canvasRect = this.canvas.nativeElement.getBoundingClientRect();
    return {
      x: localX + (rect.left - canvasRect.left) / zoom,
      y: localY + (rect.top - canvasRect.top) / zoom
    };
  }

  onDragMoved(element: MauiElement, event: CdkDragMove) {
    document.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));

    // Smart guides follow the element while it is dragged
    if (this.viewport.showGuides && element.parent?.type === ElementType.AbsoluteLayout) {
      const zoom = this.viewport.zoom || 1;
      const proposedX = (element.properties.x || 0) + event.distance.x / zoom;
      const proposedY = (element.properties.y || 0) + event.distance.y / zoom;
      this.alignmentGuides = this.alignmentService.computeGuides(element, proposedX, proposedY).guides;
    } else {
      this.alignmentGuides = [];
    }
    // The layout under the pointer is the drop candidate
    const pointer = this.toCanvasPoint(event.pointerPosition.x, event.pointerPosition.y);
    const layoutOver = this.dragDropService.findLayoutAtPosition(pointer.x, pointer.y, this.canvas.nativeElement, element)!;

    if(layoutOver?.type === ElementType.Grid && this.layoutDesigner.getVisualHints(layoutOver).showGrid) {
      const zoom = this.viewport.zoom || 1;
      const parentRect = layoutOver.domElement?.getBoundingClientRect();

      const x = (event.pointerPosition.x - (parentRect?.left || 0)) / zoom;
      const y = (event.pointerPosition.y - (parentRect?.top || 0)) / zoom;
      const gridCell = this.layoutDesigner.getGridCellAtPosition(layoutOver, x, y, layoutOver.domElement!);
      if(gridCell) {
        this.highlightedGridCell = { element: layoutOver, row: gridCell.row, column: gridCell.column };
        this.dropZonePreview = { element: layoutOver, visible: true };
      } else {
        this.highlightedGridCell = null;
        this.dropZonePreview = null;
      }
    } else {
      this.highlightedGridCell = null;
      this.dropZonePreview = null;
    }

    layoutOver?.domElement?.classList.add('drag-over');
  }

  onElementDroppedOnLayout(targetLayout: MauiElement, event: CdkDragDrop<MauiElement[]>) {
    console.log("Element dropped on layout:", targetLayout, event);
    
    if (event.previousContainer === event.container) {
      // Moving within the same container
      return;
    }
    
    const draggedElement = event.item.data as MauiElement;
    
    if (draggedElement && this.dragDropService.canDropOn(targetLayout, draggedElement)) {
      // Calculate drop position based on the event
      const dropX = event.dropPoint?.x || 0;
      const dropY = event.dropPoint?.y || 0;
      
      // Use the drag-drop service to handle the move
      this.dragDropService.handleElementMove(draggedElement, dropX, dropY, targetLayout);
    }
  }

  // Resize handle interactions
  onResizeStart(event: MouseEvent, direction: ResizeDirection, element: MauiElement) {
    event.preventDefault();
    event.stopPropagation();
    
    this.isResizing = true;
    this.elementService.beginBatch();
    this.resizeDirection = direction;
    this.resizeElement = element;
    this.startMouseX = event.clientX;
    this.startMouseY = event.clientY;
    
    const props = element.properties;
    this.startX = props.x || 0;
    this.startY = props.y || 0;
    this.startWidth = props.width || 100;
    this.startHeight = props.height || 30;
    
    // Show size display
    this.showSizeDisplay = true;
    this.updateSizeDisplay(event);
    
    // Set cursor
    document.body.style.cursor = this.getResizeCursor(direction);
    document.body.style.userSelect = 'none';
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent) {
    if (this.isPanning) {
      this.viewportService.panBy(event.clientX - this.panOrigin.x, event.clientY - this.panOrigin.y);
      this.panOrigin = { x: event.clientX, y: event.clientY };
      return;
    }

    if (this.isMarqueeSelecting) {
      this.updateMarquee(event);
      return;
    }

    if (!this.isResizing || !this.resizeDirection || !this.resizeElement) return;

    const deltaX = event.clientX - this.startMouseX;
    const deltaY = event.clientY - this.startMouseY;

    let newX = this.startX;
    let newY = this.startY;
    let newWidth = this.startWidth;
    let newHeight = this.startHeight;

    // Calculate new dimensions based on resize direction
    switch (this.resizeDirection) {
      case 'nw': // Top-left corner
        newX = this.startX + deltaX;
        newY = this.startY + deltaY;
        newWidth = this.startWidth - deltaX;
        newHeight = this.startHeight - deltaY;
        break;
      case 'ne': // Top-right corner
        newY = this.startY + deltaY;
        newWidth = this.startWidth + deltaX;
        newHeight = this.startHeight - deltaY;
        break;
      case 'sw': // Bottom-left corner
        newX = this.startX + deltaX;
        newWidth = this.startWidth - deltaX;
        newHeight = this.startHeight + deltaY;
        break;
      case 'se': // Bottom-right corner
        newWidth = this.startWidth + deltaX;
        newHeight = this.startHeight + deltaY;
        break;
      case 'n': // Top edge
        newY = this.startY + deltaY;
        newHeight = this.startHeight - deltaY;
        break;
      case 's': // Bottom edge
        newHeight = this.startHeight + deltaY;
        break;
      case 'w': // Left edge
        newX = this.startX + deltaX;
        newWidth = this.startWidth - deltaX;
        break;
      case 'e': // Right edge
        newWidth = this.startWidth + deltaX;
        break;
    }

    // Apply minimum size constraints
    if (newWidth < this.MIN_SIZE) {
      if (this.resizeDirection.includes('w')) {
        newX = this.startX + this.startWidth - this.MIN_SIZE;
      }
      newWidth = this.MIN_SIZE;
    }
    
    if (newHeight < this.MIN_SIZE) {
      if (this.resizeDirection.includes('n')) {
        newY = this.startY + this.startHeight - this.MIN_SIZE;
      }
      newHeight = this.MIN_SIZE;
    }

    // Apply grid cell constraints if element is in a grid
    if (this.resizeElement.parent && this.resizeElement.parent.type === ElementType.Grid) {
      const gridElement = this.resizeElement.parent;
      const gridElementRef = gridElement.domElement;
      
      if (gridElementRef) {
        const maxDimensions = this.layoutDesigner.getGridChildMaxDimensions(
          this.resizeElement,
          gridElement,
          gridElementRef
        );
        
        if (maxDimensions) {
          // Constrain width to grid cell boundaries
          if (newWidth > maxDimensions.maxWidth) {
            if (this.resizeDirection.includes('w')) {
              newX = this.startX + this.startWidth - maxDimensions.maxWidth;
            }
            newWidth = maxDimensions.maxWidth;
          }
          
          // Constrain height to grid cell boundaries
          if (newHeight > maxDimensions.maxHeight) {
            if (this.resizeDirection.includes('n')) {
              newY = this.startY + this.startHeight - maxDimensions.maxHeight;
            }
            newHeight = maxDimensions.maxHeight;
          }
        }
      }
    }

    // Update element properties (folded into a single history entry)
    this.elementService.updateElementProperties(this.resizeElement, {
      x: newX,
      y: newY,
      width: newWidth,
      height: newHeight
    }, { recordHistory: false });

    // Update size display
    this.updateSizeDisplay(event);
  }

  @HostListener('document:mouseup', ['$event'])
  onMouseUp(event: MouseEvent) {
    if (this.isPanning) {
      this.isPanning = false;
      return;
    }

    if (this.isMarqueeSelecting) {
      this.finishMarquee();
      return;
    }

    if (this.isResizing) {
      this.isResizing = false;
      this.elementService.endBatch();
      this.resizeDirection = null;
      this.resizeElement = null;
      this.showSizeDisplay = false;
      
      // Reset cursor
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    }
  }

  private updateSizeDisplay(event: MouseEvent) {
    if (!this.resizeElement) return;
    
    const props = this.resizeElement.properties;
    this.sizeDisplayText = `${Math.round(props.width || 0)} × ${Math.round(props.height || 0)}`;
    this.sizeDisplayX = event.clientX + 10;
    this.sizeDisplayY = event.clientY - 30;
  }

  private getResizeCursor(direction: ResizeDirection): string {
    switch (direction) {
      case 'nw':
      case 'se':
        return 'nw-resize';
      case 'ne':
      case 'sw':
        return 'ne-resize';
      case 'n':
      case 's':
        return 'ns-resize';
      case 'w':
      case 'e':
        return 'ew-resize';
      default:
        return 'default';
    }
  }

  // Layout-specific hover and drop methods
  onLayoutHover(event: MouseEvent, element: MauiElement) {

    if (element.type === ElementType.Grid) {
      // The event bubbles, so event.target is whatever sits under the pointer -
      // often a child element, whose rect would put the pointer in the wrong
      // cell. The grid's own element is the only correct frame of reference.
      const container = element.domElement ?? (event.currentTarget as HTMLElement | null);
      if (!container) {
        return;
      }

      const rect = container.getBoundingClientRect();
      const zoom = this.viewport.zoom || 1;
      const x = (event.clientX - rect.left) / zoom;
      const y = (event.clientY - rect.top) / zoom;

      const gridCell = this.layoutDesigner.getGridCellAtPosition(element, x, y, container);
      if (gridCell) {
        this.highlightedGridCell = { element, row: gridCell.row, column: gridCell.column };
      }
    }
  }

  onLayoutHoverExit(element: MauiElement) {
    if (element.type === ElementType.Grid) {
      this.highlightedGridCell = null;
    }
    this.dropZonePreview = null;
  }

  getGridCellStyles(element: MauiElement, rowIndex: number, columnIndex: number): any {
    if (this.highlightedGridCell && 
        this.highlightedGridCell.element === element &&
        this.highlightedGridCell.row === rowIndex && 
        this.highlightedGridCell.column === columnIndex) {
      return {
        backgroundColor: 'rgba(0, 123, 255, 0.2)',
        border: '2px dashed #007bff'
      };
    }
    return {};
  }

  // Grid-related methods
  isGridElement(element: MauiElement): boolean {
    return element.type === ElementType.Grid;
  }

  shouldShowGrid(element: MauiElement): boolean {
    return this.isGridElement(element) && this.layoutDesigner.getVisualHints(element).showGrid;
  }

  getGridDimensions(element: MauiElement): { rows: number, columns: number } {
    const gridDefinition = element.properties.gridDefinition || {
      rows: [{ height: { value: 1, type: 'Star' } }, { height: { value: 1, type: 'Star' } }],
      columns: [{ width: { value: 1, type: 'Star' } }, { width: { value: 1, type: 'Star' } }]
    };
    
    return {
      rows: gridDefinition.rows.length,
      columns: gridDefinition.columns.length
    };
  }

  getGridCellIndices(element: MauiElement): number[] {
    const dims = this.getGridDimensions(element);
    const totalCells = dims.rows * dims.columns;
    return Array(totalCells).fill(0).map((_, i) => i);
  }

  getGridTemplateColumns(element: MauiElement): string {
    const gridDef = element.properties.gridDefinition;
    if (!gridDef) return 'repeat(2, 1fr)'; // Default 2 equal columns
    return gridDef.columns.map(col => 
      col.width.type === 'Star' ? `${col.width.value}fr` : 
      col.width.type === 'Auto' ? 'auto' : 
      `${col.width.value}px`
    ).join(' ');
  }

  getGridTemplateRows(element: MauiElement): string {
    const gridDef = element.properties.gridDefinition;
    if (!gridDef) return 'repeat(2, 1fr)'; // Default 2 equal rows
    return gridDef.rows.map(row => 
      row.height.type === 'Star' ? `${row.height.value}fr` : 
      row.height.type === 'Auto' ? 'auto' : 
      `${row.height.value}px`
    ).join(' ');
  }

  getGridRowPosition(index: number, element: MauiElement): string {
    const dims = this.getGridDimensions(element);
    const rowIndex = Math.floor(index / dims.columns) + 1;
    return rowIndex.toString();
  }

  getGridColumnPosition(index: number, element: MauiElement): string {
    const dims = this.getGridDimensions(element);
    const colIndex = (index % dims.columns) + 1;
    return colIndex.toString();
  }

  isHighlightedCell(element: MauiElement, index: number): boolean {
    if (!this.highlightedGridCell || this.highlightedGridCell.element !== element) return false;
    const dims = this.getGridDimensions(element);
    const row = Math.floor(index / dims.columns);
    const col = index % dims.columns;
    return this.highlightedGridCell.row === row && this.highlightedGridCell.column === col;
  }
}
