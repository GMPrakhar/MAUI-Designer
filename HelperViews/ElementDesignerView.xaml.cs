using CommunityToolkit.Maui.Views;
using MAUIDesigner.DnDHelper;
using MAUIDesigner.Interfaces;
using MAUIDesigner.LayoutDesigners;
using MAUIDesigner.NewFolder;
using System.Runtime.CompilerServices;
using static MAUIDesigner.DnDHelper.ScalingHelper;
using static MAUIDesigner.MainPage;

namespace MAUIDesigner.HelperViews;

public partial class ElementDesignerView : ContentView
{
    private string[] allowedProperties = { nameof(View.HeightRequest), nameof(View.MinimumHeightRequest), nameof(View.MaximumHeightRequest), nameof(View.WidthRequest), nameof(View.MinimumWidthRequest), nameof(View.MaximumWidthRequest), nameof(View.Margin) };

    public static readonly BindableProperty ViewProperty = BindableProperty.Create(nameof(View), typeof(View), typeof(ElementDesignerView));

    public Type[] allowedHoverables = new[] { typeof(Grid) };

    private bool AllowOperations = false;

    public View EncapsulatingViewProperty => EncapsulatingView;

    public IHoverable? hoverController { get; }

    public View View
    {
        get { return (View)GetValue(ViewProperty); }
        set { SetValue(ViewProperty, value); }
    }

    public ElementDesignerView()
    {
        InitializeComponent();
        DragAndDropOperations.OnFocusChanged += OnFocusChanged;
    }

    public ElementDesignerView(View? loadedView) : this()
    {
        this.View = loadedView;
        Grid.SetRow(this, Grid.GetRow(loadedView));
        Grid.SetColumn(this, Grid.GetColumn(loadedView));
        Grid.SetRowSpan(this, Grid.GetRowSpan(loadedView));
        Grid.SetColumnSpan(this, Grid.GetColumnSpan(loadedView));
        EncapsulatingView.Margin = loadedView.Margin;
        EncapsulatingView.HeightRequest = loadedView.HeightRequest;
        EncapsulatingView.WidthRequest = loadedView.WidthRequest;
        this.View.Layout(this.View.Bounds);
        Padding = 0;

        loadedView.Margin = 0;
        loadedView.HeightRequest = -1;
        loadedView.WidthRequest = -1;

        if(allowedHoverables.Contains(loadedView.GetType()))
        {
            hoverController = HoverableFactory.GetHoverController(loadedView);

            // Add pointer move gesture recognizer to the loaded view
            var dragGestureRecognizer = new DropGestureRecognizer();
            dragGestureRecognizer.DragOver += (s, e) =>
            {
                hoverController.OnHoverMove(e.GetPosition(loadedView).Value);
            };

            dragGestureRecognizer.DragLeave += (s, e) => hoverController.OnHoverExit();
            dragGestureRecognizer.Drop += (s, e) => hoverController.OnHoverExit();

            this.GestureRecognizers.Add(dragGestureRecognizer);
        }

        loadedView.PropertyChanged += LoadedView_PropertyChanged;
    }

    private void LoadedView_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is View senderView && allowedProperties.Contains(e.PropertyName))
        {
            // Set the property with reflection
            var propertyInfo = this.GetType().GetProperty(e.PropertyName);
            if (propertyInfo != null)
            {
                var value = propertyInfo.GetValue(senderView);
                propertyInfo.SetValue(this, value);
                this.InvalidateMeasure();
            }
        }
    }

    private void DragGestureRecognizer_DragStarting(object sender, DragStartingEventArgs e)
    {
        if (AllowOperations)
        {
            var draggingView = (sender as GestureRecognizer).Parent as View;
            e.Data.Properties.Add("DraggingView", draggingView);

            // Add location of the pointer inside the view to the drag data
            // It should be taken from the current view
            var location = e.GetPosition(View).Value;
            e.Data.Properties.Add("DragLocation", location);
        }
    }

    public void OnFocusChanged(object sender)
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
        // Show all rectangles, and set the border stroke to yellow
        topLeftRect.IsVisible = true;
        topRightRect.IsVisible = true;
        bottomLeftRect.IsVisible = true;
        bottomRightRect.IsVisible = true;
        ElementBorder.Stroke = Colors.Yellow;
        AllowOperations = true;
        
    }

    private void OnFocusLost()
    {
        // Hide all rectangles, and set the border stroke to transparent
        topLeftRect.IsVisible = false;
        topRightRect.IsVisible = false;
        bottomLeftRect.IsVisible = false;
        bottomRightRect.IsVisible = false;
        ElementBorder.Stroke = Colors.Transparent;
        AllowOperations = false;
    }

    private void ScaleDirectionScaled(DragStartingEventArgs e, ScaleDirection scaleDirection)
    {
        if (AllowOperations)
        {
            // Add properties for the receiver
            e.Data.Properties.Add("ScaleDirection", scaleDirection);
            e.Data.Properties.Add("IsScaling", true);
            e.Data.Properties.Add("DraggingView", EncapsulatingView);
        }
    }

    private void TopLeftScaled(object sender, DragStartingEventArgs e)
    {
        ScaleDirectionScaled(e, ScaleDirection.TopLeft);
    }

    private void TopRightScaled(object sender, DragStartingEventArgs e)
    {
        ScaleDirectionScaled(e, ScaleDirection.TopRight);
    }

    private void BottomLeftScaled(object sender, DragStartingEventArgs e)
    {
        ScaleDirectionScaled(e, ScaleDirection.BottomLeft);
    }

    private void BottomRightScaled(object sender, DragStartingEventArgs e)
    {
        ScaleDirectionScaled(e, ScaleDirection.BottomRight);
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        DragAndDropOperations.OnFocusChanged(this);
    }
}
