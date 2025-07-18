using CommunityToolkit.Maui.Views;
using MAUIDesigner.HelperViews;
using MAUIDesigner.Interfaces;
using Microsoft.Maui.Controls.Shapes;
using Windows.Devices.Input;
using MAUIDesigner.DnDHelper;

namespace MAUIDesigner.LayoutDesigners
{
    class GridLayoutDesigner: ILayoutDesigner
    {
        public Grid Grid { get; }

        public Rectangle highlighter = new()
        {
            WidthRequest = -1,
            HeightRequest = -1,
            BackgroundColor = Colors.AliceBlue,
        };

        private List<Line> columnLines = new();
        private List<Line> rowLines = new();
        private List<Rectangle> columnDividers = new();
        private List<Rectangle> rowDividers = new();

        public GridLayoutDesigner(Grid grid)
        {
            this.Grid = grid ?? throw new ArgumentNullException(nameof(grid));

            // Set lighter background for grid
            if (Grid.BackgroundColor == null || Grid.BackgroundColor == Colors.Transparent)
            {
                Grid.BackgroundColor = Colors.LightGray.WithAlpha(0.1f);
            }

            // Initialize the highlighter
            highlighter.IsVisible = false;
            Grid.Add(highlighter as IView);

            // Initialize grid lines
            InitializeGridLines();
        }

        // Assume ElementDesigner View is getting dropped on the Grid, now we need to compute the column and row it belongs to and update the relevant property
        public void OnDrop(View view, Point location)
        {
            if (view == null)
            {
                return;
            }

            // Check if drop location is outside grid boundaries
            if (IsOutsideGrid(location))
            {
                HandleDropOutsideGrid(view);
                return;
            }

            (view.Parent.Parent as Layout).Remove(view.Parent as View);
            Grid.Add(view.Parent as View);

            // Compute the column and row
            UpdateColumnAndRowForView(view.Parent as ElementDesignerView, location);
        }

        private bool IsOutsideGrid(Point location)
        {
            return location.X < 0 || location.Y < 0 || 
                   location.X > Grid.Width || location.Y > Grid.Height;
        }

        private void HandleDropOutsideGrid(View view)
        {
            // Remove from grid and add to base layout
            if (view.Parent?.Parent == Grid)
            {
                Grid.Remove(view.Parent as View);
                
                // Add to base layout if available
                if (DragAndDropOperations.BaseLayout != null)
                {
                    DragAndDropOperations.BaseLayout.Add(view.Parent as View);
                    
                    // Reset grid properties
                    if (view.Parent is ElementDesignerView elementDesigner)
                    {
                        Grid.SetColumn(elementDesigner, 0);
                        Grid.SetRow(elementDesigner, 0);
                        Grid.SetColumnSpan(elementDesigner, 1);
                        Grid.SetRowSpan(elementDesigner, 1);
                    }
                }
            }
        }

        private void InitializeGridLines()
        {
            // Clear existing lines
            foreach (var line in columnLines)
            {
                Grid.Remove(line);
            }
            foreach (var line in rowLines)
            {
                Grid.Remove(line);
            }
            foreach (var divider in columnDividers)
            {
                Grid.Remove(divider);
            }
            foreach (var divider in rowDividers)
            {
                Grid.Remove(divider);
            }

            columnLines.Clear();
            rowLines.Clear();
            columnDividers.Clear();
            rowDividers.Clear();

            // Add column lines
            for (int i = 1; i < Grid.ColumnDefinitions.Count; i++)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 0,
                    Y2 = 1,
                    Stroke = Colors.Gray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Center,
                    IsHitTestVisible = false,
                    ZIndex = 500
                };
                
                Grid.SetColumn(line, i);
                Grid.SetRowSpan(line, Grid.RowDefinitions.Count);
                Grid.Add(line);
                columnLines.Add(line);

                // Add draggable divider for column resizing
                var divider = new Rectangle
                {
                    WidthRequest = 4,
                    Fill = Colors.Transparent,
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Center,
                    ZIndex = 600
                };

                var dragGesture = new DragGestureRecognizer();
                int columnIndex = i;
                dragGesture.DragStarting += (s, e) => OnColumnDragStarting(s, e, columnIndex);
                divider.GestureRecognizers.Add(dragGesture);

                Grid.SetColumn(divider, i);
                Grid.SetRowSpan(divider, Grid.RowDefinitions.Count);
                Grid.Add(divider);
                columnDividers.Add(divider);
            }

