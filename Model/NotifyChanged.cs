using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    class NotifyChanged : INotifyPropertyChanged
    {
        private bool _isMinimized;
        private bool _isAutoTranslate;
        private bool _isLoadKanji;
        private bool _isOpenedHistories;
        private bool _isOpenedSettings;
        private bool _isFaded;
        private bool _isJaVi;
        private bool _isRunOnStartUp;
        private bool _isDoubleClickOn;

        public bool IsMinimized
        {
            get => _isMinimized;
            set
            {
                _isMinimized = value;
                NotifyPropertyChanged("IsMinimized");
            }
        }

        public bool IsAutoTranslate
        {
            get => _isAutoTranslate;
            set
            {
                _isAutoTranslate = value;
                NotifyPropertyChanged("IsAutoTranslate");
            }
        }

        public bool IsLoadKanji
        {
            get => _isLoadKanji;
            set
            {
                _isLoadKanji = value;
                NotifyPropertyChanged("IsLoadKanji");
            }
        }

        public bool IsOpenedHistories
        {
            get => _isOpenedHistories;
            set
            {
                _isOpenedHistories = value;
                NotifyPropertyChanged("IsOpenedHistories");
            }
        }

        public bool IsOpenedSettings
        {
            get => _isOpenedSettings;
            set
            {
                _isOpenedSettings = value;
                NotifyPropertyChanged("IsOpenedSettings");
            }
        }

        public bool IsFaded
        {
            get => _isFaded;
            set
            {
                _isFaded = value;
                NotifyPropertyChanged("IsFaded");
            }
        }

        public bool IsJaVi
        {
            get => _isJaVi;
            set
            {
                _isJaVi = value;
                NotifyPropertyChanged("IsJaVi");
            }
        }
        public bool IsRunOnStartUp
        {
            get => _isRunOnStartUp;
            set
            {
                _isRunOnStartUp = value;
                NotifyPropertyChanged("IsRunOnStartUp");
            }
        }

        public bool IsDoubleClickOn
        {
            get => _isDoubleClickOn;
            set
            {
                _isDoubleClickOn = value;
                NotifyPropertyChanged("IsDoubleClickOn");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
