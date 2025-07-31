using CommunityToolkit.Maui.Views;
using MAUIDesigner.HelperViews;
using MAUIDesigner.Interfaces;
using Windows.Devices.Input;
using MAUIDesigner.DnDHelper;
using Microsoft.Maui.Controls.Shapes;

namespace MAUIDesigner.LayoutDesigners
{
    class GridLayoutDesigner: ILayoutDesigner
    {
        public Grid OwningGrid { get; }

        public Rectangle highlighter = new()
        {
            WidthRequest = -1,
            HeightRequest = -1,
            BackgroundColor = Colors.AliceBlue,
        };

        private List<Border> columnLines = new();
        private List<Border> rowLines = new();
        private List<Rectangle> columnDividers = new();
        private List<Rectangle> rowDividers = new();

        public GridLayoutDesigner(Grid grid)
        {
            this.OwningGrid = grid ?? throw new ArgumentNullException(nameof(grid));

            // Set lighter background for grid
            if (OwningGrid.BackgroundColor == null || OwningGrid.BackgroundColor == Colors.Transparent)
            {
                OwningGrid.BackgroundColor = Colors.LightGray.WithAlpha(0.1f);
            }

            // Initialize the highlighter
            highlighter.IsVisible = false;
            OwningGrid.Add(highlighter as IView);

            // Initialize grid lines
            InitializeGridLines();
        }

        // Assume ElementDesigner View is getting dropped on the OwningGrid, now we need to compute the column and row it belongs to and update the relevant property
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
            OwningGrid.Add(view.Parent as View);

            // Compute the column and row
            UpdateColumnAndRowForView(view.Parent as ElementDesignerView, location);
        }

        private bool IsOutsideGrid(Point location)
        {
            return location.X < 0 || location.Y < 0 || 
                   location.X > OwningGrid.Width || location.Y > OwningGrid.Height;
        }

        private void HandleDropOutsideGrid(View view)
        {
            // Remove from grid and add to base layout
            if (view.Parent?.Parent == OwningGrid)
            {
                OwningGrid.Remove(view.Parent as View);
                
                // Add to base layout if available
                if (DragAndDropOperations.BaseLayout != null)
                {
                    DragAndDropOperations.BaseLayout.Add(view.Parent as View);
                    
                    // Reset grid properties
                    if (view.Parent is ElementDesignerView elementDesigner)
                    {
                        OwningGrid.SetColumn(elementDesigner, 0);
                        OwningGrid.SetRow(elementDesigner, 0);
                        OwningGrid.SetColumnSpan(elementDesigner, 1);
                        OwningGrid.SetRowSpan(elementDesigner, 1);
                    }
                }
            }
        }

        private void InitializeGridLines()
        {
            // Clear existing lines
            foreach (var line in columnLines)
            {
                OwningGrid.Remove(line);
            }
            foreach (var line in rowLines)
            {
                OwningGrid.Remove(line);
            }
            foreach (var divider in columnDividers)
            {
                OwningGrid.Remove(divider);
            }
            foreach (var divider in rowDividers)
            {
                OwningGrid.Remove(divider);
            }

            columnLines.Clear();
            rowLines.Clear();
            columnDividers.Clear();
            rowDividers.Clear();

            // Add column lines using Border elements for better visibility
            for (int i = 1; i < OwningGrid.ColumnDefinitions.Count; i++)
            {
                var line = new Border
                {
                    WidthRequest = 1,
                    BackgroundColor = Colors.Gray,
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Start,
                    ZIndex = 500,
                    Opacity = 0.7
                };
                
                OwningGrid.SetColumn(line, i);
                OwningGrid.SetRowSpan(line, OwningGrid.RowDefinitions.Count);
                OwningGrid.Add(line);
                columnLines.Add(line);

                // Add draggable divider for column resizing
                var divider = new Rectangle
                {
                    WidthRequest = 4,
                    Fill = Colors.Transparent,
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Start,
                    ZIndex = 600,
                    Margin = new Thickness(-2, 0, 0, 0)
                };

                var dragGesture = new DragGestureRecognizer();
                int columnIndex = i;
                dragGesture.DragStarting += (s, e) => OnColumnDragStarting(s, e, columnIndex);
                divider.GestureRecognizers.Add(dragGesture);

                OwningGrid.SetColumn(divider, i);
                OwningGrid.SetRowSpan(divider, OwningGrid.RowDefinitions.Count);
                OwningGrid.Add(divider);
                columnDividers.Add(divider);
            }

            // Add row lines using Border elements for better visibility
            for (int i = 1; i < OwningGrid.RowDefinitions.Count; i++)
            {
                var line = new Border
                {
                    HeightRequest = 1,
                    BackgroundColor = Colors.Gray,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.Fill,
                    ZIndex = 500,
                    Opacity = 0.7
                };

                OwningGrid.SetRow(line, i);
                OwningGrid.SetColumnSpan(line, OwningGrid.ColumnDefinitions.Count);
                OwningGrid.Add(line);
                rowLines.Add(line);

                // Add draggable divider for row resizing
                var divider = new Rectangle
                {
                    HeightRequest = 4,
                    Fill = Colors.Transparent,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.Fill,
                    ZIndex = 600,
                    Margin = new Thickness(0, -2, 0, 0)
                };

                var dragGesture = new DragGestureRecognizer();
                int rowIndex = i;
                dragGesture.DragStarting += (s, e) => OnRowDragStarting(s, e, rowIndex);
                divider.GestureRecognizers.Add(dragGesture);

                OwningGrid.SetRow(divider, i);
                OwningGrid.SetColumnSpan(divider, OwningGrid.ColumnDefinitions.Count);
                OwningGrid.Add(divider);
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
            if (columnIndex > 0 && columnIndex < OwningGrid.ColumnDefinitions.Count)
            {
                var columnDef = OwningGrid.ColumnDefinitions[columnIndex - 1];
                columnDef.Width = new GridLength(Math.Max(20, newWidth));
                
                // Refresh grid lines
                InitializeGridLines();
            }
        }

        public void ResizeRow(int rowIndex, double newHeight)
        {
            if (rowIndex > 0 && rowIndex < OwningGrid.RowDefinitions.Count)
            {
                var rowDef = OwningGrid.RowDefinitions[rowIndex - 1];
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
            OwningGrid.SetColumn(elementDesigner as IView, column);
            OwningGrid.SetRow(elementDesigner as IView, row);

            // Update the margin
            elementDesigner.EncapsulatingViewProperty.Margin = 0;
        }

        private (int, int) ComputerColumnAndRowForPoint(Point location)
        {
            int column = 0, row = 0;

            // Compute the column
            var x = location.X;
            var columnWidth = OwningGrid.Width / OwningGrid.ColumnDefinitions.Count;
            for (int i = 0; i < OwningGrid.ColumnDefinitions.Count; i++)
            {
                if (x < columnWidth * (i + 1))
                {
                    column = i;
                    break;
                }
            }

            // Compute the row
            var y = location.Y;
            var rowHeight = OwningGrid.Height / OwningGrid.RowDefinitions.Count;
            for (int i = 0; i < OwningGrid.RowDefinitions.Count; i++)
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
            OwningGrid.SetColumn(highlighter as IView, column);
            OwningGrid.SetRow(highlighter as IView, row);
            highlighter.IsVisible = true;
        }

        public void OnHoverExit()
        {
            // Remove the highlighter from the grid
            highlighter.IsVisible = false;
        }
    }
}
