using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Lab_rab_2_KutlubaevAD_БПИ_23_02.ViewModel
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _asyncExecute;
        private readonly Action _syncExecute;
        private readonly Func<bool> _canExecute;

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                _isRunning = value;
                NotifyCanExecuteChanged();
            }
        }

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _asyncExecute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public AsyncRelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _syncExecute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) =>
            !_isRunning && (_canExecute == null || _canExecute());

        public async void Execute(object parameter)
        {
            if (_syncExecute != null)
            {
                _syncExecute();
                return;
            }

            IsRunning = true;
            try
            {
                await _asyncExecute();
            }
            finally
            {
                IsRunning = false;
            }
        }

        public event EventHandler CanExecuteChanged;

        public void NotifyCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
