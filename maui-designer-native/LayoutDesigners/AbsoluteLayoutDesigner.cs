using MAUIDesigner.HelperViews;

namespace MAUIDesigner.LayoutDesigners
{
    class AbsoluteLayoutDesigner : ILayoutDesigner
    {
        private readonly AbsoluteLayout? _absoluteLayout;

        public AbsoluteLayoutDesigner(AbsoluteLayout? absoluteLayout)
        {
            _absoluteLayout = absoluteLayout;
        }

        public void OnDrop(View draggingView, Point location)
        {
            if (draggingView == null || _absoluteLayout == null)
            {
                return;
            }

            var designer = draggingView as ElementDesignerView ?? draggingView.Parent as ElementDesignerView;
            if (designer == null)
            {
                return;
            }

            if (!ReferenceEquals(designer.Parent, _absoluteLayout))
            {
                (designer.Parent as Layout)?.Remove(designer);
                _absoluteLayout.Add(designer);
            }

            designer.EncapsulatingViewProperty.Margin = new Thickness(Math.Max(0, location.X), Math.Max(0, location.Y), 0, 0);
        }

        public void OnHoverExit() { }

        public void OnHoverMove(Point location) { }
    }
}
