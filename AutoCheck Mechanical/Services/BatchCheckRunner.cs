using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AutoCheckMechanical.Core;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical.Services
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

            try
            {
                doc = app.GetOpenDocumentByName(filePath) as ModelDoc2;
                wasAlreadyOpen = doc != null;

                if (doc == null)
                {
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

                CheckContext context = new CheckContext(app, doc)
                {
                    ForcarChecksDeChapa = forcarChecksDeChapa
                };

                item.SheetCount = context.SheetCount;
                item.Results = engine.Execute(context);
                item.ThumbnailPath = ThumbnailStore.Generate(doc, filePath);
            }
            catch (COMException ex)
            {
                item.OpenFailed = true;
                item.OpenError = ex.Message;
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
