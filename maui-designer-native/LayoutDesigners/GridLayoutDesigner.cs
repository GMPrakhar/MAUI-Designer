using MAUIDesigner.HelperViews;
using Microsoft.Maui.Controls.Shapes;

namespace MAUIDesigner.LayoutDesigners
{
    class GridLayoutDesigner : ILayoutDesigner
    {
        public Grid Grid { get; }

        public Rectangle highlighter = new()
        {
            BackgroundColor = Color.FromArgb("#338B7CFF"),
            Stroke = Color.FromArgb("#8B7CFF"),
            StrokeThickness = 1,
            InputTransparent = true
        };

        public GridLayoutDesigner(Grid grid)
        {
            this.Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            highlighter.IsVisible = false;
            if (!Grid.Children.Contains(highlighter))
            {
                Grid.Add(highlighter);
            }
        }

        public void OnDrop(View view, Point location)
        {
            if (view == null)
            {
                return;
            }

            var designer = view as ElementDesignerView ?? view.Parent as ElementDesignerView;
            if (designer == null)
            {
                return;
            }

            (designer.Parent as Layout)?.Remove(designer);
            Grid.Add(designer);
            UpdateColumnAndRowForView(designer, location);
        }

        private void UpdateColumnAndRowForView(ElementDesignerView elementDesigner, Point location)
        {
            var (column, row) = ComputeColumnAndRowForPoint(location);
            Grid.SetColumn(elementDesigner, column);
            Grid.SetRow(elementDesigner, row);
            elementDesigner.EncapsulatingViewProperty.Margin = 0;
        }

        private (int, int) ComputeColumnAndRowForPoint(Point location)
        {
            int columnCount = Math.Max(1, Grid.ColumnDefinitions.Count == 0 ? 1 : Grid.ColumnDefinitions.Count);
            int rowCount = Math.Max(1, Grid.RowDefinitions.Count == 0 ? 1 : Grid.RowDefinitions.Count);
            var width = Grid.Width > 0 ? Grid.Width : 1;
            var height = Grid.Height > 0 ? Grid.Height : 1;

            var column = (int)Math.Clamp(Math.Floor(location.X / (width / columnCount)), 0, columnCount - 1);
            var row = (int)Math.Clamp(Math.Floor(location.Y / (height / rowCount)), 0, rowCount - 1);
            return (column, row);
        }

        public void OnHoverMove(Point location)
        {
            if (!Cursor.IsMousePressed())
            {
                return;
            }

            var (column, row) = ComputeColumnAndRowForPoint(location);
            Grid.SetColumn(highlighter, column);
            Grid.SetRow(highlighter, row);
            highlighter.IsVisible = true;
        }

        public void OnHoverExit()
        {
            highlighter.IsVisible = false;
        }
    }
}
