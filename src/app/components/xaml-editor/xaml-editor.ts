import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ElementService } from '../../services/element';
import { XamlGeneratorService } from '../../services/xaml-generator';
import { XamlParserService } from '../../services/xaml-parser';
import { Observable, Subscription, map } from 'rxjs';

@Component({
  selector: 'app-xaml-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './xaml-editor.html',
  styleUrl: './xaml-editor.scss'
})
export class XamlEditorComponent implements OnInit, OnDestroy {
  xamlContent$: Observable<string>;
  editableXamlContent: string = '';
  statusMessage: string = '';
  statusType: 'success' | 'error' | 'info' = 'info';
  private statusTimeout: any;
  private subscription = new Subscription();

  constructor(
    private elementService: ElementService,
    private xamlGenerator: XamlGeneratorService,
    private xamlParser: XamlParserService
  ) {
    this.xamlContent$ = this.elementService.elements$.pipe(
      map(rootElement => this.xamlGenerator.generateXaml(rootElement))
    );
  }

  ngOnInit() {
    // Keep the editor in sync with the current design
    this.subscription.add(
      this.xamlContent$.subscribe(xaml => {
        this.editableXamlContent = xaml;
      })
    );
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
    if (this.statusTimeout) {
      clearTimeout(this.statusTimeout);
    }
  }

  applyXaml() {
    if (!this.editableXamlContent.trim()) {
      this.showStatus('XAML content cannot be empty', 'error');
      return;
    }

    try {
      const rootElement = this.xamlParser.parseXaml(this.editableXamlContent);
      this.elementService.setRootElement(rootElement);
      this.showStatus('XAML applied successfully!', 'success');
    } catch (error: any) {
      const errorMessage = this.formatErrorMessage(error);
      this.showStatus(errorMessage, 'error');
    }
  }

  private formatErrorMessage(error: any): string {
    if (error.message) {
      // Try to extract line number if available
      const lineMatch = error.message.match(/line (\d+)/i);
      if (lineMatch) {
        return `Parse error at line ${lineMatch[1]}: ${error.message}`;
      }
      return `Parse error: ${error.message}`;
    }
    return 'Failed to parse XAML. Please check your syntax.';
  }

  private showStatus(message: string, type: 'success' | 'error' | 'info') {
    this.statusMessage = message;
    this.statusType = type;

    if (this.statusTimeout) {
      clearTimeout(this.statusTimeout);
    }

    // Auto-clear status after 3 seconds
    this.statusTimeout = setTimeout(() => {
      this.statusMessage = '';
    }, 3000);
  }

  copyToClipboard() {
    if (!navigator.clipboard) {
      this.showStatus('Clipboard is not available in this browser', 'error');
      return;
    }
    navigator.clipboard.writeText(this.editableXamlContent)
      .then(() => this.showStatus('XAML copied to clipboard', 'success'))
      .catch(() => this.showStatus('Failed to copy XAML to clipboard', 'error'));
  }

  downloadXaml() {
    const blob = new Blob([this.editableXamlContent], { type: 'text/xml' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'MainPage.xaml';
    link.click();
    window.URL.revokeObjectURL(url);
    this.showStatus('XAML file downloaded', 'success');
  }

  /** Loads a .xaml/.svg file from disk into the editor and applies it. */
  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.editableXamlContent = String(reader.result || '');
      this.applyXaml();
      input.value = '';
    };
    reader.onerror = () => {
      this.showStatus('Failed to read the selected file', 'error');
      input.value = '';
    };
    reader.readAsText(file);
  }

  resetXaml() {
    this.editableXamlContent = this.xamlGenerator.generateXaml(this.elementService.getRootElement());
    this.showStatus('XAML reset to current design', 'info');
  }
}
