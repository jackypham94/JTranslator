using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private string _languagePath;
        private string _minimizePath;
        private string _opacityPath;
        private string _statusPath;
        private string _settingsPath;
        private string _kanjiPath;

        public MainViewModel()
        {
            LanguagePath = "";
            StatusPath = "";
            OpacityPath = "";
            MinimizePath = "";
            SettingsPath = "";
            KanjiPath = "";
        }

        public string LanguagePath
        {
            get => _languagePath;
            set
            {
                _languagePath = value;
                NotifyPropertyChanged("LanguagePath");
            }
        }


        public string StatusPath
        {
            get => _statusPath;
            set
            {
                _statusPath = value;
                NotifyPropertyChanged("StatusPath");
            }
        }

        public string OpacityPath
        {
            get => _opacityPath;
            set
            {
                _opacityPath = value;
                NotifyPropertyChanged("OpacityPath");
            }
        }

        public string MinimizePath
        {
            get => _minimizePath;
            set
            {
                _minimizePath = value;
                NotifyPropertyChanged("MinimizePath");
            }
        }

        public string SettingsPath
        {
            get => _settingsPath;
            set
            {
                _settingsPath = value;
                NotifyPropertyChanged("SettingsPath");
            }
        }

        public string KanjiPath
        {
            get => _kanjiPath;
            set
            {
                _kanjiPath = value;
                NotifyPropertyChanged("KanjiPath");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
