import { Component, signal, HostListener, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { Subscription } from 'rxjs';

import { ElementService } from './services/element';
import { HostBridgeService } from './services/host-bridge';
import { XamlParserService } from './services/xaml-parser';
import { XamlGeneratorService } from './services/xaml-generator';
import { CustomControlRegistryService } from './services/custom-control-registry';
import { ToolboxComponent } from './components/toolbox/toolbox';
import { DesignerCanvasComponent } from './components/designer-canvas/designer-canvas';
import { PropertiesPanelComponent } from './components/properties-panel/properties-panel';
import { HierarchyPanelComponent } from './components/hierarchy-panel/hierarchy-panel';
import { XamlEditorComponent } from './components/xaml-editor/xaml-editor';
import { CanvasToolbarComponent } from './components/canvas-toolbar/canvas-toolbar';

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
    XamlEditorComponent,
    CanvasToolbarComponent
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
  hostFileName: string | null = null;

  /**
   * Always resolves to the newest published asset, so the site never has to be
   * redeployed to point at a new build of the extension.
   */
  readonly vsixDownloadUrl =
    'https://github.com/GMPrakhar/MAUI-Designer/releases/latest/download/MauiDesigner.vsix';

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

  constructor(
    private elementService: ElementService,
    private hostBridge: HostBridgeService,
    private xamlParser: XamlParserService,
    private xamlGenerator: XamlGeneratorService,
    private registry: CustomControlRegistryService
  ) {}

  ngOnInit() {
    this.subscription.add(
      this.elementService.history$.subscribe(state => {
        this.canUndo = state.canUndo;
        this.canRedo = state.canRedo;
      })
    );

    if (this.hostBridge.isHosted) {
      this.connectToHost();
    }
  }

  /** In Visual Studio or VS Code the open .xaml document is the source of truth. */
  private connectToHost() {
    this.subscription.add(
      this.hostBridge.messages$.subscribe(message => {
        switch (message.type) {
          case 'host.ready':
            this.hostBridge.requestManifests();
            break;
          case 'document.load':
            this.loadFromHost(message.xaml);
            break;
          case 'manifests.push':
            for (const manifest of message.manifests) {
              this.registry.register(manifest);
            }
            break;
          case 'document.saved':
            this.showToast('Saved');
            break;
        }
      })
    );

    // Every change flows back so the host can keep the document in sync
    this.subscription.add(
      this.elementService.elements$.subscribe(root => {
        this.hostBridge.notifyChanged(this.xamlGenerator.generateXaml(root));
      })
    );

    this.subscription.add(this.hostBridge.fileName$.subscribe(name => (this.hostFileName = name)));
  }

  private loadFromHost(xaml: string) {
    try {
      this.elementService.setRootElement(this.xamlParser.parseXaml(xaml));
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.hostBridge.reportError(message);
      this.showToast(`Could not open the document: ${message}`);
    }
  }

  get isHosted(): boolean {
    return this.hostBridge.isHosted;
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
    if (this.hostBridge.isHosted) {
      // Hosted in an IDE the design belongs to the open file, not to browser storage
      this.hostBridge.save(this.xamlGenerator.generateXaml(this.elementService.getRootElement()));
      return;
    }

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

  onVsixDownload() {
    // The tooltip is easy to miss, so restate the warning where it will be seen.
    this.showToast('Downloading the beta extension - it may be unstable or broken.');
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
