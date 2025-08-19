import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ElementService } from '../../services/element';
import { XamlGeneratorService } from '../../services/xaml-generator';
import { XamlParserService } from '../../services/xaml-parser';
import { MauiElement } from '../../models/maui-element';
import { Observable, map } from 'rxjs';

@Component({
  selector: 'app-xaml-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './xaml-editor.html',
  styleUrl: './xaml-editor.scss'
})
export class XamlEditorComponent implements OnInit {
  xamlContent$: Observable<string>;
  editableXamlContent: string = '';
  statusMessage: string = '';
  statusType: 'success' | 'error' | 'info' = 'info';
  private statusTimeout: any;
  
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
    // Initialize with current XAML content
    this.xamlContent$.subscribe(xaml => {
      this.editableXamlContent = xaml;
    });
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
    
    // Clear any existing timeout
    if (this.statusTimeout) {
      clearTimeout(this.statusTimeout);
    }
    
    // Auto-clear status after 3 seconds
    this.statusTimeout = setTimeout(() => {
      this.statusMessage = '';
    }, 3000);
  }

  copyToClipboard() {
    navigator.clipboard.writeText(this.editableXamlContent).then(() => {
      this.showStatus('XAML copied to clipboard', 'success');
    });
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

  resetXaml() {
    this.xamlContent$.subscribe(xaml => {
      this.editableXamlContent = xaml;
      this.showStatus('XAML reset to current design', 'info');
    });
  }
}
