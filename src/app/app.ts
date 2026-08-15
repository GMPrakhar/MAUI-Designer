import { Component, signal, HostListener, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { Subscription } from 'rxjs';

import { ElementService } from './services/element';
import { ToolboxComponent } from './components/toolbox/toolbox';
import { DesignerCanvasComponent } from './components/designer-canvas/designer-canvas';
import { PropertiesPanelComponent } from './components/properties-panel/properties-panel';
import { HierarchyPanelComponent } from './components/hierarchy-panel/hierarchy-panel';
import { XamlEditorComponent } from './components/xaml-editor/xaml-editor';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    DragDropModule,
    ToolboxComponent,
    DesignerCanvasComponent,
    PropertiesPanelComponent,
    HierarchyPanelComponent,
    XamlEditorComponent
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit, OnDestroy {
  protected readonly title = signal('MAUI Designer - Angular');

  leftPanelWidth = 250;
  rightPanelWidth = 350;
  bottomPanelHeight = 200;

  selectedTab = 'hierarchy';
  selectedBottomTab = 'xaml';

  canUndo = false;
  canRedo = false;
  toastMessage = '';
  private toastTimeout: any;
  private subscription = new Subscription();

  // Resize state
  private isResizing = false;
  private resizeType: 'left' | 'right' | 'bottom' | null = null;
  private startX = 0;
  private startY = 0;
  private startWidth = 0;
  private startHeight = 0;

  // Constants for panel constraints
  private readonly MIN_PANEL_SIZE = 50;
  private readonly MAX_PANEL_RATIO = 0.8;

  constructor(private elementService: ElementService) {}

  ngOnInit() {
    this.subscription.add(
      this.elementService.history$.subscribe(state => {
        this.canUndo = state.canUndo;
        this.canRedo = state.canRedo;
      })
    );
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
    if (this.toastTimeout) {
      clearTimeout(this.toastTimeout);
    }
  }

  undo() {
    this.elementService.undo();
  }

  redo() {
    this.elementService.redo();
  }

  saveDesign() {
    this.showToast(
      this.elementService.saveToStorage()
        ? 'Design saved to this browser'
        : 'Unable to save the design (storage unavailable)'
    );
  }

  loadDesign() {
    this.showToast(
      this.elementService.loadFromStorage()
        ? 'Design loaded'
        : 'No saved design found'
    );
  }

  clearDesign() {
    this.elementService.clearDesign();
    this.showToast('Canvas cleared');
  }

  private showToast(message: string) {
    this.toastMessage = message;
    if (this.toastTimeout) {
      clearTimeout(this.toastTimeout);
    }
    this.toastTimeout = setTimeout(() => (this.toastMessage = ''), 3000);
  }

  selectTab(tab: string) {
    this.selectedTab = tab;
  }

  selectBottomTab(tab: string) {
    this.selectedBottomTab = tab;
  }

  // Resize handle interactions
  onResizeStart(event: MouseEvent, type: 'left' | 'right' | 'bottom') {
    event.preventDefault();
    this.isResizing = true;
    this.resizeType = type;
    this.startX = event.clientX;
    this.startY = event.clientY;
    
    switch (type) {
      case 'left':
        this.startWidth = this.leftPanelWidth;
        break;
      case 'right':
        this.startWidth = this.rightPanelWidth;
        break;
      case 'bottom':
        this.startHeight = this.bottomPanelHeight;
        break;
    }

    // Set resize cursor
    document.body.style.cursor = type === 'bottom' ? 'ns-resize' : 'ew-resize';
    document.body.style.userSelect = 'none';
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent) {
    if (!this.isResizing || !this.resizeType) return;

    const windowWidth = window.innerWidth;
    const windowHeight = window.innerHeight - 60; // Subtract header height

    switch (this.resizeType) {
      case 'left':
        const leftDelta = event.clientX - this.startX;
        const newLeftWidth = Math.max(
          this.MIN_PANEL_SIZE,
          Math.min(windowWidth * this.MAX_PANEL_RATIO, this.startWidth + leftDelta)
        );
        this.leftPanelWidth = newLeftWidth;
        break;

      case 'right':
        const rightDelta = this.startX - event.clientX;
        const newRightWidth = Math.max(
          this.MIN_PANEL_SIZE,
          Math.min(windowWidth * this.MAX_PANEL_RATIO, this.startWidth + rightDelta)
        );
        this.rightPanelWidth = newRightWidth;
        break;

      case 'bottom':
        const bottomDelta = this.startY - event.clientY;
        const newBottomHeight = Math.max(
          this.MIN_PANEL_SIZE,
          Math.min(windowHeight * this.MAX_PANEL_RATIO, this.startHeight + bottomDelta)
        );
        this.bottomPanelHeight = newBottomHeight;
        break;
    }
  }

  @HostListener('document:mouseup', ['$event'])
  onMouseUp(event: MouseEvent) {
    if (this.isResizing) {
      this.isResizing = false;
      this.resizeType = null;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    }
  }
}
