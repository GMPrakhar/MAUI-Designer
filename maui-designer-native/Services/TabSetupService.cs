using MAUIDesigner.HelperViews;

namespace MAUIDesigner.Services
{
    public interface ITabSetupService
    {
        void SetupTabs(
            TabMenuHolder leftTabMenuHolder,
            TabMenuHolder rightTabMenuHolder, 
            TabMenuHolder bottomTabMenuHolder,
            AbsoluteLayout designerFrame,
            Action generateXamlAction,
            Action loadXamlAction);
        
        ToolboxTab ToolboxTab { get; }
        PropertiesTab PropertiesTab { get; }
        XamlEditorTab XamlEditorTab { get; }
        HierarchyTab HierarchyTab { get; }
    }

    public class TabSetupService : ITabSetupService
    {
        public ToolboxTab ToolboxTab { get; private set; }
        public PropertiesTab PropertiesTab { get; private set; }
        public XamlEditorTab XamlEditorTab { get; private set; }
        public HierarchyTab HierarchyTab { get; private set; }

        public void SetupTabs(
            TabMenuHolder leftTabMenuHolder,
            TabMenuHolder rightTabMenuHolder,
            TabMenuHolder bottomTabMenuHolder,
            AbsoluteLayout designerFrame,
            Action generateXamlAction,
            Action loadXamlAction)
        {
            // Instantiate tabs
            ToolboxTab = new ToolboxTab();
            PropertiesTab = new PropertiesTab();
            HierarchyTab = new HierarchyTab();
            XamlEditorTab = new XamlEditorTab(generateXamlAction, loadXamlAction);

            // Add tabs to holders
            bottomTabMenuHolder.AddTab(XamlEditorTab);
            leftTabMenuHolder.AddTab(ToolboxTab);
            rightTabMenuHolder.AddTab(PropertiesTab);
            rightTabMenuHolder.AddTab(HierarchyTab);

            // Setup hierarchy tab
            HierarchyTab.SetDesignerFrame(designerFrame);
            designerFrame.ChildAdded += (s, e) => HierarchyTab.UpdateHierarchy();
            designerFrame.ChildRemoved += (s, e) => HierarchyTab.UpdateHierarchy();
        }
    }
}