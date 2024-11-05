using CommunityToolkit.Maui.Views;
using MAUIDesigner.HelperViews;

namespace MAUIDesigner.LayoutDesigners
{
    class GridLayoutDesigner : ILayoutDesigner
    {
        public GridLayoutDesigner(Grid grid)
        {
            this.Grid = grid;
        }

        public Grid Grid { get; }

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
            ComputeColumnAndRow(view.Parent as ElementDesignerView, location);
        }

        private void ComputeColumnAndRow(ElementDesignerView elementDesigner, Point location)
        {
            // Compute the column and row
            if (elementDesigner == null) {
                return;
            }

            var column = 0;
            var row = 0;

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

            // Update the column and row
            Grid.SetColumn(elementDesigner as IView, column);
            Grid.SetRow(elementDesigner as IView, row);

            // Update the margin
            elementDesigner.EncapsulatingViewProperty.Margin = 0;
        }
    }
}
