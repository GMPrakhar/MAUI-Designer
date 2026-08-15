import { TestBed } from '@angular/core/testing';
import { ElementService } from './element';
import { XamlGeneratorService } from './xaml-generator';
import { XamlParserService } from './xaml-parser';
import { ElementType, GridLengthType, Orientation } from '../models/maui-element';

describe('ElementService history and persistence', () => {
  let service: ElementService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ElementService);
    localStorage.clear();
  });

  afterEach(() => localStorage.clear());

  it('undoes and redoes adding an element', () => {
    const root = service.getRootElement();
    service.addElement(service.createElement(ElementType.Label), root);
    expect(service.getRootElement().children.length).toBe(1);

    service.undo();
    expect(service.getRootElement().children.length).toBe(0);

    service.redo();
    expect(service.getRootElement().children.length).toBe(1);
  });

  it('reports whether undo and redo are available', () => {
    expect(service.canUndo()).toBeFalse();
    expect(service.canRedo()).toBeFalse();

    service.addElement(service.createElement(ElementType.Button));
    expect(service.canUndo()).toBeTrue();

    service.undo();
    expect(service.canRedo()).toBeTrue();
  });

  it('folds batched changes into a single history entry', () => {
    const label = service.createElement(ElementType.Label);
    service.addElement(label);

    service.beginBatch();
    service.updateElementProperties(label, { width: 150 });
    service.updateElementProperties(label, { width: 200 });
    service.updateElementProperties(label, { width: 250 });
    service.endBatch();

    service.undo();
    expect(service.getRootElement().children[0].properties.width).toBe(100);
  });

  it('clears the selection when the selected element is removed with its parent', () => {
    const stack = service.createElement(ElementType.VerticalStackLayout);
    service.addElement(stack);
    const label = service.createElement(ElementType.Label);
    service.addElement(label, stack);
    service.selectElement(label);

    service.removeElement(stack);

    expect(service.getSelectedElement()).toBeNull();
  });

  it('never removes the root element', () => {
    service.removeElement(service.getRootElement());
    expect(service.getRootElement()).toBeTruthy();
  });

  it('duplicates an element with a fresh id next to the original', () => {
    const label = service.createElement(ElementType.Label);
    service.addElement(label);
    service.updateElementProperties(label, { text: 'Original' });

    const clone = service.duplicateElement(label)!;

    expect(clone.id).not.toBe(label.id);
    expect(clone.properties.text).toBe('Original');
    expect(service.getRootElement().children.indexOf(clone)).toBe(1);
    expect(service.getSelectedElement()).toBe(clone);
  });

  it('deep clones children when duplicating a layout', () => {
    const stack = service.createElement(ElementType.VerticalStackLayout);
    service.addElement(stack);
    service.addElement(service.createElement(ElementType.Label), stack);

    const clone = service.duplicateElement(stack)!;

    expect(clone.children.length).toBe(1);
    expect(clone.children[0]).not.toBe(stack.children[0]);
    expect(clone.children[0].parent).toBe(clone);
  });

  it('round trips the design through localStorage', () => {
    const label = service.createElement(ElementType.Label);
    service.addElement(label);
    service.updateElementProperties(label, { text: 'Saved' });

    expect(service.saveToStorage()).toBeTrue();
    service.clearDesign();
    expect(service.getRootElement().children.length).toBe(0);

    expect(service.loadFromStorage()).toBeTrue();
    expect(service.getRootElement().children[0].properties.text).toBe('Saved');
    expect(service.getRootElement().children[0].parent).toBe(service.getRootElement());
  });

  it('reports when nothing is stored', () => {
    expect(service.hasStoredDesign()).toBeFalse();
    expect(service.loadFromStorage()).toBeFalse();
  });

  it('serializes without circular references', () => {
    const stack = service.createElement(ElementType.VerticalStackLayout);
    service.addElement(stack);
    service.addElement(service.createElement(ElementType.Label), stack);

    expect(() => service.serialize()).not.toThrow();
    expect(service.serialize()).toContain('"Label"');
  });

  it('adds and removes grid rows and columns', () => {
    const grid = service.createElement(ElementType.Grid);
    service.addElement(grid);

    service.addGridRow(grid);
    service.addGridColumn(grid);
    expect(service.getGridDefinition(grid).rows.length).toBe(3);
    expect(service.getGridDefinition(grid).columns.length).toBe(3);

    service.removeGridRow(grid, 2);
    service.removeGridColumn(grid, 2);
    expect(service.getGridDefinition(grid).rows.length).toBe(2);
    expect(service.getGridDefinition(grid).columns.length).toBe(2);
  });

  it('keeps at least one row and column', () => {
    const grid = service.createElement(ElementType.Grid);
    service.addElement(grid);

    service.removeGridRow(grid, 1);
    service.removeGridRow(grid, 0);

    expect(service.getGridDefinition(grid).rows.length).toBe(1);
  });

  it('clamps children when a grid shrinks', () => {
    const grid = service.createElement(ElementType.Grid);
    service.addElement(grid);
    const label = service.createElement(ElementType.Label);
    service.addElement(label, grid);
    service.updateElementProperties(label, { row: 1, column: 1 });

    service.removeGridRow(grid, 1);

    expect(label.properties.row).toBe(0);
  });

  it('renames elements but ignores blank names', () => {
    const label = service.createElement(ElementType.Label);
    service.addElement(label);

    service.renameElement(label, 'Headline');
    expect(label.name).toBe('Headline');

    service.renameElement(label, '   ');
    expect(label.name).toBe('Headline');
  });
});

