using System.Collections.ObjectModel;

namespace MAUIDesigner.HelperViews;

public partial class ContextMenu : ContentView
{
	public ObservableCollection<PropertyViewer> ActionList { get; set; } = new();
    public ContextMenu()
	{
		InitializeComponent();
	}

    internal void UpdateCollectionView()
    {
        PropertySource.ItemsSource = ActionList;
    }

    public void Close()
    {
        this.IsVisible = false;
    }
}