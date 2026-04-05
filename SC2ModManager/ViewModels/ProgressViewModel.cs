using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.ViewModels
{
    public class ProgressViewModel : INotifyPropertyChanged
    {
        private int progress;
        public int Progress
        {
            get => progress;
            set
            {
                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        private string status;
        public string Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
