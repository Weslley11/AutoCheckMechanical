namespace AutoCheckMechanical.ViewModels
{
    public class CheckerOptionViewModel : BaseViewModel
    {
        public string Name { get; }

        private bool _isEnabled;

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public CheckerOptionViewModel(string name, bool isEnabled)
        {
            Name = name;
            _isEnabled = isEnabled;
        }
    }
}
