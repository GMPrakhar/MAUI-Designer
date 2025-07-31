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

            // Subscribe to collection changes to refresh grid lines
            OwningGrid.ColumnDefinitions.CollectionChanged += (s, e) => RefreshGridLines();
            OwningGrid.RowDefinitions.CollectionChanged += (s, e) => RefreshGridLines();

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

            // Handle moving within grid or from outside to grid
            var elementDesigner = view.Parent as ElementDesignerView;
            if (elementDesigner == null)
            {
                return;
            }

            // If the element is not currently in this grid, move it here
            if (elementDesigner.Parent != OwningGrid)
            {
                // Remove from current parent
                if (elementDesigner.Parent is Layout currentParent)
                {
                    currentParent.Remove(elementDesigner);
                }
                
                // Add to this grid
                OwningGrid.Add(elementDesigner);
            }

            // Compute and update the column and row
            UpdateColumnAndRowForView(elementDesigner, location);
            
            // Refresh grid lines to ensure they're visible
            RefreshGridLines();
        }

        private bool IsOutsideGrid(Point location)
        {
            // Get the actual bounds of the grid
            var gridBounds = OwningGrid.Frame;
            
            // Check if the drop location is outside the grid boundaries
            // Use a small tolerance to make it easier to drop near edges
            const double tolerance = 5.0;
            
            return location.X < -tolerance || 
                   location.Y < -tolerance || 
                   location.X > gridBounds.Width + tolerance || 
                   location.Y > gridBounds.Height + tolerance;
        }

        private void HandleDropOutsideGrid(View view)
        {
            var elementDesigner = view.Parent as ElementDesignerView;
            if (elementDesigner == null)
            {
                return;
            }

            // Remove from grid if it's currently in this grid
            if (elementDesigner.Parent == OwningGrid)
            {
                OwningGrid.Remove(elementDesigner);
                
                // Add to base layout if available
                if (DragAndDropOperations.BaseLayout != null)
                {
                    DragAndDropOperations.BaseLayout.Add(elementDesigner);
                    
                    // Reset grid properties
                    OwningGrid.SetColumn(elementDesigner, 0);
                    OwningGrid.SetRow(elementDesigner, 0);
                    OwningGrid.SetColumnSpan(elementDesigner, 1);
                    OwningGrid.SetRowSpan(elementDesigner, 1);
                    
                    // Reset margin that might have been set for grid positioning
                    elementDesigner.EncapsulatingViewProperty.Margin = 0;
                }
                
                // Refresh grid lines after element removal
                RefreshGridLines();
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

            // Only add lines if we have more than one column/row
            if (OwningGrid.ColumnDefinitions.Count <= 1 && OwningGrid.RowDefinitions.Count <= 1)
                return;

            // Add column lines using small Border elements with dotted appearance
            for (int i = 1; i < OwningGrid.ColumnDefinitions.Count; i++)
            {
                var line = new Border
                {
                    WidthRequest = 1,
                    BackgroundColor = Colors.Gray,
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Start,
                    ZIndex = 500,
                    Opacity = 0.5,
                    Margin = new Thickness(-0.5, 0, 0, 0) // Center the line on the boundary
                };
                
                OwningGrid.SetColumn(line, i);
                OwningGrid.SetRowSpan(line, Math.Max(1, OwningGrid.RowDefinitions.Count));
                OwningGrid.Add(line);
                columnLines.Add(line);

                // Add draggable divider for column resizing (invisible but larger hit area)
                var divider = new Rectangle
                {
                    WidthRequest = 6,
                    Fill = Colors.Transparent,
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Start,
                    ZIndex = 600,
                    Margin = new Thickness(-3, 0, 0, 0) // Center the hit area on the line
                };

                var dragGesture = new DragGestureRecognizer();
                int columnIndex = i;
                dragGesture.DragStarting += (s, e) => OnColumnDragStarting(s, e, columnIndex);
                divider.GestureRecognizers.Add(dragGesture);

                OwningGrid.SetColumn(divider, i);
                OwningGrid.SetRowSpan(divider, Math.Max(1, OwningGrid.RowDefinitions.Count));
                OwningGrid.Add(divider);
                columnDividers.Add(divider);
            }

            // Add row lines using small Border elements with dotted appearance
            for (int i = 1; i < OwningGrid.RowDefinitions.Count; i++)
            {
                var line = new Border
                {
                    HeightRequest = 1,
                    BackgroundColor = Colors.Gray,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.Fill,
                    ZIndex = 500,
                    Opacity = 0.5,
                    Margin = new Thickness(0, -0.5, 0, 0) // Center the line on the boundary
                };

                OwningGrid.SetRow(line, i);
                OwningGrid.SetColumnSpan(line, Math.Max(1, OwningGrid.ColumnDefinitions.Count));
                OwningGrid.Add(line);
                rowLines.Add(line);

                // Add draggable divider for row resizing (invisible but larger hit area)
                var divider = new Rectangle
                {
                    HeightRequest = 6,
                    Fill = Colors.Transparent,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.Fill,
                    ZIndex = 600,
                    Margin = new Thickness(0, -3, 0, 0) // Center the hit area on the line
                };

                var dragGesture = new DragGestureRecognizer();
                int rowIndex = i;
                dragGesture.DragStarting += (s, e) => OnRowDragStarting(s, e, rowIndex);
                divider.GestureRecognizers.Add(dragGesture);

                OwningGrid.SetRow(divider, i);
                OwningGrid.SetColumnSpan(divider, Math.Max(1, OwningGrid.ColumnDefinitions.Count));
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
                
                // Refresh grid lines after resize
                RefreshGridLines();
            }
        }

        public void ResizeRow(int rowIndex, double newHeight)
        {
            if (rowIndex > 0 && rowIndex < OwningGrid.RowDefinitions.Count)
            {
                var rowDef = OwningGrid.RowDefinitions[rowIndex - 1];
                rowDef.Height = new GridLength(Math.Max(20, newHeight));
                
                // Refresh grid lines after resize
                RefreshGridLines();
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

            // Ensure we have at least one column and row
            if (OwningGrid.ColumnDefinitions.Count == 0)
            {
                OwningGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }
            if (OwningGrid.RowDefinitions.Count == 0)
            {
                OwningGrid.RowDefinitions.Add(new RowDefinition());
            }

            // Compute the column
            var x = Math.Max(0, location.X); // Ensure x is not negative
            var totalWidth = Math.Max(1, OwningGrid.Width); // Avoid division by zero
            var columnWidth = totalWidth / OwningGrid.ColumnDefinitions.Count;
            
            for (int i = 0; i < OwningGrid.ColumnDefinitions.Count; i++)
            {
                if (x < columnWidth * (i + 1))
                {
                    column = i;
                    break;
                }
            }
            
            // Ensure column is within bounds
            column = Math.Min(column, OwningGrid.ColumnDefinitions.Count - 1);

            // Compute the row
            var y = Math.Max(0, location.Y); // Ensure y is not negative
            var totalHeight = Math.Max(1, OwningGrid.Height); // Avoid division by zero
            var rowHeight = totalHeight / OwningGrid.RowDefinitions.Count;
            
            for (int i = 0; i < OwningGrid.RowDefinitions.Count; i++)
            {
                if (y < rowHeight * (i + 1))
                {
                    row = i;
                    break;
                }
            }
            
            // Ensure row is within bounds
            row = Math.Min(row, OwningGrid.RowDefinitions.Count - 1);

            return (column, row);
        }

        public void RefreshGridLines()
        {
            InitializeGridLines();
        }

        // Public method to manually add columns/rows and refresh grid lines
        public void AddColumn()
        {
            OwningGrid.ColumnDefinitions.Add(new ColumnDefinition());
            RefreshGridLines();
        }

        public void AddRow()
        {
            OwningGrid.RowDefinitions.Add(new RowDefinition());
            RefreshGridLines();
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
