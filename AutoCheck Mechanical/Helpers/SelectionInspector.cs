using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Helpers
{
    public static class SelectionInspector
    {
        public static void Dump(ModelDoc2 model, CheckResult result)
        {
            if (model == null)
                return;

            SelectionMgr selMgr =
                model.SelectionManager;

            int count = selMgr.GetSelectedObjectCount2(-1);

            result.AddLog("--------------------------------");
            result.AddLog($"Objetos Selecionados: {count}");

            for (int i = 1; i <= count; i++)
            {
                object obj =
                    selMgr.GetSelectedObject6(i, -1);

                int type =
                    selMgr.GetSelectedObjectType3(i, -1);

                result.AddLog("");
                result.AddLog($"Índice: {i}");
                result.AddLog($"Tipo SW: {type}");

                if (obj == null)
                {
                    result.AddLog("Objeto NULL");
                    continue;
                }

                result.AddLog($"CLR: {obj.GetType().FullName}");

                try
                {
                    Entity ent = obj as Entity;

                    if (ent != null)
                    {
                        result.AddLog("Implementa Entity");
                        result.AddLog($"EntityType = {ent.GetType()}");
                    }
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    result.AddLog($"Erro ao inspecionar entidade: {ex.Message}");
                }

                result.AddLog("--------------------------------");
            }
        }
    }
}