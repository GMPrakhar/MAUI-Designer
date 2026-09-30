using System;
using System.ComponentModel;
using System.Drawing.Design;

using MauiDesigner.Core.Protocol;

namespace MauiDesigner.Vsix
{
    public sealed class VisualStudioDesignerSelectionProxy : DesignerSelectionProxy
    {
        public VisualStudioDesignerSelectionProxy(
            DesignerSelectionSnapshot snapshot,
            Action<string, string?> propertyChanged)
            : base(snapshot, propertyChanged, typeof(GridDefinitionsEditor))
        {
        }

        [Category("Layout")]
        [DisplayName(nameof(RowDefinitions))]
        [Editor(typeof(GridDefinitionsEditor), typeof(UITypeEditor))]
        public override DesignerGridDefinitionValue RowDefinitions
        {
            get => base.RowDefinitions;
            set => base.RowDefinitions = value;
        }

        [Category("Layout")]
        [DisplayName(nameof(ColumnDefinitions))]
        [Editor(typeof(GridDefinitionsEditor), typeof(UITypeEditor))]
        public override DesignerGridDefinitionValue ColumnDefinitions
        {
            get => base.ColumnDefinitions;
            set => base.ColumnDefinitions = value;
        }
    }
}
