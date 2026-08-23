import { TestBed } from '@angular/core/testing';

import { LayoutDesignerService } from './layout-designer';
import { ElementType, GridDefinition, MauiElement } from '../models/maui-element';

/**
 * Grid hit-testing decides which cell a drop lands in, so an off-by-one here
 * silently writes the wrong Grid.Row/Grid.Column into the generated XAML.
 */
describe('LayoutDesignerService grid hit-testing', () => {
  let service: LayoutDesignerService;
  const containers: HTMLElement[] = [];

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    service = TestBed.inject(LayoutDesignerService);
  });

  afterEach(() => {
    while (containers.length) {
      containers.pop()!.remove();
    }
  });

  function grid(definition?: GridDefinition): MauiElement {
    return {
      id: 'grid',
      type: ElementType.Grid,
      properties: definition ? { gridDefinition: definition } : {},
      children: []
    } as unknown as MauiElement;
  }

  /**
   * Builds a real grid with a real cell overlay and attaches it to the
   * document, because the service measures the rendered tracks and detached
   * nodes report every size as zero.
   */
  function renderGrid(columns: string, rows: string, width: number, height: number): HTMLElement {
    const host = document.createElement('div');
    host.style.cssText = `display:grid;position:relative;width:${width}px;height:${height}px;` +
      `grid-template-columns:${columns};grid-template-rows:${rows};`;

    const overlay = document.createElement('div');
    overlay.className = 'grid-visualization';
    overlay.style.cssText = 'position:absolute;inset:0;display:grid;' +
      `grid-template-columns:${columns};grid-template-rows:${rows};`;

    const columnCount = columns.trim().split(/\s+/).length;
    const rowCount = rows.trim().split(/\s+/).length;
    for (let i = 0; i < columnCount * rowCount; i++) {
      const cell = document.createElement('div');
      cell.className = 'grid-cell';
      overlay.appendChild(cell);
    }

    host.appendChild(overlay);
    document.body.appendChild(host);
    containers.push(host);
    return host;
  }

  it('picks the cell under the pointer in an even grid', () => {
    const container = renderGrid('1fr 1fr', '1fr 1fr', 200, 100);
    const element = grid();

    expect(service.getGridCellAtPosition(element, 10, 10, container)).toEqual({ row: 0, column: 0 });
    expect(service.getGridCellAtPosition(element, 150, 10, container)).toEqual({ row: 0, column: 1 });
    expect(service.getGridCellAtPosition(element, 150, 80, container)).toEqual({ row: 1, column: 1 });
  });

  it('respects uneven tracks instead of assuming equal cells', () => {
    // Columns are 20% / 80%. Dividing the width evenly would put x=150 in the
    // second column too, so the assertion that matters is x=60: evenly split it
    // reads as column 0, but the first column actually ends at 40px.
    const container = renderGrid('1fr 4fr', '1fr 1fr', 200, 100);
    const element = grid({
      columns: [{ width: { value: 1, type: 'Star' } }, { width: { value: 4, type: 'Star' } }],
      rows: [{ height: { value: 1, type: 'Star' } }, { height: { value: 1, type: 'Star' } }]
    } as GridDefinition);

    expect(service.getGridCellAtPosition(element, 10, 10, container)!.column).toBe(0);
    expect(service.getGridCellAtPosition(element, 60, 10, container)!.column).toBe(1);
  });

  it('handles absolute tracks', () => {
    const container = renderGrid('50px 1fr', '1fr', 200, 100);
    const element = grid({
      columns: [{ width: { value: 50, type: 'Absolute' } }, { width: { value: 1, type: 'Star' } }],
      rows: [{ height: { value: 1, type: 'Star' } }]
    } as GridDefinition);

    expect(service.getGridCellAtPosition(element, 49, 10, container)!.column).toBe(0);
    expect(service.getGridCellAtPosition(element, 51, 10, container)!.column).toBe(1);
  });

  it('clamps a pointer outside the grid to a real cell', () => {
    const container = renderGrid('1fr 1fr', '1fr 1fr', 200, 100);
    const element = grid();

    // Negative coordinates used to floor to -1, which becomes Grid.Column="-1".
    expect(service.getGridCellAtPosition(element, -30, -30, container)).toEqual({ row: 0, column: 0 });
    expect(service.getGridCellAtPosition(element, 9999, 9999, container)).toEqual({ row: 1, column: 1 });
  });

  it('returns nothing for layouts that have no cells', () => {
    const container = renderGrid('1fr', '1fr', 100, 100);
    const stack = { id: 's', type: ElementType.VerticalStackLayout, properties: {}, children: [] } as unknown as MauiElement;

    expect(service.getGridCellAtPosition(stack, 10, 10, container)).toBeNull();
  });

  it('sizes a child from its own row and column, not the whole grid', () => {
    const container = renderGrid('50px 1fr', '1fr 1fr', 200, 100);
    const element = grid({
      columns: [{ width: { value: 50, type: 'Absolute' } }, { width: { value: 1, type: 'Star' } }],
      rows: [{ height: { value: 1, type: 'Star' } }, { height: { value: 1, type: 'Star' } }]
    } as GridDefinition);

    const child = { id: 'c', type: ElementType.Label, properties: { row: 0, column: 1 }, children: [] } as unknown as MauiElement;
    const size = service.getGridChildMaxDimensions(child, element, container)!;

    expect(size.maxWidth).toBeCloseTo(150, 0);
    expect(size.maxHeight).toBeCloseTo(50, 0);
  });

  it('lets a spanning child cover every track it spans', () => {
    const container = renderGrid('1fr 1fr', '1fr 1fr', 200, 100);
    const element = grid();
    const child = {
      id: 'c',
      type: ElementType.Label,
      properties: { row: 0, column: 0, columnSpan: 2, rowSpan: 2 },
      children: []
    } as unknown as MauiElement;

    const size = service.getGridChildMaxDimensions(child, element, container)!;

    expect(size.maxWidth).toBeCloseTo(200, 0);
    expect(size.maxHeight).toBeCloseTo(100, 0);
  });

  it('rebases a drop onto the container it was dropped into', () => {
    const container = renderGrid('1fr 1fr', '1fr 1fr', 200, 100);
    // Offset the grid so a viewport coordinate and a container-relative one
    // cannot be confused: the drop is in the container's second column even
    // though its viewport x is small.
    container.style.position = 'absolute';
    container.style.left = '120px';
    container.style.top = '40px';

    const element = grid();
    const rect = container.getBoundingClientRect();
    const event = { clientX: rect.left + 150, clientY: rect.top + 80 } as MouseEvent;

    expect(service.calculateDropPosition(element, event, container)).toEqual({ x: 1, y: 1 });
  });
});
