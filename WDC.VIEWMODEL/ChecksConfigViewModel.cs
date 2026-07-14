using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace WDC.VIEWMODEL
{
    public class ChecksConfigViewModel : BaseViewModel
    {
        public ObservableCollection<CheckerOptionViewModel> CheckerOptions { get; }

        public HashSet<string> CheckersDesativados { get; private set; }

        public ICommand SaveCommand { get; }

        public ICommand CancelCommand { get; }

        public ChecksConfigViewModel(IEnumerable<string> todosOsChecks, ISet<string> desativadosAtuais, Action<bool> setDialogResultAction)
        {
            CheckerOptions = new ObservableCollection<CheckerOptionViewModel>(
                todosOsChecks.Select(nome => new CheckerOptionViewModel(nome, !desativadosAtuais.Contains(nome))));

            SaveCommand = new DelegateCommand(_ =>
            {
                CheckersDesativados = new HashSet<string>(
                    CheckerOptions.Where(o => !o.IsEnabled).Select(o => o.Name));

                setDialogResultAction(true);
            });

            CancelCommand = new DelegateCommand(_ => setDialogResultAction(false));
        }
    }
}
