using eDrawings.Interop.EModelViewControl;
using System;
using System.Windows.Forms;

namespace WDC.EDRAWINGS
{
    // Porte quase literal de WFV.EDRAWINGS.EDrawingsHost (mesmo mecanismo
    // real que o WAU Factory Viewer usa) -- EModelViewControl é um controle
    // ActiveX, não um objeto COM de automação "puro" como o
    // SldWorks.Application, então precisa de um AxHost hospedado numa
    // janela de verdade (mesmo que fora da tela) pra inicializar.
    //
    // O CLSID abaixo é o do eDrawings 2022 (confirmado no WFV real) -- este
    // app usa o SolidWorks 2024 (ver HintPath em WDC.SERVICES.csproj), então
    // o CLSID do eDrawings instalado pode ser diferente. Ainda não
    // confirmado contra uma instalação real; se `EDrawingsMassService`
    // falhar ao criar o controle, esse é o primeiro lugar a conferir --
    // regedit em HKEY_CLASSES_ROOT\CLSID procurando "EModelViewControl", ou
    // reinstanciando esse projeto no Visual Studio via "Adicionar
    // Referência > Componentes COM" apontando pro eDrawings 2024 instalado
    // localmente, que gera o CLSID certo automaticamente.
    public class EDrawingsHost : AxHost
    {
        public event Action<EModelViewControl> ControlLoaded;

        public EModelViewControl ModelControl { get; private set; }

        private bool _carregado;

        public EDrawingsHost() : base("22945A69-1191-4DCF-9E6F-409BDE94D101")
        {
            _carregado = false;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            if (_carregado)
                return;

            _carregado = true;
            ModelControl = GetOcx() as EModelViewControl;
            ControlLoaded?.Invoke(ModelControl);
        }
    }
}
