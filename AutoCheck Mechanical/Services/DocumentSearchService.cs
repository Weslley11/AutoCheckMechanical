using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AutoCheckMechanical.Services.DocumentOutput;

namespace AutoCheckMechanical.Services
{
    public class DocumentoEncontrado
    {
        public string DocumentNumber { get; set; }
        public string Type { get; set; }
        public string Part { get; set; }
        public string Version { get; set; }
        public string Status { get; set; }
        public string ChangeNumber { get; set; }
        public string Descricao { get; set; }
        public List<string> CaminhosOriginais { get; } = new List<string>();
    }

    // Busca documentos do DMS vinculados a uma ECM, via o Web Service SOA da
    // WEG ITF_O_S_DOCUMENT_OUTPUT (código de registro 634-049) -- mesmo
    // estilo que o WBC usa pra buscar Material (MaterialService.cs), só que
    // aqui a busca é por ChangeNumber (ECM) em vez de MaterialNumber.
    //
    // Diferença importante em relação ao WBC: o MaterialService usa
    // Wau.Util.Services.SapServices.GetServiceCredential() (uma conta de
    // serviço própria, registrada pra WBC). O AutoCheck Mechanical não tem
    // essa credencial compartilhada configurada, então por enquanto
    // autentica com o próprio usuário/senha SAP do funcionário (os mesmos
    // já usados na tela de login RFC).
    public static class DocumentSearchService
    {
        public static List<DocumentoEncontrado> BuscarPorEcm(string ecm, string usuario, string senha)
        {
            DTP_DOCUMENT_OUTPUT request = new DTP_DOCUMENT_OUTPUT
            {
                UserCode = usuario,
                DMS = new DTP_DOCUMENT_OUTPUTDMS
                {
                    language = "PT",
                    Originals = new DTP_DOCUMENT_OUTPUTDMSOriginals { CheckIn = false },
                    ReturnClassInfo = false,
                    SearchBy = new DTP_DOCUMENT_OUTPUTDMSSearchBy
                    {
                        Header = new DTP_DOCUMENT_OUTPUTDMSSearchByHeader(),
                        Document = new DTP_DOCUMENT_OUTPUTDMSSearchByDocument
                        {
                            DescriptionList = new string[0],
                            ChangeNumberList = new[] { ecm },
                        },
                    },
                },
            };

            ITF_O_S_DOCUMENT_OUTPUTService service = new ITF_O_S_DOCUMENT_OUTPUTService();
            service.Credentials = new NetworkCredential(usuario, senha);

            DTP_DOCUMENT_OUTPUT_R response;

            try
            {
                response = service.ITF_O_S_DOCUMENT_OUTPUT(request);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Erro ao chamar o serviço de busca de documentos: " + ex.Message, ex);
            }

            if (response.ErrorList?.Error != null && response.ErrorList.Error.Length > 0)
            {
                string mensagens = string.Join(" | ", response.ErrorList.Error.Select(erro =>
                    erro.Description != null && erro.Description.Length > 0 ? erro.Description[0].Value : erro.Code));

                throw new InvalidOperationException("O SAP retornou erro na busca: " + mensagens);
            }

            List<DocumentoEncontrado> resultado = new List<DocumentoEncontrado>();

            if (response.DIRList?.DIR == null)
                return resultado;

            foreach (DTP_DOCUMENT_OUTPUT_RDIRListDIR dir in response.DIRList.DIR)
            {
                DocumentoEncontrado documento = new DocumentoEncontrado
                {
                    DocumentNumber = dir.DocumentNumber,
                    Type = dir.Type,
                    Part = dir.Part,
                    Version = dir.Version,
                    Status = dir.Status,
                    ChangeNumber = dir.ChangeNumber,
                    Descricao = dir.DescriptionList?.Description?.FirstOrDefault()?.Value,
                };

                if (dir.Originals?.Original != null)
                {
                    documento.CaminhosOriginais.AddRange(dir.Originals.Original.Select(o => o.Path));
                }

                resultado.Add(documento);
            }

            return resultado;
        }
    }
}
