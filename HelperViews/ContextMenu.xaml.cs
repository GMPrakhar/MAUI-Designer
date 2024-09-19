using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;

namespace MAUIDesigner.HelperViews;

public partial class ContextMenu : ContentView
{
	public ObservableCollection<PropertyViewer> ActionList { get; set; } = new();
    public ContextMenu()
	{
		InitializeComponent();
        this.IsVisible = false;
	}

    internal void UpdateCollectionView()
    {
        PropertySource.ItemsSource = ActionList;
    }

    public void Close()
    {
        this.IsVisible = false;
    }

    public void Show()
    {
        this.IsVisible = true;
        this.Focus();
        this.ZIndex = int.MaxValue;
    }
}