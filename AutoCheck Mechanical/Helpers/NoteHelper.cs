using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical.Helpers
{
    public static class NoteHelper
    {
        public static List<Note> GetAll(View view)
        {
            List<Note> list = new List<Note>();

            foreach (Annotation ann in AnnotationHelper.GetAll(view))
            {
                if (ann.GetType() != (int)swAnnotationType_e.swNote)
                    continue;

                Note note = ann.GetSpecificAnnotation() as Note;

                if (note == null)
                    continue;

                if (!note.IsBomBalloon())
                    list.Add(note);
            }

            return list;
        }

        public static int Count(View view)
        {
            return GetAll(view).Count;
        }
    }
}