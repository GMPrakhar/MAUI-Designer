using CommunityToolkit.Maui.Views;
using MAUIDesigner.HelperViews;
using MAUIDesigner.NewFolder;
using Microsoft.Maui.Controls.Shapes;
using Windows.Devices.Input;

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

        public GridLayoutDesigner(Grid grid)
        {
            this.Grid = grid ?? throw new ArgumentNullException(nameof(grid));

            // Initialize the highlighter
            highlighter.IsVisible = false;
            Grid.Add(highlighter as IView);
        }

        // Assume ElementDesigner View is getting dropped on the Grid, now we need to compute the column and row it belongs to and update the relevant property
        public void OnDrop(View view, Point location)
        {
            if (view == null)
            {
                return;
            }


            (view.Parent.Parent as Layout).Remove(view.Parent as View);
            Grid.Add(view.Parent as View);

            // Compute the column and row
            UpdateColumnAndRowForView(view.Parent as ElementDesignerView, location);
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
