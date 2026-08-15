import { TestBed } from '@angular/core/testing';
import { AlignmentService } from './alignment';
import { ElementService } from './element';
import { ElementType, MauiElement } from '../models/maui-element';

describe('AlignmentService', () => {
  let service: AlignmentService;
  let elements: ElementService;

  const add = (type: ElementType, x: number, y: number, width: number, height: number): MauiElement => {
    const element = elements.createElement(type);
    elements.addElement(element, elements.getRootElement());
    elements.updateElementProperties(element, { x, y, width, height });
    return element;
  };

  beforeEach(() => {
    TestBed.configureTestingModule({});
    elements = TestBed.inject(ElementService);
    service = TestBed.inject(AlignmentService);
  });

  it('aligns elements to the leftmost edge', () => {
    const a = add(ElementType.Label, 50, 10, 100, 30);
    const b = add(ElementType.Button, 120, 80, 80, 40);

    service.align([a, b], 'left');

    expect(a.properties.x).toBe(50);
    expect(b.properties.x).toBe(50);
  });

  it('aligns elements to the rightmost edge using their widths', () => {
    const a = add(ElementType.Label, 50, 10, 100, 30);
    const b = add(ElementType.Button, 120, 80, 80, 40);

    service.align([a, b], 'right');

    expect(a.properties.x! + 100).toBe(200);
    expect(b.properties.x! + 80).toBe(200);
  });

  it('centres elements on the shared horizontal centre', () => {
    const a = add(ElementType.Label, 0, 10, 100, 30);
    const b = add(ElementType.Button, 100, 80, 50, 40);

    service.align([a, b], 'center');

    expect(a.properties.x! + 50).toBe(75);
    expect(b.properties.x! + 25).toBe(75);
  });

  it('aligns to the top and bottom edges', () => {
    const a = add(ElementType.Label, 0, 10, 100, 30);
    const b = add(ElementType.Button, 0, 90, 100, 50);

    service.align([a, b], 'top');
    expect(b.properties.y).toBe(10);

    elements.updateElementProperties(b, { y: 90 });
    service.align([a, b], 'bottom');
    expect(a.properties.y! + 30).toBe(140);
  });

  it('ignores single elements', () => {
    const a = add(ElementType.Label, 50, 10, 100, 30);

    service.align([a], 'left');

    expect(a.properties.x).toBe(50);
    expect(service.canAlign([a])).toBeFalse();
  });

  it('only aligns children of an absolute layout', () => {
    const stack = elements.createElement(ElementType.VerticalStackLayout);
    elements.addElement(stack, elements.getRootElement());
    const child = elements.createElement(ElementType.Label);
    elements.addElement(child, stack);
    elements.updateElementProperties(child, { x: 40 });

    expect(service.canAlign([child, stack])).toBeFalse();
    service.align([child, stack], 'left');
    expect(child.properties.x).toBe(40);
  });

  it('distributes three elements with equal gaps', () => {
    const a = add(ElementType.Label, 0, 0, 100, 30);
    const b = add(ElementType.Label, 150, 0, 100, 30);
    const c = add(ElementType.Label, 400, 0, 100, 30);

    service.distribute([a, b, c], 'horizontal');

    const gapOne = b.properties.x! - (a.properties.x! + 100);
    const gapTwo = c.properties.x! - (b.properties.x! + 100);
    expect(Math.abs(gapOne - gapTwo)).toBeLessThanOrEqual(1);
  });

  it('needs at least three elements to distribute', () => {
    const a = add(ElementType.Label, 0, 0, 100, 30);
    const b = add(ElementType.Label, 150, 0, 100, 30);

    service.distribute([a, b], 'horizontal');

    expect(b.properties.x).toBe(150);
  });

  it('distributes vertically', () => {
    const a = add(ElementType.Label, 0, 0, 100, 30);
    const b = add(ElementType.Label, 0, 50, 100, 30);
    const c = add(ElementType.Label, 0, 300, 100, 30);

    service.distribute([a, b, c], 'vertical');

    const gapOne = b.properties.y! - (a.properties.y! + 30);
    const gapTwo = c.properties.y! - (b.properties.y! + 30);
    expect(Math.abs(gapOne - gapTwo)).toBeLessThanOrEqual(1);
  });

  it('snaps coordinates to the nearest grid intersection', () => {
    expect(service.snapToGrid(33, 20)).toBe(40);
    expect(service.snapToGrid(29, 20)).toBe(20);
    expect(service.snapToGrid(12.4, 1)).toBe(12);
    expect(service.snapToGrid(7, 0)).toBe(7);
  });

  it('produces a guide when a dragged edge is near a sibling edge', () => {
    const anchor = add(ElementType.Label, 100, 40, 100, 30);
    const dragged = add(ElementType.Button, 300, 300, 100, 30);

    const result = service.computeGuides(dragged, 103, 300);

    expect(result.x).toBe(100);
    expect(result.guides.some(guide => guide.orientation === 'vertical')).toBeTrue();
    expect(anchor.properties.x).toBe(100);
  });

  it('does not snap beyond the threshold', () => {
    add(ElementType.Label, 100, 40, 100, 30);
    const dragged = add(ElementType.Button, 300, 300, 100, 30);

    // 250 avoids the parent's centre line, which is a guide of its own
    const result = service.computeGuides(dragged, 140, 250);

    expect(result.x).toBe(140);
    expect(result.guides.length).toBe(0);
  });

  it('snaps to the parent centre line', () => {
    const root = elements.getRootElement();
    elements.updateElementProperties(root, { width: 800, height: 600 });
    const dragged = add(ElementType.Button, 0, 0, 100, 30);

    const result = service.computeGuides(dragged, 398, 0);

    expect(result.x).toBe(400);
  });

  it('aligning a group records a single undo step', () => {
    const a = add(ElementType.Label, 50, 10, 100, 30);
    const b = add(ElementType.Button, 120, 80, 80, 40);

    service.align([a, b], 'left');
    elements.undo();

    expect(elements.findElementById(b.id)!.properties.x).toBe(120);
    expect(elements.findElementById(a.id)!.properties.x).toBe(50);
  });
});
