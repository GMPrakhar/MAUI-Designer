import { TestBed } from '@angular/core/testing';

import { HostBridgeService, HostInboundMessage } from './host-bridge';

describe('HostBridgeService', () => {
  const originalChrome = (window as any).chrome;
  const originalAcquire = (window as any).acquireVsCodeApi;

  afterEach(() => {
    (window as any).chrome = originalChrome;
    (window as any).acquireVsCodeApi = originalAcquire;
  });

  function create(): HostBridgeService {
    const webview = (window as any).chrome?.webview;
    if (webview && typeof webview.addEventListener !== 'function') {
      webview.addEventListener = window.addEventListener.bind(window);
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    return TestBed.inject(HostBridgeService);
  }

  function post(message: unknown) {
    window.dispatchEvent(new MessageEvent('message', { data: message }));
  }

  it('reports a plain browser when no host is present', () => {
    (window as any).chrome = undefined;
    (window as any).acquireVsCodeApi = undefined;

    const service = create();

    expect(service.host).toBe('browser');
    expect(service.isHosted).toBeFalse();
  });

  it('sending a message in a browser is a no-op', () => {
    (window as any).chrome = undefined;
    (window as any).acquireVsCodeApi = undefined;

    const service = create();

    expect(() => service.save('<ContentPage />')).not.toThrow();
  });

  it('detects Visual Studio through the WebView2 bridge', () => {
    const posted: unknown[] = [];
    (window as any).chrome = { webview: { postMessage: (m: unknown) => posted.push(m) } };
    (window as any).acquireVsCodeApi = undefined;

    const service = create();

    expect(service.host).toBe('visual-studio');
    expect(service.isHosted).toBeTrue();
    expect(posted).toEqual([]);

    service.start();

    expect(posted).toEqual([JSON.stringify({ type: 'designer.ready' })]);
  });

  it('serialises messages to Visual Studio as JSON strings', () => {
    const posted: string[] = [];
    (window as any).chrome = { webview: { postMessage: (m: string) => posted.push(m) } };

    const service = create();
    service.start();
    service.save('<ContentPage />');

    expect(JSON.parse(posted[1])).toEqual({ type: 'document.save', xaml: '<ContentPage />' });
  });

  it('detects VS Code and posts structured messages', () => {
    (window as any).chrome = undefined;
    const posted: unknown[] = [];
    (window as any).acquireVsCodeApi = () => ({ postMessage: (m: unknown) => posted.push(m) });

    const service = create();
    service.start();
    service.notifyChanged('<ContentPage />');

    expect(service.host).toBe('vscode');
    expect(posted).toEqual([
      { type: 'designer.ready' },
      { type: 'document.changed', xaml: '<ContentPage />' }
    ]);
  });

  it('acquires the VS Code API only once', () => {
    (window as any).chrome = undefined;
    let calls = 0;
    (window as any).acquireVsCodeApi = () => {
      calls++;
      return { postMessage: () => undefined };
    };

    const service = create();
    service.notifyChanged('a');
    service.notifyChanged('b');

    expect(calls).toBe(1);
    expect(service.host).toBe('vscode');
  });

  it('subscribes before starting the host handshake', () => {
    (window as any).chrome = {
      webview: {
        postMessage: (message: string) => {
          if (JSON.parse(message).type === 'designer.ready') {
            post({ type: 'document.load', xaml: '<ContentPage><Label /></ContentPage>' });
          }
        }
      }
    };
    const service = create();
    const received: HostInboundMessage[] = [];
    service.messages$.subscribe(message => received.push(message));

    service.start();
    service.start();

    expect(received.map(message => message.type)).toEqual(['document.load']);
  });

  it('forwards messages received from the host', () => {
    (window as any).chrome = { webview: { postMessage: () => undefined } };
    const service = create();

    const received: HostInboundMessage[] = [];
    service.messages$.subscribe(message => received.push(message));

    post({ type: 'document.load', xaml: '<ContentPage />', fileName: 'MainPage.xaml' });

    expect(received).toEqual([
      { type: 'document.load', xaml: '<ContentPage />', fileName: 'MainPage.xaml' } as HostInboundMessage
    ]);
  });

  it('accepts JSON encoded messages, as WebView2 delivers them', () => {
    (window as any).chrome = { webview: { postMessage: () => undefined } };
    const service = create();

    const received: HostInboundMessage[] = [];
    service.messages$.subscribe(message => received.push(message));

    post(JSON.stringify({ type: 'document.load', xaml: '<Grid />' }));

    expect(received.length).toBe(1);
    expect((received[0] as { xaml: string }).xaml).toBe('<Grid />');
  });

  it('ignores malformed or unrelated window messages', () => {
    (window as any).chrome = { webview: { postMessage: () => undefined } };
    const service = create();

    const received: HostInboundMessage[] = [];
    service.messages$.subscribe(message => received.push(message));

    post('not json at all');
    post({ nope: true });
    post(null);
    post(42);

    expect(received).toEqual([]);
  });

  it('tracks the file name the host opened', () => {
    (window as any).chrome = { webview: { postMessage: () => undefined } };
    const service = create();

    expect(service.fileName).toBeNull();

    post({ type: 'host.ready', host: 'visual-studio', fileName: 'Views/LoginPage.xaml' });

    expect(service.fileName).toBe('Views/LoginPage.xaml');
  });

  it('lets the host correct the detected kind', () => {
    (window as any).chrome = { webview: { postMessage: () => undefined } };
    const service = create();

    post({ type: 'host.ready', host: 'vscode' });

    expect(service.host).toBe('vscode');
  });

  it('does not listen for host messages in a plain browser', () => {
    (window as any).chrome = undefined;
    (window as any).acquireVsCodeApi = undefined;
    const service = create();

    const received: HostInboundMessage[] = [];
    service.messages$.subscribe(message => received.push(message));

    post({ type: 'document.load', xaml: '<ContentPage />' });

    expect(received).toEqual([]);
  });
});
