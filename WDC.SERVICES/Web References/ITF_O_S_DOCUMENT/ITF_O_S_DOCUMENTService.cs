// Proxy escrito à mão (NÃO gerado pelo wsdl.exe, ao contrário do
// ITF_O_S_DOCUMENT_OUTPUTService.cs) a partir do WSDL real do serviço SOA da
// WEG ITF_O_S_DOCUMENT (singular, distinto do ITF_O_S_DOCUMENT_OUTPUT já
// usado pra busca). Só modela os campos que este app realmente usa -- o
// schema real tem uma árvore de classificação/estrutura bem mais profunda
// (Classifcation, DocumentStructureList, ObjectLinks, etc.) que foi omitida
// de propósito porque XmlSerializer ignora elementos desconhecidos ao
// desserializar a resposta, e todos os campos omitidos são opcionais
// (minOccurs="0") no request.
//
// IMPORTANTE (registrado aqui pra não se perder): o schema desse serviço
// (Originals.Original em DTP_DOCUMENT e DTP_DOCUMENT_R) só tem os campos
// Path/Description/Code/StorageCategory/ApplicationCode/CheckIn/Return --
// nenhum campo de conteúdo binário, nenhuma URL, nenhum base64Binary/
// hexBinary, e o binding é soap:body use="literal" simples, sem MTOM/XOP/
// SOAP-with-Attachments. Ou seja, pela própria definição do serviço, ele não
// tem como devolver o conteúdo do arquivo -- Return=true provavelmente só
// sinaliza um checkout/reserva do lado do SAP, não um download. Mesmo assim,
// foi implementado e é chamado de verdade (a pedido) pra confirmar isso
// empiricamente e trazer a resposta real do SAP como evidência.
//
// Diferença em relação ao ITF_O_S_DOCUMENT_OUTPUTService: aquele é chamado
// via SoapClientFactory().Create(typeof(...), "634-049") -- "634-049" é o
// código de registro desse serviço no catálogo SOA interno da WEG, que
// resolve endereço + credencial. Não temos o código de registro equivalente
// pra este serviço (só o WSDL cru), então aqui a URL é montada direto a
// partir do WSDL, sem passar pelo SoapClientFactory. A mesma ressalva já
// registrada em DocumentSearchService.cs se aplica aqui também: o endereço
// brjgs916:50000 direto já foi recusado pela rede numa tentativa anterior
// (com outro serviço) -- é bem possível que essa chamada falhe pelo mesmo
// motivo, até o time de infra da WEG liberar o acesso.
#pragma warning disable 1591

namespace WDC.SERVICES.ITF_O_S_DOCUMENT
{
    using System.Web.Services.Protocols;
    using System.Xml.Schema;
    using System.Xml.Serialization;

