import { TestBed } from '@angular/core/testing';
import { ElementService } from '../services/element';
import { ElementType } from '../models/maui-element';

describe('ElementService', () => {
  let service: ElementService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ElementService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should create root element', () => {
    const root = service.getRootElement();
    expect(root).toBeTruthy();
    expect(root.type).toBe(ElementType.AbsoluteLayout);
    expect(root.children.length).toBe(0);
  });

  it('should create new elements', () => {
    const label = service.createElement(ElementType.Label);
    expect(label).toBeTruthy();
    expect(label.type).toBe(ElementType.Label);
    expect(label.properties.text).toBe('Label');
  });

  it('should add elements to root', () => {
    const root = service.getRootElement();
    const label = service.createElement(ElementType.Label);
    
    service.addElement(label, root);
    
    expect(root.children.length).toBe(1);
    expect(root.children[0]).toBe(label);
    expect(label.parent).toBe(root);
  });

  it('should select elements', () => {
    const label = service.createElement(ElementType.Label);
    
    service.selectElement(label);
    
    expect(service.getSelectedElement()).toBe(label);
  });

  it('should update element properties', () => {
    const button = service.createElement(ElementType.Button);
    
    service.updateElementProperties(button, {
      text: 'Updated Button',
      backgroundColor: '#ff0000',
      width: 200
    });
    
    expect(button.properties.text).toBe('Updated Button');
    expect(button.properties.backgroundColor).toBe('#ff0000');
    expect(button.properties.width).toBe(200);
  });
});