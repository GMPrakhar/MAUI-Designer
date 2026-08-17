using System;
using System.Collections.Generic;

using MauiDesigner.Core.Manifests;

namespace MauiDesigner.Core.Protocol
{
    /// <summary>
    /// The host-side half of the designer conversation, kept free of any Visual
    /// Studio types so it can be unit tested on any platform. The VSIX supplies
    /// the transport (WebView2) and the document services through the callbacks.
    /// </summary>
    public sealed class DesignerSession
    {
        private readonly Action<string> _post;
        private readonly string _hostKind;
        private bool _designerReady;
        private string? _pendingXaml;

        /// <param name="post">Sends a JSON payload into the WebView.</param>
        /// <param name="hostKind"><c>visual-studio</c> or <c>vscode</c>.</param>
        public DesignerSession(Action<string> post, string hostKind = "visual-studio")
        {
            _post = post ?? throw new ArgumentNullException(nameof(post));
            _hostKind = hostKind;
        }

        /// <summary>Path of the document currently shown in the designer.</summary>
        public string? FileName { get; private set; }

        /// <summary>The latest XAML the designer produced.</summary>
        public string? CurrentXaml { get; private set; }

        /// <summary>True when the designer has edits the host has not persisted.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>Raised when the designer asks the host to write the document.</summary>
        public event EventHandler<DocumentSaveRequestedEventArgs>? SaveRequested;

        /// <summary>Raised on every designer edit so the host can mark the buffer dirty.</summary>
        public event EventHandler<DocumentChangedEventArgs>? DocumentChanged;

        /// <summary>Raised when the designer reports a problem, for the output window.</summary>
        public event EventHandler<string>? ErrorReported;

        /// <summary>Raised when the designer asks for the project's control manifests.</summary>
        public event EventHandler? ManifestsRequested;

        /// <summary>
        /// Queues (or sends, once the designer is ready) the document to edit.
        /// </summary>
        public void OpenDocument(string xaml, string fileName)
        {
            FileName = fileName;
            CurrentXaml = xaml;
            IsDirty = false;

            if (_designerReady)
            {
                _post(DesignerProtocol.DocumentLoad(xaml, fileName));
            }
            else
            {
                // The WebView may still be starting; replay as soon as it announces itself.
                _pendingXaml = xaml;
            }
        }

        /// <summary>Pushes control manifests generated from the project's NuGet packages.</summary>
        public void PushManifests(IEnumerable<CustomControlManifest> manifests)
        {
            _post(DesignerProtocol.ManifestsPush(manifests));
        }

        /// <summary>Tells the designer the document reached disk.</summary>
        public void NotifySaved()
        {
            IsDirty = false;
            _post(DesignerProtocol.DocumentSaved());
        }

        /// <summary>Handles a raw message posted by the designer. Unknown payloads are ignored.</summary>
        public void HandleMessage(string? json)
        {
            var message = DesignerProtocol.Parse(json);
            if (message is null)
            {
                return;
            }

            switch (message.Type)
            {
                case MessageTypes.DesignerReady:
                    _designerReady = true;
                    _post(DesignerProtocol.HostReady(_hostKind, FileName));
                    if (_pendingXaml is not null)
                    {
                        _post(DesignerProtocol.DocumentLoad(_pendingXaml, FileName));
                        _pendingXaml = null;
                    }
                    break;

                case MessageTypes.ManifestsRequest:
                    ManifestsRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case MessageTypes.DocumentChanged:
                    if (message.Xaml is null || message.Xaml == CurrentXaml)
                    {
                        break;
                    }

                    CurrentXaml = message.Xaml;
                    IsDirty = true;
                    DocumentChanged?.Invoke(this, new DocumentChangedEventArgs(message.Xaml));
                    break;

                case MessageTypes.DocumentSave:
                    var xaml = message.Xaml ?? CurrentXaml;
                    if (xaml is null)
                    {
                        break;
                    }

                    CurrentXaml = xaml;
                    SaveRequested?.Invoke(this, new DocumentSaveRequestedEventArgs(xaml, FileName));
                    break;

                case MessageTypes.DesignerError:
                    ErrorReported?.Invoke(this, message.Message ?? "Unknown designer error");
                    break;
            }
        }
    }

    public sealed class DocumentChangedEventArgs : EventArgs
    {
        public DocumentChangedEventArgs(string xaml) => Xaml = xaml;

        public string Xaml { get; }
    }

    public sealed class DocumentSaveRequestedEventArgs : EventArgs
    {
        public DocumentSaveRequestedEventArgs(string xaml, string? fileName)
        {
            Xaml = xaml;
            FileName = fileName;
        }

        public string Xaml { get; }

        public string? FileName { get; }
    }
}
