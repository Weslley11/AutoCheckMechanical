using System;
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

        private SolidWorksSession()
        {
        }

        public static SolidWorksSession Connect()
        {
            SolidWorksSession session =
                new SolidWorksSession();

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
    }
}