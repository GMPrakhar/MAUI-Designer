import { Component, signal } from '@angular/core';
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

  selectTab(tab: string) {
    this.selectedTab = tab;
  }

  selectBottomTab(tab: string) {
    this.selectedBottomTab = tab;
  }
}
