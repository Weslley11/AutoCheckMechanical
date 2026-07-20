using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WDC.SERVICES.Core;
using WDC.MODEL;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace WDC.SERVICES
{
    public static class BatchCheckRunner
    {
        // Recebe a SolidWorksSession (não o SldWorks app direto) de propósito
        // -- SolidWorksSession.Application é resolvida aqui dentro, na mesma
        // assembly (WDC.SERVICES) que criou a conexão de verdade
        // (SolidWorksSession.Connect(), também em WDC.SERVICES). Passar o
        // objeto COM SldWorks já "desembrulhado" como parâmetro através da
        // fronteira entre WDC.VIEWMODEL e WDC.SERVICES é a diferença
        // concreta entre esta versão e a que dava RPC_E_SERVERFAULT em
        // OpenDoc6 (mas não em GetOpenDocumentByName, chamado antes no mesmo
        // objeto) -- suspeita de problema de marshaling de parâmetro ref
        // (Errors/Warnings) especificamente quando o valor do SldWorks
        // atravessa a fronteira de assembly antes da chamada.
        public static List<BatchFileResult> Run(
            SolidWorksSession session,
            CheckEngine engine,
            IEnumerable<string> filePaths,
            Action<BatchFileResult> aoConcluirArquivo = null,
            bool forcarChecksDeChapa = false)
        {
            SldWorks app = session.Application;
            List<BatchFileResult> results = new List<BatchFileResult>();

            ModelDoc2 originalActiveDoc = app.ActiveDoc as ModelDoc2;

            foreach (string filePath in filePaths)
            {
                BatchFileResult item = RunSingleFile(app, engine, filePath, forcarChecksDeChapa);

                results.Add(item);

                aoConcluirArquivo?.Invoke(item);
            }

            if (originalActiveDoc != null)
            {
                try
                {
                    int activateErrors = 0;
                    app.ActivateDoc2(originalActiveDoc.GetTitle(), false, ref activateErrors);
                }
                catch (COMException)
                {
                }
            }

            return results;
        }

        private static BatchFileResult RunSingleFile(SldWorks app, CheckEngine engine, string filePath, bool forcarChecksDeChapa = false)
        {
            BatchFileResult item = new BatchFileResult
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath
            };

            ModelDoc2 doc = null;
            bool wasAlreadyOpen = false;

            // Etapa é registrada a cada passo pra saber exatamente qual
            // chamada COM falhou quando o catch pegar um COMException --
            // "RPC_E_SERVERFAULT" sozinho não diz se foi
            // GetOpenDocumentByName, OpenDoc6, o construtor de CheckContext
            // ou engine.Execute.
            string etapa = "GetOpenDocumentByName";

            try
            {
                doc = app.GetOpenDocumentByName(filePath) as ModelDoc2;
                wasAlreadyOpen = doc != null;

                if (doc == null)
                {
                    etapa = "OpenDoc6";

                    int errors = 0;
                    int warnings = 0;

                    // RPC_E_SERVERFAULT em OpenDoc6 (mas nunca em
                    // GetOpenDocumentByName, chamado antes no mesmo app) se
                    // mostrou consistente mesmo depois de eliminar qualquer
                    // objeto COM cruzando entre projetos -- pode ser uma
                    // falha transitória do lado do SolidWorks (ele "emitiu
                    // uma exceção" ao processar a chamada, não é erro de
                    // marshaling do lado do cliente). Tenta de novo algumas
                    // vezes com um intervalo antes de desistir, em vez de
                    // falhar na primeira.
                    const int tentativasAbrir = 3;
                    COMException ultimoErroAbrir = null;

                    for (int tentativa = 0; tentativa < tentativasAbrir; tentativa++)
                    {
                        try
                        {
                            errors = 0;
                            warnings = 0;

                            doc = app.OpenDoc6(
                                filePath,
                                (int)swDocumentTypes_e.swDocDRAWING,
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "",
                                ref errors,
                                ref warnings) as ModelDoc2;

                            ultimoErroAbrir = null;
                            break;
                        }
                        catch (COMException ex)
                        {
                            ultimoErroAbrir = ex;

                            if (tentativa < tentativasAbrir - 1)
                                System.Threading.Thread.Sleep(1000);
                        }
                    }

                    if (ultimoErroAbrir != null)
                    {
                        int hr = ultimoErroAbrir.HResult;
                        var apartmentState = System.Threading.Thread.CurrentThread.GetApartmentState();

                        item.OpenFailed = true;
                        item.OpenError = $"[OpenDoc6] depois de {tentativasAbrir} tentativa(s), HResult=0x{hr:X8}, " +
                            $"ThreadApartment={apartmentState}, ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}: {ultimoErroAbrir.Message}";
                        return item;
                    }

                    if (doc == null)
                    {
                        item.OpenFailed = true;
                        item.OpenError = $"Não foi possível abrir o arquivo (código de erro {errors}).";
                        return item;
                    }
                }

                etapa = "new CheckContext";

                CheckContext context = new CheckContext(app, doc)
                {
                    ForcarChecksDeChapa = forcarChecksDeChapa
                };

                etapa = "context.SheetCount";
                item.SheetCount = context.SheetCount;

                etapa = "engine.Execute";
                item.Results = engine.Execute(context);

                etapa = "ThumbnailStore.Generate";
                item.ThumbnailPath = ThumbnailStore.Generate(doc, filePath);
            }
            catch (COMException ex)
            {
                item.OpenFailed = true;
                item.OpenError = $"[{etapa}] {ex.Message}";
            }
            finally
            {
                if (doc != null && !wasAlreadyOpen)
                {
                    try
                    {
                        app.CloseDoc(doc.GetTitle());
                    }
                    catch (COMException)
                    {
                    }
                }
            }

            return item;
        }
    }
}
