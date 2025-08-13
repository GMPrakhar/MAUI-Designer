import { Component, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule } from '@angular/cdk/drag-drop';

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
export class App {
  protected readonly title = signal('MAUI Designer - Angular');
  
  leftPanelWidth = 250;
  rightPanelWidth = 350;
  bottomPanelHeight = 200;
  
  selectedTab = 'hierarchy';
  selectedBottomTab = 'xaml';
  
  // Resize state
  private isResizing = false;
  private resizeType: 'left' | 'right' | 'bottom' | null = null;
  private startX = 0;
  private startY = 0;
  private startWidth = 0;
  private startHeight = 0;
  
  // Panel constraints
  private readonly minPanelWidth = 200;
  private readonly maxPanelWidth = 600;
  private readonly minPanelHeight = 150;
  private readonly maxPanelHeight = 400;

  selectTab(tab: string) {
    this.selectedTab = tab;
  }

  selectBottomTab(tab: string) {
    this.selectedBottomTab = tab;
  }
  
  startResize(event: MouseEvent, type: 'left' | 'right' | 'bottom') {
    event.preventDefault();
    this.isResizing = true;
    this.resizeType = type;
    this.startX = event.clientX;
    this.startY = event.clientY;
    
    // Store initial dimensions
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
    
    // Add cursor style to body
    document.body.style.cursor = type === 'bottom' ? 'ns-resize' : 'ew-resize';
    document.body.style.userSelect = 'none';
  }
  
  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent) {
    if (!this.isResizing || !this.resizeType) return;
    
    event.preventDefault();
    
    switch (this.resizeType) {
      case 'left':
        const leftDelta = event.clientX - this.startX;
        this.leftPanelWidth = Math.max(
          this.minPanelWidth,
          Math.min(this.maxPanelWidth, this.startWidth + leftDelta)
        );
        break;
        
      case 'right':
        const rightDelta = this.startX - event.clientX;
        this.rightPanelWidth = Math.max(
          this.minPanelWidth,
          Math.min(this.maxPanelWidth, this.startWidth + rightDelta)
        );
        break;
        
      case 'bottom':
        const bottomDelta = this.startY - event.clientY;
        this.bottomPanelHeight = Math.max(
          this.minPanelHeight,
          Math.min(this.maxPanelHeight, this.startHeight + bottomDelta)
        );
        break;
    }
  }
  
  @HostListener('document:mouseup', ['$event'])
  onMouseUp(event: MouseEvent) {
    if (!this.isResizing) return;
    
    this.isResizing = false;
    this.resizeType = null;
    
    // Reset cursor styles
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  }
}
