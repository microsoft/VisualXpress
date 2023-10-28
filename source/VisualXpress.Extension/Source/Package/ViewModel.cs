// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace Microsoft.VisualXpress
{
    public abstract class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        protected void RaisePropertyChanged([CallerMemberName] String property = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
    }

    public abstract class ViewModelWithOwner<T> : ViewModel
        where T : ViewModel
    {
        public ViewModelWithOwner(T owner)
        {
            _Owner = owner;
        }

        private readonly T _Owner;
        public T Owner { get { return _Owner; } }
    }
}