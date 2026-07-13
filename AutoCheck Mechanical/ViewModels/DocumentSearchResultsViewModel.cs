using System.Collections.Generic;
using System.Collections.ObjectModel;
using AutoCheckMechanical.Services;

namespace AutoCheckMechanical.ViewModels
{
    // ViewModel simples só pra exibição -- a busca em si acontece no
    // MainViewModel (BuscarDocumentosPorEcmCommand), essa classe só recebe
    // a lista já pronta e mostra.
    public class DocumentSearchResultsViewModel : BaseViewModel
    {
        public ObservableCollection<DocumentoEncontrado> Resultados { get; } = new ObservableCollection<DocumentoEncontrado>();

        private string _statusText = "";
        public string StatusText
        {
            get { return _statusText; }
            set { _statusText = value; OnPropertyChanged(); }
        }

        public DocumentSearchResultsViewModel(string ecm, List<DocumentoEncontrado> resultados)
        {
            foreach (DocumentoEncontrado documento in resultados)
                Resultados.Add(documento);

            StatusText = resultados.Count == 0
                ? $"Nenhum documento encontrado para a ECM {ecm}."
                : $"{resultados.Count} documento(s) encontrado(s) para a ECM {ecm}.";
        }
    }
}