describe('XAML round trip', () => {
  let elements: ElementService;
  let generator: XamlGeneratorService;
  let parser: XamlParserService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    elements = TestBed.inject(ElementService);
    generator = TestBed.inject(XamlGeneratorService);
    parser = TestBed.inject(XamlParserService);
  });

  it('emits the actual grid definition', () => {
    const grid = elements.createElement(ElementType.Grid);
    elements.addElement(grid);
    elements.addGridRow(grid);
    elements.updateGridColumn(grid, 0, { value: 120, type: GridLengthType.Absolute });
    elements.updateGridRow(grid, 0, { value: 1, type: GridLengthType.Auto });

    const xaml = generator.generateXaml(elements.getRootElement());

    expect(xaml).toContain('<RowDefinition Height="Auto" />');
    expect(xaml).toContain('<ColumnDefinition Width="120" />');
    expect(xaml.match(/<RowDefinition /g)!.length).toBe(3);
  });

  it('parses grid definitions back', () => {
    const grid = elements.createElement(ElementType.Grid);
    elements.addElement(grid);
    elements.updateGridRow(grid, 0, { value: 2, type: GridLengthType.Star });

    const parsed = parser.parseXaml(generator.generateXaml(elements.getRootElement()));
    const parsedGrid = parsed.children[0];

    expect(parsedGrid.type).toBe(ElementType.Grid);
    expect(parsedGrid.properties.gridDefinition!.rows[0].height).toEqual({ value: 2, type: GridLengthType.Star });
  });

  it('preserves the orientation of a stack layout', () => {
    const stack = elements.createElement(ElementType.StackLayout);
    elements.addElement(stack);
    elements.updateElementProperties(stack, { orientation: Orientation.Horizontal });

    const xaml = generator.generateXaml(elements.getRootElement());
    expect(xaml).toContain('<HorizontalStackLayout');

    const parsed = parser.parseXaml(xaml);
    expect(parsed.children[0].properties.orientation).toBe(Orientation.Horizontal);
  });

  it('keeps text and bounds stable across a round trip', () => {
    const label = elements.createElement(ElementType.Label);
    elements.addElement(label);
    elements.updateElementProperties(label, { text: 'Hi & bye', x: 12, y: 34, width: 111, height: 22 });

    const first = generator.generateXaml(elements.getRootElement());
    const parsed = parser.parseXaml(first);
    const second = generator.generateXaml(parsed);

    expect(second).toBe(first);
    expect(parsed.children[0].properties.text).toBe('Hi & bye');
    expect(parsed.children[0].properties.x).toBe(12);
  });
});
