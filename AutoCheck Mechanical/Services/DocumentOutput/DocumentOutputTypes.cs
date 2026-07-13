using System;
using System.Xml.Serialization;

namespace AutoCheckMechanical.Services.DocumentOutput
{
    // Tipos gerados manualmente a partir do WSDL real de ITF_O_S_DOCUMENT_OUTPUT
    // (interface SOA da WEG, código de registro 634-049, mesmo estilo que o
    // WBC usa para Material/BOM_Input). Como não temos como rodar wsdl.exe
    // aqui, essas classes replicam à mão o que essa ferramenta geraria: uma
    // classe por complexType, na mesma ordem de campos do XSD (importante --
    // XmlSerializer serializa na ordem declarada das propriedades) e sem
    // namespace nos elementos locais (o schema não declara
    // elementFormDefault="qualified", então os elementos filhos são
    // unqualified por padrão; só os elementos-raiz do envelope SOAP usam o
    // namespace do serviço).

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT
    {
        public string UserCode;

        public DTP_DOCUMENT_OUTPUTDMS DMS;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUTDMS
    {
        [XmlElement(IsNullable = true)]
        public DTP_ORDER_BY_LIST2 OrderByList;

        [XmlElement(IsNullable = true)]
        public DTP_DOCUMENT_OUTPUTDMSDocument Document;

        [XmlElement(IsNullable = true)]
        public DTP_INDEX Index;

        public DTP_DOCUMENT_OUTPUTDMSOriginals Originals;

        public bool ReturnClassInfo;

        [XmlElement(IsNullable = true)]
        public string GetObjectLinks;

        [XmlElement(IsNullable = true)]
        public bool ReturnCurrentVersion;

        [XmlElement(IsNullable = true)]
        public DateTime ValidFrom;

        [XmlIgnore]
        public bool ValidFromSpecified;

        [XmlElement(IsNullable = true)]
        public DateTime ValidTo;

        [XmlIgnore]
        public bool ValidToSpecified;

        [XmlElement(IsNullable = true)]
        public bool ReturnDocumentStructure;

        public DTP_DOCUMENT_OUTPUTDMSSearchBy SearchBy;

        // Igual o request.Material.language = "PT" do MaterialService.cs do
        // WBC (mesmo nível: no objeto-container da busca, não no request raiz).
        [XmlAttribute]
        public string language;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUTDMSDocument
    {
        public string Status;
        public string User;
        public string DesignOffice;
        public DTP_DOCUMENT_HEADER SuperiorDocument;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUTDMSOriginals
    {
        public bool CheckIn;
        public string Path;
        [XmlElement(IsNullable = true)]
        public bool URL;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUTDMSSearchBy
    {
        public DTP_DOCUMENT_OUTPUTDMSSearchByHeader Header;

        public DTP_DOCUMENT_OUTPUTDMSSearchByDocument Document;

        [XmlElement(IsNullable = true)]
        public DTP_DOCUMENT_OUTPUTDMSSearchByObjectLinks ObjectLinks;

        // Classification/Characteristic (busca por classe/característica) não
        // são necessários pra busca por ECM -- omitidos por simplicidade.
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUTDMSSearchByHeader
    {
        [XmlArray(IsNullable = true)]
        [XmlArrayItem("Document", IsNullable = false)]
        public DTP_DOCUMENT_HEADER[] HeaderInfoList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUTDMSSearchByDocument
    {
        [XmlArray]
        [XmlArrayItem("Description", IsNullable = false)]
        public string[] DescriptionList;

        [XmlArray]
        [XmlArrayItem("ChangeNumber", IsNullable = false)]
        public string[] ChangeNumberList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUTDMSSearchByObjectLinks
    {
        [XmlArray(IsNullable = true)]
        [XmlArrayItem("MaterialNumber", IsNullable = false)]
        public string[] MaterialList;

        [XmlArray(IsNullable = true)]
        [XmlArrayItem("ClaimNotificationNo", IsNullable = false)]
        public string[] ClaimList;

        [XmlArray(IsNullable = true)]
        [XmlArrayItem("WBSElementKey", IsNullable = false)]
        public string[] WBSElementList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_HEADER
    {
        public string DocumentNumber;
        public string Type;
        public string Part;
        public string Version;
    }

    [XmlType(Namespace = "")]
    public class DTP_INDEX
    {
        public int From;
        public int To;
    }

    [XmlType(Namespace = "")]
    public class DTP_ORDER_BY_LIST2
    {
        [XmlElement("OrderBy")]
        public DTP_ORDER_BY_LIST2OrderBy[] OrderByItems;
    }

    [XmlType(Namespace = "")]
    public class DTP_ORDER_BY_LIST2OrderBy
    {
        public string Field;
        public int Sequence;
        public string Sort;
    }

    // ---- Resposta ----

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_R
    {
        public DTP_ERRORS_R ErrorList;
        public string UserCode;
        public DTP_DOCUMENT_OUTPUT_RDIRList DIRList;

        [XmlAttribute]
        public string language;
    }

    [XmlType(Namespace = "")]
    public class DTP_ERRORS_R
    {
        [XmlElement("Error")]
        public DTP_ERRORS_RError[] Error;
    }

    [XmlType(Namespace = "")]
    public class DTP_ERRORS_RError
    {
        public string Code;
        public string TYPE;

        [XmlElement("Description")]
        public DTP_ERRORS_RErrorDescription[] Description;
    }

    [XmlType(Namespace = "")]
    public class DTP_ERRORS_RErrorDescription
    {
        [XmlText]
        public string Value;

        [XmlAttribute]
        public string language;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRList
    {
        [XmlElement("DIR")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIR[] DIR;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIR
    {
        public string DocumentNumber;
        public string Type;
        public string Part;
        public string Version;
        public string DesignOffice;
        public DTP_DOCUMENT_HEADER Template;
        public string Status;
        public string ChangeNumber;
        public DTP_DOCUMENT_HEADER SuperiorDocument;

        [XmlElement(IsNullable = true)]
        public DateTime ValidFrom;
        [XmlIgnore]
        public bool ValidFromSpecified;

        [XmlElement(IsNullable = true)]
        public DateTime ValidTo;
        [XmlIgnore]
        public bool ValidToSpecified;

        public DTP_DOCUMENT_OUTPUT_RDIRListDIROriginals Originals;
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRDescriptionList DescriptionList;
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinks ObjectLinks;
        public bool ReturnClassification;
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRClassification Classification;

        [XmlElement(IsNullable = true)]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRDocumentStructureList DocumentStructureList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIROriginals
    {
        [XmlElement("Original")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIROriginalsOriginal[] Original;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIROriginalsOriginal
    {
        public string Path;
        public string Description;
        public string Code;
        public string StorageCategory;
        public string ApplicationCode;
        public bool CheckIn;
        public string CheckedOutUser;
        public string URL;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRDescriptionList
    {
        [XmlElement("Description")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRDescriptionListDescription[] Description;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRDescriptionListDescription
    {
        [XmlText]
        public string Value;

        [XmlAttribute]
        public string language;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinks
    {
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksWBSElementList WBSElementList;

        [XmlArray]
        [XmlArrayItem("MasterMaterialNumber")]
        public string[] MasterMaterialList;

        [XmlArray]
        [XmlArrayItem("Claim")]
        public string[] ClaimList;

        public DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksDocumentInfoList DocumentInfoList;
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksPurchaseOrderList PurchaseOrderList;

        [XmlArray(IsNullable = true)]
        [XmlArrayItem("EquipmentNumber")]
        public string[] EquipmentList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksWBSElementList
    {
        [XmlElement("WBSElement")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksWBSElementListWBSElement[] WBSElement;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksWBSElementListWBSElement
    {
        public string WBSElementKey;
        public string Version;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksDocumentInfoList
    {
        [XmlElement("DocumentInfo")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksDocumentInfoListDocumentInfo[] DocumentInfo;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksDocumentInfoListDocumentInfo
    {
        public string DocumentNumber;
        public string DocumentType;
        public string DocumentPart;
        public string DocumentVersion;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksPurchaseOrderList
    {
        [XmlElement("PurchaseOrder")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksPurchaseOrderListPurchaseOrder[] PurchaseOrder;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRObjectLinksPurchaseOrderListPurchaseOrder
    {
        public string PurchasingDocumentNumber;
        public string ItemNumber;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRClassification
    {
        public string ClassType;
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassList ClassList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassList
    {
        [XmlElement("Class")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClass[] Class;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClass
    {
        public string ClassNumber;
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClassCharacteristicList CharacteristicList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClassCharacteristicList
    {
        [XmlElement("Characteristic")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClassCharacteristicListCharacteristic[] Characteristic;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClassCharacteristicListCharacteristic
    {
        public string Name;
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClassCharacteristicListCharacteristicValueList ValueList;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRClassificationClassListClassCharacteristicListCharacteristicValueList
    {
        [XmlElement("Value")]
        public string[] Value;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRDocumentStructureList
    {
        [XmlElement("DocumentStructure")]
        public DTP_DOCUMENT_OUTPUT_RDIRListDIRDocumentStructureListDocumentStructure[] DocumentStructure;
    }

    [XmlType(Namespace = "")]
    public class DTP_DOCUMENT_OUTPUT_RDIRListDIRDocumentStructureListDocumentStructure
    {
        public string Item;
        public string DocumentNumber;
        public string DocumentType;
        public string DocumentPart;
        public string DocumentVersion;
        public decimal Quantity;
        public string SortString;
        public string QuantityIsFixed;
        public string CADIndicator;
        public string Text;
    }
}
