namespace MAUIDesigner.Core.Elements
{
    /// <summary>
    /// Service for managing element creation and operations
    /// </summary>
    public class ElementService : IElementOperations
    {
        private readonly Dictionary<string, IElementFactory> _elementFactories;
        private readonly Services.ICursorService _cursorService;

        public ElementService(Services.ICursorService cursorService)
        {
            _cursorService = cursorService;
            _elementFactories = InitializeElementFactories();
        }

        private Dictionary<string, IElementFactory> InitializeElementFactories()
        {
            var factories = new Dictionary<string, IElementFactory>();
            
            // Register all available element factories
            var factoryTypes = new[]
            {
                new LabelElementFactory(),
                new EditorElementFactory(),
                new StackLayoutElementFactory(),
                new ButtonElementFactory(),
                new EntryElementFactory(),
                new GridElementFactory(),
                new RectangleElementFactory()
            };

            foreach (var factory in factoryTypes)
            {
                factories[factory.DisplayName] = factory;
            }

            return factories;
        }

        /// <summary>
        /// Gets all available element factories grouped by category
        /// </summary>
        public IEnumerable<IGrouping<string, IElementFactory>> GetElementFactoriesByCategory()
        {
            return _elementFactories.Values.GroupBy(f => f.Category);
        }

        /// <summary>
        /// Creates an element by name
        /// </summary>
        public View CreateElement(string elementName)
        {
            if (_elementFactories.TryGetValue(elementName, out var factory))
            {
                return factory.CreateElement();
            }
            
            throw new ArgumentException($"Unknown element type: {elementName}");
        }

        public void CreateElementInDesignerFrame(Layout mainDesignerView, string elementType)
        {
            try
            {
                var newElement = CreateElement(elementType);
                var border = new HelperViews.ElementDesignerView(newElement);

                AddDesignerGestureControls(border);
                DnDHelper.DragAndDropOperations.OnFocusChanged(border);
                mainDesignerView.Add(border);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating element in designer frame: {ex.Message}");
            }
        }

        public void AddDesignerGestureControls(HelperViews.ElementDesignerView newElement)
        {
            var rightClickRecognizer = new TapGestureRecognizer();
            rightClickRecognizer.Tapped += ToolBox.ShowContextMenu;
            rightClickRecognizer.Buttons = ButtonsMask.Secondary;

            // Cursor changes to pointer on an Element
            var pointerGestureRecognizer = new PointerGestureRecognizer();
            pointerGestureRecognizer.PointerEntered += (s, e) => _cursorService.SetCursor(s as View, CursorType.Hand);
            pointerGestureRecognizer.PointerExited += (s, e) => _cursorService.SetCursor(s as View, CursorType.Arrow);

            if (newElement.View is Layout)
            {
                var dropGestureRecognizer = new DropGestureRecognizer();
                dropGestureRecognizer.Drop += DnDHelper.DragAndDropOperations.OnDrop;
                newElement.GestureRecognizers.Add(dropGestureRecognizer);
            }

            newElement.GestureRecognizers.Add(rightClickRecognizer);
            newElement.GestureRecognizers.Add(pointerGestureRecognizer);
        }
    }
}