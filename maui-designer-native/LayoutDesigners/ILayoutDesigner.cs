using MAUIDesigner.Interfaces;

namespace MAUIDesigner.LayoutDesigners
{
    public interface ILayoutDesigner : IHoverable
    {
        void OnDrop(View view, Point location);
    }
}
