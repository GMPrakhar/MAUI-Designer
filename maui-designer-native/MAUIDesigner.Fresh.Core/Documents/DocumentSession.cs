namespace MAUIDesigner.Fresh.Core.Documents;

public sealed class DocumentSession
{
    private readonly int _historyCapacity;
    private readonly LinkedList<DesignerDocument> _undo = [];
    private readonly Stack<DesignerDocument> _redo = [];

    public DocumentSession(DesignerDocument document, int historyCapacity = 100)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyCapacity);
        document.Validate();
        Current = document;
        _historyCapacity = historyCapacity;
    }

    public event EventHandler<DocumentChangedEventArgs>? Changed;

    public DesignerDocument Current { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Execute(IDocumentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        DesignerDocument next = command.Apply(Current);
        next.Validate();
        if (ReferenceEquals(Current, next) || Current == next)
        {
            return;
        }

        PushUndo(Current);
        _redo.Clear();
        Current = next;
        Changed?.Invoke(this, new DocumentChangedEventArgs(next, command.Description));
    }

    public bool Undo()
    {
        if (_undo.Last is null)
        {
            return false;
        }

        _redo.Push(Current);
        Current = _undo.Last.Value;
        _undo.RemoveLast();
        Changed?.Invoke(this, new DocumentChangedEventArgs(Current, "Undo"));
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out DesignerDocument? document))
        {
            return false;
        }

        PushUndo(Current);
        Current = document;
        Changed?.Invoke(this, new DocumentChangedEventArgs(Current, "Redo"));
        return true;
    }

    private void PushUndo(DesignerDocument document)
    {
        _undo.AddLast(document);
        if (_undo.Count > _historyCapacity)
        {
            _undo.RemoveFirst();
        }
    }
}

public sealed record DocumentChangedEventArgs(DesignerDocument Document, string Description);
