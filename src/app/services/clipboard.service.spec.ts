import { TestBed } from '@angular/core/testing';
import { ClipboardService } from './clipboard';
import { ElementService } from './element';
import { ElementType, MauiElement } from '../models/maui-element';

describe('ClipboardService', () => {
  let service: ClipboardService;
  let elements: ElementService;

  const add = (type: ElementType, parent?: MauiElement): MauiElement => {
    const element = elements.createElement(type);
    elements.addElement(element, parent || elements.getRootElement());
    return element;
  };

  beforeEach(() => {
    localStorage.removeItem('maui-designer.templates');
    localStorage.removeItem('maui-designer.clipboard');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    elements = TestBed.inject(ElementService);
    service = TestBed.inject(ClipboardService);
  });

  afterEach(() => {
    localStorage.removeItem('maui-designer.templates');
    localStorage.removeItem('maui-designer.clipboard');
  });

  it('copies and pastes an element with a fresh id', () => {
    const label = add(ElementType.Label);
    elements.updateElementProperties(label, { text: 'Hello' });

    expect(service.copy([label])).toBe(1);
    const pasted = service.paste(elements.getRootElement());

    expect(pasted.length).toBe(1);
    expect(pasted[0].id).not.toBe(label.id);
    expect(pasted[0].properties.text).toBe('Hello');
    expect(elements.getRootElement().children.length).toBe(2);
  });

  it('offsets the pasted copy so it is visible', () => {
    const label = add(ElementType.Label);
    elements.updateElementProperties(label, { x: 10, y: 20 });

    service.copy([label]);
    const [pasted] = service.paste(elements.getRootElement());

    expect(pasted.properties.x).toBe(26);
    expect(pasted.properties.y).toBe(36);
  });

  it('refuses to copy the root element', () => {
    expect(service.copy([elements.getRootElement()])).toBe(0);
    expect(service.hasContent()).toBeFalse();
    expect(service.paste()).toEqual([]);
  });

  it('copies a container together with its children', () => {
    const stack = add(ElementType.VerticalStackLayout);
    add(ElementType.Label, stack);
    add(ElementType.Button, stack);

    service.copy([stack]);
    const [pasted] = service.paste(elements.getRootElement());

    expect(pasted.children.length).toBe(2);
    expect(pasted.children[0].parent).toBe(pasted);
  });

  it('cuts by copying and then removing the selection', () => {
    const label = add(ElementType.Label);
    elements.setSelection([label]);

    expect(service.cut([label])).toBe(1);
    expect(elements.getRootElement().children.length).toBe(0);

    service.paste(elements.getRootElement());
    expect(elements.getRootElement().children.length).toBe(1);
  });

  it('saves, lists and inserts a component template', () => {
    const button = add(ElementType.Button);
    elements.updateElementProperties(button, { text: 'Primary' });

    const template = service.saveTemplate('Primary button', [button]);
    expect(template).toBeTruthy();
    expect(service.getTemplates().length).toBe(1);

    const inserted = service.insertTemplate(template!.id, elements.getRootElement());
    expect(inserted.length).toBe(1);
    expect(inserted[0].properties.text).toBe('Primary');
  });

  it('rejects templates without a name or content', () => {
    const button = add(ElementType.Button);

    expect(service.saveTemplate('   ', [button])).toBeNull();
    expect(service.saveTemplate('Empty', [])).toBeNull();
    expect(service.getTemplates().length).toBe(0);
  });

  it('deletes templates and ignores unknown ids', () => {
    const button = add(ElementType.Button);
    const template = service.saveTemplate('Temp', [button])!;

    expect(service.insertTemplate('missing')).toEqual([]);
    service.deleteTemplate(template.id);
    expect(service.getTemplates().length).toBe(0);
  });

  it('persists templates in localStorage', () => {
    const button = add(ElementType.Button);
    service.saveTemplate('Persisted', [button]);

    expect(localStorage.getItem('maui-designer.templates')).toContain('Persisted');
  });

  it('applies a starter page and replaces the design', () => {
    add(ElementType.Label);

    expect(service.applyStarterPage('login')).toBeTrue();
    expect(elements.getRootElement().children.length).toBeGreaterThan(1);
  });

  it('reports an unknown starter page', () => {
    expect(service.applyStarterPage('nope')).toBeFalse();
  });

  it('exposes every starter page with parsable XAML', () => {
    expect(service.starterPages.length).toBeGreaterThan(0);
    for (const page of service.starterPages) {
      expect(service.applyStarterPage(page.id)).toBeTrue();
    }
  });
});
