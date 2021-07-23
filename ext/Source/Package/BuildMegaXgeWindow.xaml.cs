// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Microsoft.VisualXpress
{
    public partial class BuildMegaXgeWindow : Window
    {
        public BuildMegaXgeWindow()
        {
            InitializeComponent();
        }
    }

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

    public class DelegateCommand : ICommand
    {
        readonly Action<object> _Action;
        readonly Predicate<object> _Predicate;

        public virtual event EventHandler CanExecuteChanged;

        public DelegateCommand(Action<Object> action, Predicate<Object> predicate = null)
        {
            _Action = action;
            _Predicate = predicate;
        }

        public bool CanExecute(object parameter)
        {
            return _Predicate == null ? true : _Predicate(parameter);
        }

        public void Execute(object parameter)
        {
            _Action(parameter);
        }

        public void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class RelayCommand : DelegateCommand
    {
        public RelayCommand(Action<Object> action)
            : base(action, null)
        {

        }

        public override event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
            }
        }
    }

    public static class WpfCommands
    {
        public static readonly ICommand CloseCommand =
            new RelayCommand(x => ((Window)x).Close());
    }

    public static class SelectedItemTemplateBehavior
    {
        public static readonly DependencyProperty SelectedItemDataTemplateProperty =
            DependencyProperty.RegisterAttached("SelectedItemDataTemplate", typeof(DataTemplate),
                typeof(SelectedItemTemplateBehavior), new PropertyMetadata(default(DataTemplate), PropertyChangedCallback));

        public static void SetSelectedItemDataTemplate(this UIElement element, DataTemplate value)
        {
            element.SetValue(SelectedItemDataTemplateProperty, value);
        }

        public static DataTemplate GetSelectedItemDataTemplate(this ComboBox element)
        {
            return (DataTemplate)element.GetValue(SelectedItemDataTemplateProperty);
        }

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var Element = d as ComboBox;

            if (e.Property == SelectedItemDataTemplateProperty && Element != null)
            {
                Element.Loaded -= ElementLoaded;
                UpdateSelectionTemplate(Element);
                Element.Loaded += ElementLoaded;

            }
        }

        static void ElementLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectionTemplate((ComboBox)sender);
        }

        private static void UpdateSelectionTemplate(ComboBox element)
        {
            var ContentPresenter = GetChildOfType<ContentPresenter>(element);

            if (ContentPresenter == null)
            {
                return;
            }

            var Template = element.GetSelectedItemDataTemplate();
            ContentPresenter.ContentTemplate = Template;
        }

        private static T GetChildOfType<T>(DependencyObject d)
            where T : DependencyObject
        {
            if (d == null)
            {
                return null;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            {
                var Child = VisualTreeHelper.GetChild(d, i);
                var Result = (Child as T) ?? GetChildOfType<T>(Child);

                if (Result != null)
                {
                    return Result;
                }
            }

            return null;
        }
    }

    public interface IMultiSelectable
    {
        bool IsMultiSelect { get; }
    }

    public class MultiSelectTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(Object item, DependencyObject container)
        {
            var Item = item as IMultiSelectable;

            if (Item?.IsMultiSelect == true)
            {
                return MultiSelectTemplate;
            }

            return DefaultTemplate;
        }

        public DataTemplate DefaultTemplate { get; set; }
        public DataTemplate MultiSelectTemplate { get; set; }
    }
}

