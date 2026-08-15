import { TestBed } from '@angular/core/testing';
import { DEVICE_PRESETS, ViewportService } from './viewport';

describe('ViewportService', () => {
  let service: ViewportService;

  beforeEach(() => {
    localStorage.removeItem('maui-designer.viewport');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ViewportService);
  });

  afterEach(() => localStorage.removeItem('maui-designer.viewport'));

  it('starts at 100% zoom with no pan', () => {
    expect(service.state.zoom).toBe(1);
    expect(service.state.panX).toBe(0);
    expect(service.state.panY).toBe(0);
  });

  it('zooms in and out in 10% steps', () => {
    service.zoomIn();
    expect(service.state.zoom).toBeCloseTo(1.1, 5);

    service.zoomOut();
    expect(service.state.zoom).toBeCloseTo(1, 5);
  });

  it('clamps the zoom to the supported range', () => {
    service.setZoom(99);
    expect(service.state.zoom).toBe(3);

    service.setZoom(0.01);
    expect(service.state.zoom).toBe(0.25);
  });

  it('clamps the grid size', () => {
    service.patch({ gridSize: 1 });
    expect(service.state.gridSize).toBe(2);

    service.patch({ gridSize: 5000 });
    expect(service.state.gridSize).toBe(200);
  });

  it('fits the design into the viewport', () => {
    service.zoomToFit(400, 600, 800, 600);
    expect(service.state.zoom).toBe(0.5);
    expect(service.state.panX).toBe(0);
  });

  it('ignores a fit request for an empty design', () => {
    service.setZoom(2);
    service.zoomToFit(400, 600, 0, 0);
    expect(service.state.zoom).toBe(2);
  });

  it('accumulates pan offsets and resets them', () => {
    service.panBy(10, -5);
    service.panBy(4, 5);
    expect(service.state.panX).toBe(14);
    expect(service.state.panY).toBe(0);

    service.resetView();
    expect(service.state.panX).toBe(0);
    expect(service.state.zoom).toBe(1);
  });

  it('toggles the preview flags', () => {
    const { theme, snapToGrid, showGrid, showRulers } = service.state;

    service.toggleTheme();
    service.toggleSnap();
    service.toggleGrid();
    service.toggleRulers();

    expect(service.state.theme).not.toBe(theme);
    expect(service.state.snapToGrid).toBe(!snapToGrid);
    expect(service.state.showGrid).toBe(!showGrid);
    expect(service.state.showRulers).toBe(!showRulers);
  });

  it('exposes the device presets by id', () => {
    expect(service.getDevice('tablet')!.width).toBe(768);
    expect(service.getDevice('nope')).toBeUndefined();
    expect(service.devices.length).toBe(DEVICE_PRESETS.length);
  });

  it('persists the state so a new instance restores it', () => {
    service.setZoom(2);
    service.toggleTheme();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const restored = TestBed.inject(ViewportService);

    expect(restored.state.zoom).toBe(2);
    expect(restored.state.theme).toBe('dark');
  });

  it('publishes every change to subscribers', () => {
    const seen: number[] = [];
    const subscription = service.state$.subscribe(state => seen.push(state.zoom));

    service.zoomIn();
    service.zoomIn();
    subscription.unsubscribe();

    expect(seen.length).toBe(3);
  });
});
