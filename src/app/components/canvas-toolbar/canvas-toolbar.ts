import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { ElementService } from '../../services/element';
import { AlignmentService, AlignMode, DistributeMode } from '../../services/alignment';
import { ClipboardService } from '../../services/clipboard';
import { ViewportService, ViewportState, DEVICE_PRESETS } from '../../services/viewport';
import { MauiElement } from '../../models/maui-element';

@Component({
  selector: 'app-canvas-toolbar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './canvas-toolbar.html',
  styleUrl: './canvas-toolbar.scss'
})
export class CanvasToolbarComponent implements OnInit, OnDestroy {
  readonly devices = DEVICE_PRESETS;

  viewport!: ViewportState;
  selection: MauiElement[] = [];

  private subscription = new Subscription();

  constructor(
    private elementService: ElementService,
    private alignmentService: AlignmentService,
    private clipboardService: ClipboardService,
    private viewportService: ViewportService
  ) {
    this.viewport = this.viewportService.state;
  }

  ngOnInit() {
    this.subscription.add(this.viewportService.state$.subscribe(state => (this.viewport = state)));
    this.subscription.add(this.elementService.selectedElements$.subscribe(selection => (this.selection = selection)));
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  // --- Device & theme ---------------------------------------------------------

  get zoomPercent(): number {
    return Math.round(this.viewport.zoom * 100);
  }

  selectDevice(deviceId: string) {
    this.viewportService.patch({ deviceId });
    const device = this.viewportService.getDevice(deviceId);
    if (device && deviceId !== 'custom') {
      this.elementService.updateElementProperties(this.elementService.getRootElement(), {
        width: device.width,
        height: device.height
      });
    }
  }

  toggleTheme() {
    this.viewportService.toggleTheme();
  }

  toggleSnap() {
    this.viewportService.toggleSnap();
  }

  toggleGrid() {
    this.viewportService.toggleGrid();
  }

  toggleRulers() {
    this.viewportService.toggleRulers();
  }

  setGridSize(value: number) {
    this.viewportService.patch({ gridSize: value });
  }

  // --- Zoom -------------------------------------------------------------------

  zoomIn() {
    this.viewportService.zoomIn();
  }

  zoomOut() {
    this.viewportService.zoomOut();
  }

  resetView() {
    this.viewportService.resetView();
  }

  zoomToFit() {
    const surface = document.querySelector('.canvas-viewport') as HTMLElement | null;
    const root = this.elementService.getRootElement().properties;
    if (!surface) {
      return;
    }
    this.viewportService.zoomToFit(
      surface.clientWidth - 32,
      surface.clientHeight - 32,
      root.width || 800,
      root.height || 600
    );
  }

  // --- Alignment --------------------------------------------------------------

  get canAlign(): boolean {
    return this.alignmentService.canAlign(this.selection);
  }

  get canDistribute(): boolean {
    return this.selection.length > 2;
  }

  align(mode: AlignMode) {
    this.alignmentService.align(this.selection, mode);
  }

  distribute(mode: DistributeMode) {
    this.alignmentService.distribute(this.selection, mode);
  }

  // --- Clipboard --------------------------------------------------------------

  get hasSelection(): boolean {
    return this.selection.some(element => !!element.parent);
  }

  copy() {
    this.clipboardService.copy(this.selection);
  }

  paste() {
    this.clipboardService.paste();
  }

  saveAsTemplate() {
    const name = window.prompt('Template name', this.selection[0]?.name || 'Component');
    if (name) {
      this.clipboardService.saveTemplate(name, this.selection);
    }
  }
}
