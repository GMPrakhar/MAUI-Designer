using MAUIDesigner.DnDHelper;
using MAUIDesigner.Interfaces;
using static MAUIDesigner.DnDHelper.ScalingHelper;

namespace MAUIDesigner.HelperViews;

public partial class ElementDesignerView : ContentView
{
    private static readonly string[] AllowedProperties =
    {
        nameof(View.HeightRequest),
        nameof(View.MinimumHeightRequest),
        nameof(View.MaximumHeightRequest),
        nameof(View.WidthRequest),
        nameof(View.MinimumWidthRequest),
        nameof(View.MaximumWidthRequest),
        nameof(View.Margin)
    };

    public static readonly BindableProperty ViewProperty = BindableProperty.Create(nameof(View), typeof(View), typeof(ElementDesignerView));

    public Type[] allowedHoverables = [typeof(Grid)];

    private bool AllowOperations = false;

    public View EncapsulatingViewProperty => EncapsulatingView;

    public IHoverable? hoverController { get; }

    public View View
    {
        get => (View)GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    public ElementDesignerView()
    {
        InitializeComponent();
        DragAndDropOperations.OnFocusChanged += OnFocusChanged;
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += TapGestureRecognizer_Tapped;
        GestureRecognizers.Add(tapGesture);
        OnFocusLost();
    }

    public ElementDesignerView(View? loadedView) : this()
    {
        ArgumentNullException.ThrowIfNull(loadedView);

        View = loadedView;
        Grid.SetRow(this, Grid.GetRow(loadedView));
        Grid.SetColumn(this, Grid.GetColumn(loadedView));
        Grid.SetRowSpan(this, Grid.GetRowSpan(loadedView));
        Grid.SetColumnSpan(this, Grid.GetColumnSpan(loadedView));
        EncapsulatingView.Margin = loadedView.Margin;
        EncapsulatingView.HeightRequest = loadedView.HeightRequest;
        EncapsulatingView.WidthRequest = loadedView.WidthRequest;
        Padding = 0;

        loadedView.Margin = 0;
        loadedView.HeightRequest = -1;
        loadedView.WidthRequest = -1;

        if (allowedHoverables.Contains(loadedView.GetType()))
        {
            hoverController = HoverableFactory.GetHoverController(loadedView);

            var dragGestureRecognizer = new DropGestureRecognizer();
            dragGestureRecognizer.DragOver += (_, e) =>
            {
                var pos = e.GetPosition(loadedView);
                if (pos is Point point)
                {
                    hoverController?.OnHoverMove(point);
                }
            };
            dragGestureRecognizer.DragLeave += (_, _) => hoverController?.OnHoverExit();
            dragGestureRecognizer.Drop += (_, _) => hoverController?.OnHoverExit();
            GestureRecognizers.Add(dragGestureRecognizer);
        }

        loadedView.PropertyChanged += LoadedView_PropertyChanged;
    }

    private void LoadedView_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not View senderView || e.PropertyName is null || !AllowedProperties.Contains(e.PropertyName))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(View.HeightRequest):
                EncapsulatingView.HeightRequest = senderView.HeightRequest;
                break;
            case nameof(View.WidthRequest):
                EncapsulatingView.WidthRequest = senderView.WidthRequest;
                break;
            case nameof(View.Margin):
                EncapsulatingView.Margin = senderView.Margin;
                break;
            case nameof(View.MinimumHeightRequest):
                EncapsulatingView.MinimumHeightRequest = senderView.MinimumHeightRequest;
                break;
            case nameof(View.MinimumWidthRequest):
                EncapsulatingView.MinimumWidthRequest = senderView.MinimumWidthRequest;
                break;
            case nameof(View.MaximumHeightRequest):
                EncapsulatingView.MaximumHeightRequest = senderView.MaximumHeightRequest;
                break;
            case nameof(View.MaximumWidthRequest):
                EncapsulatingView.MaximumWidthRequest = senderView.MaximumWidthRequest;
                break;
        }
        InvalidateMeasure();
        RefreshSizeLabel();
    }

    private void DragGestureRecognizer_DragStarting(object sender, DragStartingEventArgs e)
    {
        if (!AllowOperations)
        {
            return;
        }

        e.Data.Properties["DraggingView"] = this;
        var location = e.GetPosition(this) ?? Point.Zero;
        e.Data.Properties["DragLocation"] = location;
    }

    public void OnFocusChanged(object? sender)
    {
        if (sender == this)
        {
            OnFocused();
        }
        else
        {
            OnFocusLost();
        }
    }

    private void OnFocused()
    {
        topLeftRect.IsVisible = true;
        topRightRect.IsVisible = true;
        bottomLeftRect.IsVisible = true;
        bottomRightRect.IsVisible = true;
        topEdgeRect.IsVisible = true;
        bottomEdgeRect.IsVisible = true;
        leftEdgeRect.IsVisible = true;
        rightEdgeRect.IsVisible = true;
        ElementBorder.Stroke = Color.FromArgb("#8B7CFF");
        AllowOperations = true;
        RefreshSizeLabel();
    }

    private void OnFocusLost()
    {
        topLeftRect.IsVisible = false;
        topRightRect.IsVisible = false;
        bottomLeftRect.IsVisible = false;
        bottomRightRect.IsVisible = false;
        topEdgeRect.IsVisible = false;
        bottomEdgeRect.IsVisible = false;
        leftEdgeRect.IsVisible = false;
        rightEdgeRect.IsVisible = false;
        sizeLabel.IsVisible = false;
        ElementBorder.Stroke = Colors.Transparent;
        AllowOperations = false;
    }

    public void RefreshSizeLabel()
    {
        if (EncapsulatingView == null)
        {
            return;
        }

        var width = EncapsulatingView.WidthRequest > 0 ? EncapsulatingView.WidthRequest : EncapsulatingView.Width;
        var height = EncapsulatingView.HeightRequest > 0 ? EncapsulatingView.HeightRequest : EncapsulatingView.Height;
        sizeLabel.Text = $"{width:F0} × {height:F0}";
        sizeLabel.IsVisible = AllowOperations;
    }

    private void ScaleDirectionScaled(DragStartingEventArgs e, ScaleDirection scaleDirection)
    {
        if (!AllowOperations)
        {
            return;
        }

        e.Data.Properties["ScaleDirection"] = scaleDirection;
        e.Data.Properties["IsScaling"] = true;
        e.Data.Properties["DraggingView"] = EncapsulatingView;
        sizeLabel.IsVisible = true;
    }

    private void TopLeftScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.TopLeft);
    private void TopRightScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.TopRight);
    private void BottomLeftScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.BottomLeft);
    private void BottomRightScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.BottomRight);
    private void TopEdgeScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.Top);
    private void BottomEdgeScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.Bottom);
    private void LeftEdgeScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.Left);
    private void RightEdgeScaled(object sender, DragStartingEventArgs e) => ScaleDirectionScaled(e, ScaleDirection.Right);

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        DragAndDropOperations.OnFocusChanged?.Invoke(this);
        ContextMenu.SetCurrentSelectedElement(this);
    }
}
