using MAUIDesigner.DnDHelper;
using MAUIDesigner.HelperViews;
using System.Diagnostics;
using Extensions = Microsoft.Maui.Controls.Xaml.Extensions;

namespace MAUIDesigner.Services
{
    public interface IXamlService
    {
        void LoadViewFromXaml(string xaml, AbsoluteLayout designerFrame, HierarchyTab hierarchyTab);
        void AddDirectChildrenOfAbsoluteLayout(AbsoluteLayout sourceLayout, AbsoluteLayout targetLayout);
        void LoadLayoutRecursively(Layout newLayout, Layout? loadedLayout);
        void CopyProperties(Layout sourceLayout, Layout? targetLayout);
    }

    public class XamlService : IXamlService
    {
        public void LoadViewFromXaml(string xaml, AbsoluteLayout designerFrame, HierarchyTab hierarchyTab)
        {
            if (string.IsNullOrWhiteSpace(xaml))
            {
                throw new ArgumentException("XAML content cannot be empty.");
            }

            designerFrame.Children.Clear();
            var newLayout = new AbsoluteLayout();
            
            try
            {
                var xamlLoaded = Extensions.LoadFromXaml(newLayout, xaml);
                
                if (newLayout.Children.Count == 0)
                {
                    throw new InvalidOperationException("No valid XAML elements found in the provided content.");
                }
                
                var loadedLayout = newLayout.Children[0] as AbsoluteLayout;
                if (loadedLayout == null)
                {
                    throw new InvalidOperationException("Root element must be an AbsoluteLayout.");
                }
                
                var newAbsoluteLayout = new AbsoluteLayout();
                LoadLayoutRecursively(newAbsoluteLayout, loadedLayout);
                AddDirectChildrenOfAbsoluteLayout(newAbsoluteLayout, designerFrame);
                hierarchyTab.UpdateHierarchy();
            }
            catch (Microsoft.Maui.Controls.Xaml.XamlParseException xamlEx)
            {
                Debug.WriteLine($"XAML Parse Error: {xamlEx.Message}");
                throw new InvalidOperationException($"XAML parse error at line {xamlEx.XmlInfo?.LineNumber ?? 0}: {xamlEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading XAML: {ex.Message}");
                throw new InvalidOperationException($"Error loading XAML: {ex.Message}");
            }
        }

        public void AddDirectChildrenOfAbsoluteLayout(AbsoluteLayout sourceLayout, AbsoluteLayout targetLayout)
        {
            foreach (View loadedView in sourceLayout.Children)
            {
                targetLayout.Add(loadedView);
            }
        }

        public void LoadLayoutRecursively(Layout newLayout, Layout? loadedLayout)
        {
            if (loadedLayout == null) return;

            foreach (View loadedView in loadedLayout.Children)
            {
                ElementDesignerView elementDesignerView;
                if (loadedView is Layout internalLayout)
                {
                    var newLoadedLayout = Activator.CreateInstance(internalLayout.GetType()) as Layout;
                    CopyProperties(internalLayout, newLoadedLayout);
                    LoadLayoutRecursively(newLoadedLayout, internalLayout);
                    elementDesignerView = new ElementDesignerView(newLoadedLayout);
                }
                else
                {
                    elementDesignerView = new ElementDesignerView(loadedView);
                }
                newLayout.Add(elementDesignerView);
                ElementOperations.AddDesignerGestureControls(elementDesignerView);
            }
            DragAndDropOperations.OnFocusChanged?.Invoke(null);
        }

        public void CopyProperties(Layout sourceLayout, Layout? targetLayout)
        {
            if (targetLayout == null) return;
            
            var properties = sourceLayout.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.CanRead && property.CanWrite && property.Name != "Item")
                {
                    try
                    {
                        var value = property.GetValue(sourceLayout);
                        property.SetValue(targetLayout, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error copying property {property.Name}: {ex.Message}");
                    }
                }
            }
        }
    }
}