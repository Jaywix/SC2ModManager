using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.Models
{
    public class CompareResultItem : INotifyPropertyChanged
    {
        public string FileName { get; set; }
        public bool IsDifferent { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
