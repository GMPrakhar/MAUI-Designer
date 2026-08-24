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

  describe('z-order', () => {
    /** Adds `count` labels to the root and returns them in document order. */
    function addLabels(count: number) {
      const root = service.getRootElement();
      return Array.from({ length: count }, () => {
        const label = service.createElement(ElementType.Label);
        service.addElement(label, root);
        return label;
      });
    }

    /** The current stacking order of the root's children, back to front. */
    function order(labels: ReturnType<typeof addLabels>) {
      return service.getRootElement().children.map(child => labels.indexOf(child));
    }

    it('brings the selection to the front', () => {
      const labels = addLabels(3);
      service.selectElement(labels[0]);

      expect(service.reorderSelection('front')).toBe(true);
      expect(order(labels)).toEqual([1, 2, 0]);
    });

    it('sends the selection to the back', () => {
      const labels = addLabels(3);
      service.selectElement(labels[2]);

      expect(service.reorderSelection('back')).toBe(true);
      expect(order(labels)).toEqual([2, 0, 1]);
    });

    it('moves one step at a time', () => {
      const labels = addLabels(3);
      service.selectElement(labels[0]);

      service.reorderSelection('forward');
      expect(order(labels)).toEqual([1, 0, 2]);

      service.reorderSelection('forward');
      expect(order(labels)).toEqual([1, 2, 0]);
    });

    it('keeps a multi-selection together instead of collapsing it', () => {
      const labels = addLabels(4);
      service.setSelection([labels[0], labels[1]]);

      service.reorderSelection('forward');

      // Both move up one slot and stay in their original relative order. Moving
      // them independently would let the lower one hop over the upper one.
      expect(order(labels)).toEqual([2, 0, 1, 3]);
    });

    it('reports no change when the selection is already at the edge', () => {
      const labels = addLabels(3);
      service.selectElement(labels[2]);

      expect(service.reorderSelection('front')).toBe(false);
      expect(service.reorderSelection('forward')).toBe(false);
      expect(order(labels)).toEqual([0, 1, 2]);
    });

    it('does not spend an undo step on a move that changes nothing', () => {
      const labels = addLabels(2);
      service.selectElement(labels[1]);

      // A fresh service has nothing to undo; a no-op must not create one.
      const undoBefore = service.canUndo();
      service.reorderSelection('front');

      expect(service.canUndo()).toBe(undoBefore);
    });

    it('undoes a restack as a single step', () => {
      const labels = addLabels(3);
      service.selectElement(labels[0]);
      service.reorderSelection('front');

      service.undo();

      const ids = service.getRootElement().children.map(child => child.id);
      expect(ids).toEqual([labels[0].id, labels[1].id, labels[2].id]);
    });

    it('restacks each parent independently when the selection spans containers', () => {
      const root = service.getRootElement();
      const stack = service.createElement(ElementType.VerticalStackLayout);
      service.addElement(stack, root);

      const rootLabel = service.createElement(ElementType.Label);
      service.addElement(rootLabel, root);
      const nested = [0, 1].map(() => {
        const label = service.createElement(ElementType.Label);
        service.addElement(label, stack);
        return label;
      });

      // `stack` is first in root, `nested[0]` is first in stack. Selecting one
      // from each container and moving forward should advance both.
      service.setSelection([stack, nested[0]]);
      service.reorderSelection('forward');

      expect(root.children).toEqual([rootLabel, stack]);
      expect(stack.children).toEqual([nested[1], nested[0]]);
    });

    it('refuses to restack the root, which has no siblings', () => {
      service.selectElement(service.getRootElement());

      expect(service.reorderSelection('front')).toBe(false);
      expect(service.canReorderSelection()).toBe(false);
    });

    it('only offers to restack when there is something to restack against', () => {
      const labels = addLabels(1);
      service.selectElement(labels[0]);
      expect(service.canReorderSelection()).toBe(false);

      const second = service.createElement(ElementType.Label);
      service.addElement(second, service.getRootElement());
      expect(service.canReorderSelection()).toBe(true);
    });

    it('reorders siblings with the CDK drop-list index convention', () => {
      const labels = addLabels(3);
      const root = service.getRootElement();

      expect(service.reorderChild(root, 0, 2)).toBe(true);
      expect(root.children.map(child => child.id)).toEqual([labels[1].id, labels[2].id, labels[0].id]);
    });

    it('moves a sibling one slot at a time', () => {
      const labels = addLabels(2);
      const root = service.getRootElement();

      expect(service.moveSibling(labels[0], 1)).toBe(true);
      expect(root.children.map(child => child.id)).toEqual([labels[1].id, labels[0].id]);
      expect(service.moveSibling(labels[0], 1)).toBe(false);
    });
  });
});