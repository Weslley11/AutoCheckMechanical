using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AutoCheckMechanical
{
    public partial class ChecksConfigWindow : Window
    {
        private readonly List<CheckBox> _checkBoxes = new List<CheckBox>();

        public HashSet<string> CheckersDesativados { get; private set; }

        public ChecksConfigWindow(IEnumerable<string> todosOsChecks, ISet<string> desativadosAtuais, bool temaEscuro)
        {
            InitializeComponent();

            AplicarTema(temaEscuro);

            foreach (string nome in todosOsChecks)
            {
                CheckBox checkBox = new CheckBox
                {
                    Content = nome,
                    IsChecked = !desativadosAtuais.Contains(nome),
                    Foreground = this.Foreground,
                    Margin = new Thickness(0, 0, 0, 12),
                    FontSize = 13,
                    Tag = nome
                };

                _checkBoxes.Add(checkBox);
                panelCheckers.Children.Add(checkBox);
            }
        }

        private void AplicarTema(bool escuro)
        {
            if (escuro)
            {
                gridRoot.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                this.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                this.Foreground = new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3));
            }
            else
            {
                gridRoot.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                this.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                this.Foreground = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
            }

            txtTitulo.Foreground = this.Foreground;
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            CheckersDesativados = new HashSet<string>();

            foreach (CheckBox checkBox in _checkBoxes)
            {
                if (checkBox.IsChecked != true)
                    CheckersDesativados.Add((string)checkBox.Tag);
            }

            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
