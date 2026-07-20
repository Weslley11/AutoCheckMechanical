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
        public static List<BatchFileResult> Run(
            SldWorks app,
            CheckEngine engine,
            IEnumerable<string> filePaths,
            Action<BatchFileResult> aoConcluirArquivo = null,
            bool forcarChecksDeChapa = false)
        {
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

                    doc = app.OpenDoc6(
                        filePath,
                        (int)swDocumentTypes_e.swDocDRAWING,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                        "",
                        ref errors,
                        ref warnings) as ModelDoc2;

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
