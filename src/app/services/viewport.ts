import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface DevicePreset {
  id: string;
  name: string;
  width: number;
  height: number;
}

export type PreviewTheme = 'light' | 'dark';

export interface ViewportState {
  zoom: number;
  panX: number;
  panY: number;
  snapToGrid: boolean;
  gridSize: number;
  showGrid: boolean;
  showRulers: boolean;
  showGuides: boolean;
  theme: PreviewTheme;
  deviceId: string;
}

export const DEVICE_PRESETS: DevicePreset[] = [
  { id: 'phone', name: 'Phone (390 × 844)', width: 390, height: 844 },
  { id: 'phone-small', name: 'Small phone (360 × 640)', width: 360, height: 640 },
  { id: 'tablet', name: 'Tablet (768 × 1024)', width: 768, height: 1024 },
  { id: 'desktop', name: 'Desktop (1280 × 800)', width: 1280, height: 800 },
  { id: 'custom', name: 'Custom', width: 800, height: 600 }
];

const DEFAULT_STATE: ViewportState = {
  zoom: 1,
  panX: 0,
  panY: 0,
  snapToGrid: true,
  gridSize: 8,
  showGrid: false,
  showRulers: true,
  showGuides: true,
  theme: 'light',
  deviceId: 'custom'
};

/** Holds every "how the canvas is displayed" setting, persisted per browser. */
@Injectable({ providedIn: 'root' })
export class ViewportService {
  private static readonly STORAGE_KEY = 'maui-designer.viewport';
  private static readonly MIN_ZOOM = 0.25;
  private static readonly MAX_ZOOM = 3;

  readonly devices = DEVICE_PRESETS;

  private stateSubject = new BehaviorSubject<ViewportState>({ ...DEFAULT_STATE, ...this.restore() });
  state$ = this.stateSubject.asObservable();

  get state(): ViewportState {
    return this.stateSubject.value;
  }

  patch(patch: Partial<ViewportState>): void {
    const next = { ...this.state, ...patch };
    next.zoom = Math.min(ViewportService.MAX_ZOOM, Math.max(ViewportService.MIN_ZOOM, next.zoom));
    next.gridSize = Math.min(200, Math.max(2, Math.round(next.gridSize)));
    this.stateSubject.next(next);
    this.persist(next);
  }

  zoomIn(): void {
    this.patch({ zoom: this.roundZoom(this.state.zoom + 0.1) });
  }

  zoomOut(): void {
    this.patch({ zoom: this.roundZoom(this.state.zoom - 0.1) });
  }

  setZoom(zoom: number): void {
    this.patch({ zoom: this.roundZoom(zoom) });
  }

  /** Scales the design so it fits into the given viewport size. */
  zoomToFit(viewportWidth: number, viewportHeight: number, designWidth: number, designHeight: number): void {
    if (designWidth <= 0 || designHeight <= 0) {
      return;
    }
    const scale = Math.min(viewportWidth / designWidth, viewportHeight / designHeight);
    this.patch({ zoom: this.roundZoom(scale), panX: 0, panY: 0 });
  }

  resetView(): void {
    this.patch({ zoom: 1, panX: 0, panY: 0 });
  }

  panBy(deltaX: number, deltaY: number): void {
    this.patch({ panX: this.state.panX + deltaX, panY: this.state.panY + deltaY });
  }

  toggleTheme(): void {
    this.patch({ theme: this.state.theme === 'light' ? 'dark' : 'light' });
  }

  toggleSnap(): void {
    this.patch({ snapToGrid: !this.state.snapToGrid });
  }

  toggleGrid(): void {
    this.patch({ showGrid: !this.state.showGrid });
  }

  toggleRulers(): void {
    this.patch({ showRulers: !this.state.showRulers });
  }

  getDevice(id: string): DevicePreset | undefined {
    return DEVICE_PRESETS.find(device => device.id === id);
  }

  private roundZoom(zoom: number): number {
    return Math.round(zoom * 100) / 100;
  }

  private persist(state: ViewportState): void {
    try {
      localStorage.setItem(ViewportService.STORAGE_KEY, JSON.stringify(state));
    } catch {
      // storage is optional
    }
  }

  private restore(): Partial<ViewportState> {
    try {
      const stored = localStorage.getItem(ViewportService.STORAGE_KEY);
      return stored ? JSON.parse(stored) : {};
    } catch {
      return {};
    }
  }
}
