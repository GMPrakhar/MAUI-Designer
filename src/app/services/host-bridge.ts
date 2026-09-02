import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';

import { CustomControlManifest } from '../models/custom-control';

/** Messages the host (Visual Studio / VS Code) sends into the designer. */
export type HostInboundMessage =
  | { type: 'host.ready'; host: HostKind; fileName?: string }
  | { type: 'document.load'; xaml: string; fileName?: string }
  | { type: 'manifests.push'; manifests: CustomControlManifest[] }
  | { type: 'document.saved' };

/** Messages the designer sends back to the host. */
export type HostOutboundMessage =
  | { type: 'designer.ready' }
  | { type: 'document.changed'; xaml: string }
  | { type: 'document.save'; xaml: string }
  | { type: 'manifests.request' }
  | { type: 'designer.error'; message: string };

export type HostKind = 'visual-studio' | 'vscode' | 'browser';

interface VsCodeApi {
  postMessage(message: unknown): void;
}

interface VisualStudioWebView {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: (event: MessageEvent) => void): void;
}

declare global {
  interface Window {
    chrome?: { webview?: VisualStudioWebView };
    acquireVsCodeApi?: () => VsCodeApi;
  }
}

/**
 * Normalises the differences between the hosts the designer can run in.
 *
 * - Visual Studio hosts the built app in a WebView2 control and exchanges JSON through
 *   `window.chrome.webview`.
 * - VS Code hosts it in a webview and exchanges JSON through `acquireVsCodeApi()`.
 * - In a plain browser the designer is standalone and every send is a no-op.
 *
 * Everything above this service is host agnostic.
 */
@Injectable({ providedIn: 'root' })
export class HostBridgeService {
  private readonly hostSubject: BehaviorSubject<HostKind>;
  private readonly messageSubject = new Subject<HostInboundMessage>();
  private readonly fileNameSubject = new BehaviorSubject<string | null>(null);
  private vsCodeApi: VsCodeApi | null = null;
  private started = false;

  /** The host the designer is currently running in. */
  readonly host$: Observable<HostKind>;
  /** Messages pushed by the host. */
  readonly messages$ = this.messageSubject.asObservable();
  /** The document the host has opened, when there is one. */
  readonly fileName$ = this.fileNameSubject.asObservable();

  constructor() {
    this.hostSubject = new BehaviorSubject<HostKind>(this.detectHost());
    this.host$ = this.hostSubject.asObservable();

    if (this.host === 'visual-studio') {
      window.chrome!.webview!.addEventListener('message', this.onWindowMessage);
    } else if (this.host === 'vscode') {
      window.addEventListener('message', this.onWindowMessage);
    }
  }

  get host(): HostKind {
    return this.hostSubject.value;
  }

  /** True when the designer is embedded in an IDE rather than running standalone. */
  get isHosted(): boolean {
    return this.host !== 'browser';
  }

  get fileName(): string | null {
    return this.fileNameSubject.value;
  }

  /** Starts the host handshake after consumers have subscribed to inbound messages. */
  start(): void {
    if (this.started || !this.isHosted) {
      return;
    }

    this.started = true;
    this.send({ type: 'designer.ready' });
  }

  /** Sends a message to the host. Does nothing in a plain browser. */
  send(message: HostOutboundMessage): void {
    switch (this.host) {
      case 'visual-studio':
        window.chrome!.webview!.postMessage(JSON.stringify(message));
        return;
      case 'vscode':
        this.vsCodeApi?.postMessage(message);
        return;
      default:
        return;
    }
  }

  /** Asks the host to persist the given XAML into the open document. */
  save(xaml: string): void {
    this.send({ type: 'document.save', xaml });
  }

  /** Tells the host the design changed so it can mark the document dirty. */
  notifyChanged(xaml: string): void {
    this.send({ type: 'document.changed', xaml });
  }

  /** Asks the host for control manifests generated from the project's NuGet packages. */
  requestManifests(): void {
    this.send({ type: 'manifests.request' });
  }

  reportError(message: string): void {
    this.send({ type: 'designer.error', message });
  }

  /** Entry point for host messages from WebView2 or VS Code. */
  private readonly onWindowMessage = (event: MessageEvent) => {
    const message = this.parse(event.data);
    if (!message) {
      return;
    }

    if (message.type === 'host.ready') {
      this.hostSubject.next(message.host);
    }
    if ((message.type === 'host.ready' || message.type === 'document.load') && message.fileName) {
      this.fileNameSubject.next(message.fileName);
    }

    this.messageSubject.next(message);
  };

  private parse(data: unknown): HostInboundMessage | null {
    let payload: unknown = data;
    if (typeof payload === 'string') {
      try {
        payload = JSON.parse(payload);
      } catch {
        return null;
      }
    }

    if (!payload || typeof payload !== 'object' || typeof (payload as { type?: unknown }).type !== 'string') {
      return null;
    }
    return payload as HostInboundMessage;
  }

  private detectHost(): HostKind {
    if (typeof window === 'undefined') {
      return 'browser';
    }
    if (window.chrome?.webview) {
      return 'visual-studio';
    }
    if (typeof window.acquireVsCodeApi === 'function') {
      this.vsCodeApi = window.acquireVsCodeApi();
      return 'vscode';
    }
    return 'browser';
  }
}
