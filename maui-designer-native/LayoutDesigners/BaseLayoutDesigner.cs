using MAUIDesigner.HelperViews;

namespace MAUIDesigner.LayoutDesigners
{
    class BaseLayoutDesigner : ILayoutDesigner
    {
        private readonly Layout _layout;

        public BaseLayoutDesigner(Layout layout)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public void OnDrop(View draggingView, Point location)
        {
            if (draggingView == null)
            {
                return;
            }

            var designer = draggingView as ElementDesignerView ?? draggingView.Parent as ElementDesignerView;
            if (designer == null)
            {
                return;
            }

            if (!ReferenceEquals(designer.Parent, _layout))
            {
                (designer.Parent as Layout)?.Remove(designer);
                _layout.Children.Add(designer);
            }
        }

        public void OnHoverMove(Point location) { }

        public void OnHoverExit() { }
    }
}