    [System.Web.Services.WebServiceBindingAttribute(Name = "ITF_O_S_DOCUMENTBinding", Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class ITF_O_S_DOCUMENTService : SoapHttpClientProtocol
    {
        public ITF_O_S_DOCUMENTService()
        {
            this.Url = "http://brjgs916:50000/sap/xi/engine?type=entry&version=3.0&Sender.Service=*&Interface=http%3A%2F%2Fsoa.weg.net%2F70%2Fpp%2FDocument%2Fsender%5EITF_O_S_DOCUMENT";
        }

        [SoapDocumentMethodAttribute("http://sap.com/xi/WebService/soap1.1", Use = System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle = SoapParameterStyle.Bare)]
        [return: XmlElementAttribute("MTP_DOCUMENT_R", Namespace = "http://soa.weg.net/70/pp/Document/sender")]
        public DTP_DOCUMENT_R ITF_O_S_DOCUMENT([XmlElementAttribute(Namespace = "http://soa.weg.net/70/pp/Document/sender")] DTP_DOCUMENT MTP_DOCUMENT)
        {
            object[] results = this.Invoke("ITF_O_S_DOCUMENT", new object[] { MTP_DOCUMENT });
            return (DTP_DOCUMENT_R)results[0];
        }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT
    {
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string UserCode { get; set; }

        [XmlArrayAttribute(Form = XmlSchemaForm.Unqualified)]
        [XmlArrayItemAttribute("DIR", Form = XmlSchemaForm.Unqualified, IsNullable = false)]
        public DTP_DOCUMENTDIR[] DIRList { get; set; }

        [XmlAttributeAttribute]
        public string language { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENTDIR
    {
        // Ordem dos campos abaixo precisa bater com a ordem do xsd:sequence
        // real (DocumentNumber, DocumentType, DocumentPart, DocumentVersion,
        // [Template omitido], DesignOffice, Status, [ChangeNumber e
        // SuperiorDocument omitidos], Originals, DescriptionList, [demais
        // listas omitidas], ReturnClassification) -- XmlSerializer serializa
        // na ordem de declaração da classe quando não usa Order= explícito.
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentNumber { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentType { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentPart { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentVersion { get; set; }

        // Obrigatório no schema (sem minOccurs="0") mas não sabemos o valor
        // de verdade pra esse ambiente -- mandado como string vazia (válido
        // pro XSD, que só limita maxLength) só pra o elemento existir.
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DesignOffice { get; set; } = string.Empty;

        // Mesma observação do DesignOffice: obrigatório, valor real
        // desconhecido.
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Status { get; set; } = string.Empty;

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENTDIROriginals Originals { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENTDIRDescriptionList DescriptionList { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public bool ReturnClassification { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENTDIROriginals
    {
        [XmlElementAttribute("Original", Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENTDIROriginalsOriginal[] Original { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENTDIROriginalsOriginal
    {
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Path { get; set; }

        // Obrigatório no schema -- string vazia se não tivermos uma
        // descrição de verdade pra mandar.
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Description { get; set; } = string.Empty;

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Code { get; set; }

        // Obrigatório no schema -- valor real desconhecido pra esse
        // ambiente (categoria de armazenamento do DMS).
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string StorageCategory { get; set; } = string.Empty;

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string ApplicationCode { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public bool CheckIn { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public bool Return { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENTDIRDescriptionList
    {
        [XmlElementAttribute("Description", Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENTDIRDescriptionListDescription[] Description { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENTDIRDescriptionListDescription
    {
        [XmlAttributeAttribute]
        public string language { get; set; }

        [XmlTextAttribute]
        public string Value { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_R
    {
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENT_RErrorList ErrorList { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string UserCode { get; set; }

        [XmlArrayAttribute(Form = XmlSchemaForm.Unqualified)]
        [XmlArrayItemAttribute("DIR", Form = XmlSchemaForm.Unqualified, IsNullable = false)]
        public DTP_DOCUMENT_RDIR[] DIRList { get; set; }

        [XmlAttributeAttribute]
        public string language { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_RErrorList
    {
        [XmlElementAttribute("Error", Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENT_RErrorListError[] Error { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_RErrorListError
    {
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Code { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string TYPE { get; set; }

        [XmlElementAttribute("Description", Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENT_RErrorListErrorDescription[] Description { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_RErrorListErrorDescription
    {
        [XmlAttributeAttribute]
        public string language { get; set; }

        [XmlTextAttribute]
        public string Value { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_RDIR
    {
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentNumber { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentType { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentPart { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DocumentVersion { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string DesignOffice { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Status { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string ChangeNumber { get; set; }

        [XmlArrayAttribute(Form = XmlSchemaForm.Unqualified)]
        [XmlArrayItemAttribute("Original", Form = XmlSchemaForm.Unqualified, IsNullable = false)]
        public DTP_DOCUMENT_RDIROriginal[] Originals { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENT_RDIRDescriptionList DescriptionList { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_RDIROriginal
    {
        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Path { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Description { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string Code { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string StorageCategory { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public string ApplicationCode { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public bool CheckIn { get; set; }

        [XmlElementAttribute(Form = XmlSchemaForm.Unqualified)]
        public bool Return { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_RDIRDescriptionList
    {
        [XmlElementAttribute("Description", Form = XmlSchemaForm.Unqualified)]
        public DTP_DOCUMENT_RDIRDescriptionListDescription[] Description { get; set; }
    }

    [System.SerializableAttribute]
    [XmlTypeAttribute(AnonymousType = true, Namespace = "http://soa.weg.net/70/pp/Document/sender")]
    public partial class DTP_DOCUMENT_RDIRDescriptionListDescription
    {
        [XmlAttributeAttribute]
        public string language { get; set; }

        [XmlTextAttribute]
        public string Value { get; set; }
    }
}
