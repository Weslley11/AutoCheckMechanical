using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml.Serialization;

namespace AutoCheckMechanical.Services.DocumentOutput
{
    // Proxy SOAP escrito à mão pro Web Service ITF_O_S_DOCUMENT_OUTPUT
    // (código de registro 634-049), no mesmo formato que o wsdl.exe geraria
    // (é assim que o WBC referencia ITF_O_S_MATERIAL/ITF_O_S_BOM_INPUT --
    // "Referência de Web" clássica, SoapHttpClientProtocol, não WCF).
    //
    // Endereço e binding vêm direto do WSDL real informado pelo usuário; o
    // estilo é Document/Literal/Bare (a wsdl:part referencia o elemento
    // global diretamente, sem wrapper), por isso ParameterStyle = Bare.
    [WebServiceBinding(Name = "ITF_O_S_DOCUMENT_OUTPUTBinding", Namespace = "http://soa.weg.net/70/pp/DocumentOutput/sender")]
    public class ITF_O_S_DOCUMENT_OUTPUTService : SoapHttpClientProtocol
    {
        public ITF_O_S_DOCUMENT_OUTPUTService()
        {
            Url = "http://brjgs916:50000/sap/xi/engine?type=entry&version=3.0&Sender.Service=ED0_604&Interface=http%3A%2F%2Fsoa.weg.net%2F70%2Fpp%2FDocumentOutput%2Fsender%5EITF_O_S_DOCUMENT_OUTPUT";
        }

        [SoapDocumentMethod("http://sap.com/xi/WebService/soap1.1",
            RequestNamespace = "http://soa.weg.net/70/pp/DocumentOutput/sender",
            ResponseNamespace = "http://soa.weg.net/70/pp/DocumentOutput/sender",
            ParameterStyle = SoapParameterStyle.Bare)]
        [return: XmlElement("MTP_DOCUMENT_OUTPUT_R", Namespace = "http://soa.weg.net/70/pp/DocumentOutput/sender")]
        public DTP_DOCUMENT_OUTPUT_R ITF_O_S_DOCUMENT_OUTPUT(
            [XmlElement("MTP_DOCUMENT_OUTPUT", Namespace = "http://soa.weg.net/70/pp/DocumentOutput/sender")]
            DTP_DOCUMENT_OUTPUT MTP_DOCUMENT_OUTPUT)
        {
            object[] results = Invoke("ITF_O_S_DOCUMENT_OUTPUT", new object[] { MTP_DOCUMENT_OUTPUT });
            return (DTP_DOCUMENT_OUTPUT_R)results[0];
        }
    }
}