            // Add row lines
            for (int i = 1; i < Grid.RowDefinitions.Count; i++)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 1,
                    Y2 = 0,
                    Stroke = Colors.Gray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Fill,
                    IsHitTestVisible = false,
                    ZIndex = 500
                };

                Grid.SetRow(line, i);
                Grid.SetColumnSpan(line, Grid.ColumnDefinitions.Count);
                Grid.Add(line);
                rowLines.Add(line);

                // Add draggable divider for row resizing
                var divider = new Rectangle
                {
                    HeightRequest = 4,
                    Fill = Colors.Transparent,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Fill,
                    ZIndex = 600
                };

                var dragGesture = new DragGestureRecognizer();
                int rowIndex = i;
                dragGesture.DragStarting += (s, e) => OnRowDragStarting(s, e, rowIndex);
                divider.GestureRecognizers.Add(dragGesture);

                Grid.SetRow(divider, i);
                Grid.SetColumnSpan(divider, Grid.ColumnDefinitions.Count);
                Grid.Add(divider);
                rowDividers.Add(divider);
            }
        }

        private void OnColumnDragStarting(object sender, DragStartingEventArgs e, int columnIndex)
        {
            e.Data.Properties.Add("IsResizingColumn", true);
            e.Data.Properties.Add("ColumnIndex", columnIndex);
            e.Data.Properties.Add("GridDesigner", this);
        }

        private void OnRowDragStarting(object sender, DragStartingEventArgs e, int rowIndex)
        {
            e.Data.Properties.Add("IsResizingRow", true);
            e.Data.Properties.Add("RowIndex", rowIndex);
            e.Data.Properties.Add("GridDesigner", this);
        }

        public void ResizeColumn(int columnIndex, double newWidth)
        {
            if (columnIndex > 0 && columnIndex < Grid.ColumnDefinitions.Count)
            {
                var columnDef = Grid.ColumnDefinitions[columnIndex - 1];
                columnDef.Width = new GridLength(Math.Max(20, newWidth));
                
                // Refresh grid lines
                InitializeGridLines();
            }
        }

        public void ResizeRow(int rowIndex, double newHeight)
        {
            if (rowIndex > 0 && rowIndex < Grid.RowDefinitions.Count)
            {
                var rowDef = Grid.RowDefinitions[rowIndex - 1];
                rowDef.Height = new GridLength(Math.Max(20, newHeight));
                
                // Refresh grid lines
                InitializeGridLines();
            }
        }

        private void UpdateColumnAndRowForView(ElementDesignerView elementDesigner, Point location)
        {
            // Compute the column and row
            if (elementDesigner == null)
            {
                return;
            }

            var (column, row) = ComputerColumnAndRowForPoint(location);

            // Update the column and row
            Grid.SetColumn(elementDesigner as IView, column);
            Grid.SetRow(elementDesigner as IView, row);

            // Update the margin
            elementDesigner.EncapsulatingViewProperty.Margin = 0;
        }

        private (int, int) ComputerColumnAndRowForPoint(Point location)
        {
            int column = 0, row = 0;

            // Compute the column
            var x = location.X;
            var columnWidth = Grid.Width / Grid.ColumnDefinitions.Count;
            for (int i = 0; i < Grid.ColumnDefinitions.Count; i++)
            {
                if (x < columnWidth * (i + 1))
                {
                    column = i;
                    break;
                }
            }

            // Compute the row
            var y = location.Y;
            var rowHeight = Grid.Height / Grid.RowDefinitions.Count;
            for (int i = 0; i < Grid.RowDefinitions.Count; i++)
            {
                if (y < rowHeight * (i + 1))
                {
                    row = i;
                    break;
                }
            }

            return (column, row);
        }

        public void RefreshGridLines()
        {
            InitializeGridLines();
        }

        public void OnHoverMove(Point location)
        {
            if(!Cursor.IsMousePressed())
            {
                // If mouse is not pressed, we don't want to show the highlighter
                return;
            }

            // Get current mouse point
            var (column, row) = ComputerColumnAndRowForPoint(location);

            // Set the highlighter position and size
            Grid.SetColumn(highlighter as IView, column);
            Grid.SetRow(highlighter as IView, row);
            highlighter.IsVisible = true;
        }

        public void OnHoverExit()
        {
            // Remove the highlighter from the grid
            highlighter.IsVisible = false;
        }
    }
}
