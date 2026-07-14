using System;
using System.Windows;
using WDC.Views;

namespace WDC
{
    /// <summary>
    /// Interação lógica para App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Sem StartupUri no App.xaml -- constrói a janela principal aqui,
        // igual o padrão real do WBC/WFV (App.xaml.cs deles faz a mesma
        // coisa: OnStartup + try/catch + new Views.MainWindow().ShowDialog()
        // + mensagem de erro fatal + Shutdown()).
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                new MainWindow().ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro fatal: " + ex.Message, "WAU Design Check",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Current.Shutdown();
            }
        }
    }
}
