import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ElementService } from '../../services/element';
import { XamlGeneratorService } from '../../services/xaml-generator';
import { MauiElement } from '../../models/maui-element';
import { Observable, map } from 'rxjs';

@Component({
  selector: 'app-xaml-editor',
  imports: [CommonModule, FormsModule],
  templateUrl: './xaml-editor.html',
  styleUrl: './xaml-editor.scss'
})
export class XamlEditorComponent implements OnInit {
  xamlContent$: Observable<string>;
  
  constructor(
    private elementService: ElementService,
    private xamlGenerator: XamlGeneratorService
  ) {
    this.xamlContent$ = this.elementService.elements$.pipe(
      map(rootElement => this.xamlGenerator.generateXaml(rootElement))
    );
  }

  ngOnInit() {
    // Initialize XAML editor
  }

  copyToClipboard() {
    this.xamlContent$.subscribe(xaml => {
      navigator.clipboard.writeText(xaml).then(() => {
        // Could show a toast notification here
        console.log('XAML copied to clipboard');
      });
    });
  }

  downloadXaml() {
    this.xamlContent$.subscribe(xaml => {
      const blob = new Blob([xaml], { type: 'text/xml' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'MainPage.xaml';
      link.click();
      window.URL.revokeObjectURL(url);
    });
  }
}
