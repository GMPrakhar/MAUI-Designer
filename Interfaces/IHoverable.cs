using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.NewFolder
{
    public interface IHoverable
    {
        public void OnHoverMove(Point location);
        public void OnHoverExit();
    }
}
