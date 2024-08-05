using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner
{
    public class XamlViewModel : INotifyPropertyChanged
    {
        private string _generatedXaml;
        public string GeneratedXaml
        {
            get { return _generatedXaml; }
            set
            {
                if (_generatedXaml != value)
                {
                    _generatedXaml = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GeneratedXaml)));
                }
            }
        }

        public XamlViewModel(string xaml)
        {
            _generatedXaml = xaml;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
