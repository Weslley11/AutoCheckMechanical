using eDrawings.Interop.EModelViewControl;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace WDC.EDRAWINGS
{
    // Prova de conceito: abre um arquivo no eDrawings (via automação COM do
    // controle ActiveX, não o eDrawings.exe como processo separado) e mede
    // quanto tempo leva pra carregar + calcular massa/folhas/camadas -- pra
    // comparar contra o mesmo cálculo via SolidWorks (SolidWorksSession +
    // CreateMassProperty). NÃO substitui os checks reais (bloco de título,
    // cotas, balões): a API do eDrawings (EModelViewControl) não expõe
    // propriedade customizada nem valor de cota, só o que está aqui --
    // confirmado via análise da DLL real (eDrawings.Interop.
    // EModelViewControl.dll), não é chute.
    //
    // Nunca testado contra uma instalação real de eDrawings -- em especial,
    // o CLSID em EDrawingsHost é do eDrawings 2022 (ver comentário lá), e o
    // loop de espera abaixo usa Application.DoEvents() (bombeamento manual
    // de mensagens do Windows, igual ao padrão já usado neste app via
    // Dispatcher.Invoke em espera síncrona) porque OpenDoc parece ser
    // assíncrono (dispara OnFinishedLoadingDocument/OnFailedLoadingDocument
    // depois, não bloqueia até terminar) -- ainda não confirmado.
    public static class EDrawingsMassService
    {
        public static EDrawingsMassResult AbrirEMedir(string caminhoArquivo)
        {
            Stopwatch cronometro = Stopwatch.StartNew();

            using (Form janela = new Form
            {
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                // Fora da área visível de qualquer monitor comum -- o
                // AxHost precisa de uma janela/handle real pra inicializar
                // o ActiveX, mas não precisa aparecer pro usuário.
                Location = new Point(-3000, -3000),
                Width = 10,
                Height = 10
            })
            using (EDrawingsHost host = new EDrawingsHost { Dock = DockStyle.Fill })
            {
                EModelViewControl controle = null;

                host.ControlLoaded += ctrl => controle = ctrl;
                janela.Controls.Add(host);
                janela.Show();

                DateTime limiteInicializacao = DateTime.Now.AddSeconds(15);

                while (controle == null && DateTime.Now < limiteInicializacao)
                {
                    Application.DoEvents();
                    Thread.Sleep(20);
                }

                if (controle == null)
                {
                    return new EDrawingsMassResult
                    {
                        Sucesso = false,
                        Erro = "Não foi possível inicializar o controle ActiveX do eDrawings (CLSID não encontrado/registrado, ou tempo esgotado). Confira se o eDrawings está instalado nesta máquina.",
                        TempoDecorridoMs = cronometro.ElapsedMilliseconds
                    };
                }

                bool carregou = false;
                bool falhou = false;
                string erroCarregamento = null;

                controle.OnFinishedLoadingDocument += _ => carregou = true;
                controle.OnFailedLoadingDocument += (nomeArquivo, codigoErro, mensagemErro) =>
                {
                    falhou = true;
                    erroCarregamento = $"[{codigoErro}] {mensagemErro}";
                };

                try
                {
                    controle.OpenDoc(caminhoArquivo, false, false, true, "");
                }
                catch (Exception ex)
                {
                    return new EDrawingsMassResult
                    {
                        Sucesso = false,
                        Erro = "OpenDoc lançou exceção: " + ex.Message,
                        TempoDecorridoMs = cronometro.ElapsedMilliseconds
                    };
                }

                DateTime limiteCarregamento = DateTime.Now.AddSeconds(60);

                while (!carregou && !falhou && DateTime.Now < limiteCarregamento)
                {
                    Application.DoEvents();
                    Thread.Sleep(20);
                }

                if (falhou)
                {
                    return new EDrawingsMassResult
                    {
                        Sucesso = false,
                        Erro = "eDrawings não conseguiu abrir o arquivo: " + erroCarregamento,
                        TempoDecorridoMs = cronometro.ElapsedMilliseconds
                    };
                }

                if (!carregou)
                {
                    return new EDrawingsMassResult
                    {
                        Sucesso = false,
                        Erro = "Tempo esgotado esperando o eDrawings carregar o arquivo.",
                        TempoDecorridoMs = cronometro.ElapsedMilliseconds
                    };
                }

                try
                {
                    double massa = (double)controle.MassProperty[EMVMassProperty.eMVMass];
                    int folhas = controle.SheetCount;
                    int camadas = controle.LayerCount;

                    cronometro.Stop();

                    return new EDrawingsMassResult
                    {
                        Sucesso = true,
                        MassaKg = massa,
                        QuantidadeFolhas = folhas,
                        QuantidadeCamadas = camadas,
                        TempoDecorridoMs = cronometro.ElapsedMilliseconds
                    };
                }
                catch (Exception ex)
                {
                    return new EDrawingsMassResult
                    {
                        Sucesso = false,
                        Erro = "Arquivo carregou, mas falhou ao ler MassProperty/SheetCount/LayerCount: " + ex.Message,
                        TempoDecorridoMs = cronometro.ElapsedMilliseconds
                    };
                }
            }
        }
    }
}
