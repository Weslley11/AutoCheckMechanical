using System.Collections.Generic;
using System.Windows;
using AutoCheckMechanical.Services;
using AutoCheckMechanical.ViewModels;

namespace AutoCheckMechanical
{
    public partial class DocumentSearchResultsWindow : Window
    {
        public DocumentSearchResultsWindow(string ecm, List<DocumentoEncontrado> resultados)
        {
            InitializeComponent();

            DataContext = new DocumentSearchResultsViewModel(ecm, resultados);
        }
    }
}
