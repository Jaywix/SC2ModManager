using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class ModBase : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Path { get; set; }

        private bool isEnabled;
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled != value)
                {
                    isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }




        public ModBase(string name, string path, bool isEnabled)
        {
            this.Name = name;
            this.Path = path;
            this.IsEnabled = isEnabled;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
