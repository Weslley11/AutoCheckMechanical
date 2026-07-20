using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Core
{
    public class SolidWorksSession
    {
        public SldWorks Application { get; private set; }

        public ModelDoc2 ActiveDocument { get; private set; }

        public bool IsConnected
        {
            get
            {
                return Application != null;
            }
        }

        // Preenchido quando há mais de um processo SLDWORKS.exe rodando --
        // confirmado como causa real de RPC_E_SERVERFAULT ao abrir
        // documentos (a automação via Marshal.GetActiveObject conecta num
        // processo, mas o comportamento fica inconsistente/instável com
        // mais de uma instância aberta). Null quando só tem um processo (ou
        // nenhum).
        public string AvisoProcessosDuplicados { get; private set; }

        private SolidWorksSession()
        {
        }

        public static SolidWorksSession Connect()
        {
            SolidWorksSession session =
                new SolidWorksSession();

            session.AvisoProcessosDuplicados = DetectarProcessosDuplicados();

            try
            {
                session.Application =
                    (SldWorks)Marshal.GetActiveObject(
                        "SldWorks.Application");

                session.ActiveDocument =
                    session.Application.ActiveDoc;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"SolidWorksSession.Connect: falha ao conectar ({ex.GetType().Name}): {ex.Message}");

                return session;
            }

            return session;
        }

        private static string DetectarProcessosDuplicados()
        {
            try
            {
                Process[] processos = Process.GetProcessesByName("SLDWORKS");

                if (processos.Length <= 1)
                    return null;

                string ids = string.Join(", ", processos.Select(p => p.Id));

                return $"{processos.Length} processos SLDWORKS.exe rodando ao mesmo tempo (PIDs: {ids}) -- " +
                    "isso já causou RPC_E_SERVERFAULT ao abrir documentos. Feche todos e abra o SolidWorks " +
                    "de novo, só uma vez.";
            }
            catch
            {
                // Diagnóstico best-effort -- nunca deve impedir a conexão
                // em si.
                return null;
            }
        }
    }
}