import { Injectable } from '@angular/core';
import { MauiElement, ElementType, GridDefinition, GridLength, GridLengthType, LayoutOptions } from '../models/maui-element';

export interface LayoutInfo {
  canHaveChildren: boolean;
  supportsDragDrop: boolean;
  supportsAbsolutePositioning: boolean;
  supportsGridPositioning: boolean;
}

/**
 * The implicit 2x2 grid used when an element has no explicit definition.
 *
 * Shared so hit-testing and sizing cannot disagree about the fallback.
 */
export const DEFAULT_GRID_DEFINITION: GridDefinition = {
  rows: [{ height: { value: 1, type: GridLengthType.Star } }, { height: { value: 1, type: GridLengthType.Star } }],
  columns: [{ width: { value: 1, type: GridLengthType.Star } }, { width: { value: 1, type: GridLengthType.Star } }]
};

@Injectable({
  providedIn: 'root'
})
export class LayoutDesignerService {

  constructor() { }

  getLayoutInfo(elementType: ElementType): LayoutInfo {
    switch (elementType) {
      case ElementType.AbsoluteLayout:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: true,
          supportsGridPositioning: false
        };
      
      case ElementType.Grid:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: true
        };
      
      case ElementType.StackLayout:
      case ElementType.VerticalStackLayout:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: false
        };
      
      case ElementType.Frame:
      case ElementType.ScrollView:
        return {
          canHaveChildren: true,
          supportsDragDrop: true,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: false
        };
      
      default:
        return {
          canHaveChildren: false,
          supportsDragDrop: false,
          supportsAbsolutePositioning: false,
          supportsGridPositioning: false
        };
    }
  }

  calculateDropPosition(parentElement: MauiElement, event: MouseEvent, containerElement: HTMLElement | null): { x: number, y: number } {
   
    if (!containerElement) {
      // Fallback for cases where container element is not available
      return { x: event.clientX, y: event.clientY };
    }
    
    // clientX/Y are viewport coordinates, so they have to be rebased onto the
    // container. properties.x/y are the element's position within *its own*
    // parent, which is a different space entirely and lands the drop in the
    // wrong cell whenever the layout is not at the canvas origin.
    const rect = containerElement.getBoundingClientRect();
    const scale = rect.width && containerElement.offsetWidth
      ? containerElement.offsetWidth / rect.width
      : 1;
    const x = (event.clientX - rect.left) * scale;
    const y = (event.clientY - rect.top) * scale;
    
    const layoutInfo = this.getLayoutInfo(parentElement.type);
    
    if (layoutInfo.supportsAbsolutePositioning) {
      return { x, y };
    } else if (layoutInfo.supportsGridPositioning) {
      // For grid layouts, calculate grid cell
      
      const gridCell = this.getGridCellAtPosition(parentElement, x, y, containerElement);
      return { x: gridCell!.column, y: gridCell!.row };
    } else {
      // For stack layouts, position is managed by the layout
      return { x: 0, y: 0 };
    }
  }

  private calculateGridPosition(gridElement: MauiElement, x: number, y: number, containerElement: HTMLElement): { x: number, y: number } {
    const gridDefinition = gridElement.properties.gridDefinition || DEFAULT_GRID_DEFINITION;

    // offsetWidth/Height are unscaled, so grid maths stay correct while the canvas is zoomed
    const rect = containerElement.getBoundingClientRect();
    const width = containerElement.offsetWidth || rect.width;
    const height = containerElement.offsetHeight || rect.height;

    const columnSizes = this.measureTracks(containerElement, 'column')
      ?? this.calculateGridSizes(gridDefinition.columns.map(c => c.width), width);
    const rowSizes = this.measureTracks(containerElement, 'row')
      ?? this.calculateGridSizes(gridDefinition.rows.map(r => r.height), height);

    return {
      x: this.trackAtOffset(columnSizes, x),
      y: this.trackAtOffset(rowSizes, y)
    };
  }

  /**
   * Find the track containing an offset, given the size of each track.
   *
   * Tracks are rarely equal - a Grid can mix Star, Absolute and Auto - so the
   * boundaries have to be accumulated rather than derived by division.
   */
  private trackAtOffset(sizes: number[], offset: number): number {
    let edge = 0;
    for (let index = 0; index < sizes.length; index++) {
      edge += sizes[index];
      if (offset < edge) {
        // Clamp below: a pointer left of/above the grid belongs to the first
        // track, and a negative index would generate invalid XAML.
        return Math.max(0, index);
      }
    }

    return Math.max(0, sizes.length - 1);
  }

  /**
   * Measure the rendered grid tracks, or null when the overlay is not laid out.
   *
   * The cell overlay is a real CSS grid, so the browser has already resolved
   * Auto tracks and any gap between them. Measuring is therefore exact, where
   * recomputing the sizes here can only ever approximate Auto and would have to
   * duplicate the gap. Sizes are read from offsetWidth/offsetHeight, which are
   * unscaled, so the result does not change as the canvas is zoomed.
   */
  private measureTracks(containerElement: HTMLElement, axis: 'column' | 'row'): number[] | null {
    const overlay = containerElement.querySelector<HTMLElement>(':scope > .grid-visualization');
    const cells = overlay ? Array.from(overlay.children) as HTMLElement[] : [];
    if (!cells.length) {
      return null;
    }

    // One row of cells gives every column width, and one column gives every
    // row height. Deduplicate by start offset to pick a single line out.
    const starts = new Map<number, number>();
    for (const cell of cells) {
      const start = axis === 'column' ? cell.offsetLeft : cell.offsetTop;
      const size = axis === 'column' ? cell.offsetWidth : cell.offsetHeight;
      if (!starts.has(start)) {
        starts.set(start, size);
      }
    }

    const ordered = [...starts.entries()].sort((a, b) => a[0] - b[0]);
    if (!ordered.length || ordered.every(([, size]) => size === 0)) {
      return null;
    }

    // Stretch each track to the start of the next one so any gap belongs to a
    // cell. Without this, hovering a gap highlights whichever cell was last.
    return ordered.map(([start, size], index) =>
      index < ordered.length - 1 ? ordered[index + 1][0] - start : size);
  }

  getChildLayoutProperties(parent: MauiElement, child: MauiElement, position: { x: number, y: number }): Partial<MauiElement['properties']> {
    const layoutInfo = this.getLayoutInfo(parent.type);
    
    if (layoutInfo.supportsAbsolutePositioning) {
      return {
        x: position.x,
        y: position.y
      };
    } else if (layoutInfo.supportsGridPositioning) {
      return {
        column: position.x,
        row: position.y,
        x: 0, // Reset absolute positioning in grid
        y: 0
      };
    } else {
      // For stack layouts, clear absolute positioning
      return {
        x: 0,
        y: 0
      };
    }
  }

  canDropOn(targetElement: MauiElement, droppedElement?: MauiElement): boolean {
    const layoutInfo = this.getLayoutInfo(targetElement.type);
    
    if (!layoutInfo.canHaveChildren) {
      return false;
    }
    
    if (droppedElement) {
      // Prevent dropping an element on itself or its descendants
      return !this.isDescendant(targetElement, droppedElement);
    }
    
    return true;
  }

  private isDescendant(potential: MauiElement, ancestor: MauiElement): boolean {
    let current = potential.parent;
    while (current) {
      if (current === ancestor) {
        return true;
      }
      current = current.parent;
    }
    return false;
  }

  getVisualHints(element: MauiElement): { showGrid: boolean, showDropZones: boolean } {
    const layoutInfo = this.getLayoutInfo(element.type);
    
    return {
      showGrid: layoutInfo.supportsGridPositioning,
      showDropZones: layoutInfo.supportsDragDrop
    };
  }

  /**
   * Calculate which grid cell would be highlighted during hover
   */
  getGridCellAtPosition(gridElement: MauiElement, x: number, y: number, containerElement: HTMLElement): { row: number, column: number } | null {
    if (!this.getLayoutInfo(gridElement.type).supportsGridPositioning) {
      return null;
    }

    const position = this.calculateGridPosition(gridElement, x, y, containerElement);
    return { row: position.y, column: position.x };
  }

  /**
   * For stack layouts, determine insertion index based on position
   */
  getStackInsertionIndex(stackElement: MauiElement, x: number, y: number, containerElement: HTMLElement): number {
    const layoutInfo = this.getLayoutInfo(stackElement.type);
    if (layoutInfo.supportsAbsolutePositioning || layoutInfo.supportsGridPositioning) {
      return stackElement.children.length; // Append at end for non-stack layouts
    }

    // VerticalStackLayout is always vertical
    const isVertical = stackElement.type === ElementType.VerticalStackLayout || 
                      stackElement.properties.orientation !== 'Horizontal';
    const rect = containerElement.getBoundingClientRect();
    
    let insertionIndex = 0;
    
    if (isVertical) {
      // For vertical stack, use Y position
      const relativeY = y / rect.height;
      insertionIndex = Math.floor(relativeY * (stackElement.children.length + 1));
    } else {
      // For horizontal stack, use X position
      const relativeX = x / rect.width;
      insertionIndex = Math.floor(relativeX * (stackElement.children.length + 1));
    }
    
    return Math.min(insertionIndex, stackElement.children.length);
  }

  /**
   * Calculate the maximum allowed dimensions for a grid child element
   * based on its grid position and span
   */
  getGridChildMaxDimensions(childElement: MauiElement, gridElement: MauiElement, gridContainerElement: HTMLElement): { maxWidth: number, maxHeight: number } | null {
    if (!this.getLayoutInfo(gridElement.type).supportsGridPositioning) {
      return null;
    }

    const gridDefinition = gridElement.properties.gridDefinition || DEFAULT_GRID_DEFINITION;

    const rect = gridContainerElement.getBoundingClientRect();
    // Unscaled, so a zoomed canvas does not shrink the size cap it computes.
    const width = gridContainerElement.offsetWidth || rect.width;
    const height = gridContainerElement.offsetHeight || rect.height;
    const totalColumns = gridDefinition.columns.length;
    const totalRows = gridDefinition.rows.length;

    // Get child's grid position and span
    const column = childElement.properties.column || 0;
    const row = childElement.properties.row || 0;
    const columnSpan = childElement.properties.columnSpan || 1;
    const rowSpan = childElement.properties.rowSpan || 1;

    // Prefer the rendered tracks for the same reason hit-testing does: they
    // already account for Auto sizing and the gap between cells.
    const columnWidths = this.measureTracks(gridContainerElement, 'column')
      ?? this.calculateGridSizes(gridDefinition.columns.map(c => c.width), width);
    const rowHeights = this.measureTracks(gridContainerElement, 'row')
      ?? this.calculateGridSizes(gridDefinition.rows.map(r => r.height), height);

    // Calculate max width by summing up the widths of spanned columns
    let maxWidth = 0;
    for (let i = column; i < Math.min(column + columnSpan, totalColumns); i++) {
      maxWidth += columnWidths[i];
    }

    // Calculate max height by summing up the heights of spanned rows
    let maxHeight = 0;
    for (let i = row; i < Math.min(row + rowSpan, totalRows); i++) {
      maxHeight += rowHeights[i];
    }

    return { maxWidth, maxHeight };
  }

  /**
   * Calculate actual sizes for grid columns/rows based on their definitions
   */
  private calculateGridSizes(definitions: { value: number, type: string }[], totalSize: number): number[] {
    const sizes: number[] = [];
    let starCount = 0;
    let fixedSize = 0;

    // First pass: calculate fixed sizes and count star units
    definitions.forEach(def => {
      if (def.type === 'Absolute') {
        fixedSize += def.value;
      } else if (def.type === 'Star') {
        starCount += def.value;
      } else {
        // Auto needs content measurement, which is only available from the DOM.
        // Treat it as one star so the tracks still sum to the container: the
        // second pass hands Auto a star's worth, so counting it here too is
        // what keeps the total honest.
        starCount += 1;
      }
    });

    // Calculate size per star unit
    const remainingSize = Math.max(0, totalSize - fixedSize);
    const starSize = starCount > 0 ? remainingSize / starCount : 0;

    // Second pass: assign actual sizes
    definitions.forEach(def => {
      if (def.type === 'Star') {
        sizes.push(def.value * starSize);
      } else if (def.type === 'Absolute') {
        sizes.push(def.value);
      } else { // Auto
        sizes.push(starSize); // Fallback to 1 star unit
      }
    });

    return sizes;
  }

  /**
   * CSS `grid-template-*` value for one MAUI GridLength.
   *
   * Star tracks use `minmax(0, Nfr)` so a neighbouring Auto track can shrink
   * them instead of every track pretending to be equal. Absolute is pixels.
   * Auto is `auto` so the track sizes to the child's WidthRequest/HeightRequest.
   */
  trackCss(length: GridLength | undefined): string {
    if (!length) {
      return 'minmax(0, 1fr)';
    }
    switch (length.type) {
      case GridLengthType.Auto:
        return 'auto';
      case GridLengthType.Absolute:
        return `${Math.max(0, length.value || 0)}px`;
      case GridLengthType.Star:
      default:
        return `minmax(0, ${length.value || 1}fr)`;
    }
  }

  templateColumnsCss(grid: MauiElement): string {
    const columns = (grid.properties.gridDefinition || DEFAULT_GRID_DEFINITION).columns;
    return columns.map(column => this.trackCss(column.width)).join(' ') || 'minmax(0, 1fr)';
  }

  templateRowsCss(grid: MauiElement): string {
    const rows = (grid.properties.gridDefinition || DEFAULT_GRID_DEFINITION).rows;
    return rows.map(row => this.trackCss(row.height)).join(' ') || 'minmax(0, 1fr)';
  }

  /**
   * CSS grid-row / grid-column placement, 1-based, honouring span.
   * Missing coordinates are treated as 0 so imported XAML without Grid.Row
   * still occupies the first cell instead of `NaN`.
   */
  childPlacement(child: MauiElement): { row: string; column: string } {
    const definition = child.parent?.properties.gridDefinition || DEFAULT_GRID_DEFINITION;
    const rowStart = Math.max(0, child.properties.row || 0);
    const columnStart = Math.max(0, child.properties.column || 0);
    const rowSpan = this.clampSpan(child.properties.rowSpan || 1, definition.rows.length - rowStart);
    const columnSpan = this.clampSpan(child.properties.columnSpan || 1, definition.columns.length - columnStart);
    return {
      row: `${rowStart + 1} / span ${rowSpan}`,
      column: `${columnStart + 1} / span ${columnSpan}`
    };
  }

  private clampSpan(span: number, remaining: number): number {
    if (!Number.isFinite(span) || span < 1) {
      return 1;
    }
    return Math.max(1, Math.min(Math.floor(span), Math.max(1, remaining)));
  }

  /** Maps MAUI LayoutOptions onto CSS justify-self / align-self. */
  selfAlignment(option: LayoutOptions | undefined): string {
    switch (option) {
      case 'Start':
        return 'start';
      case 'Center':
        return 'center';
      case 'End':
        return 'end';
      case 'Fill':
      default:
        return 'stretch';
    }
  }
}
