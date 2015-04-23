using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace AudioAlign.ViewModels {
    /// <summary>
    /// A command implementation for MVVM.
    /// Source: http://social.technet.microsoft.com/wiki/contents/articles/18199.event-handling-in-an-mvvm-wpf-application.aspx
    /// </summary>
    public class DelegateCommand<T> : ICommand where T : class {

        private readonly Predicate<T> _canExecute;
        private readonly Action<T> _execute;

        public DelegateCommand(Action<T> execute)
            : this(execute, null) {
        }

        public DelegateCommand(Action<T> execute, Predicate<T> canExecute) {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) {
            if (_canExecute == null)
                return true;

            return _canExecute((T)parameter);
        }

        public void Execute(object parameter) {
            _execute((T)parameter);
        }

        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() {
            if (CanExecuteChanged != null)
                CanExecuteChanged(this, EventArgs.Empty);
        }
    }

    public class DelegateCommand : DelegateCommand<object> {
        public DelegateCommand(Action<object> execute)
            : base(execute, null) {
        }

        public DelegateCommand(Action<object> execute, Predicate<object> canExecute)
            : base(execute, canExecute) {
        }
    }
}
