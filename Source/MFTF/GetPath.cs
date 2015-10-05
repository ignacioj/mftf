using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;

namespace MFT_fileoper
{
    class GetPath
    {
        public static Dictionary<UInt32, FileNameAndParentFrn> soloMFTDictOffsets = new Dictionary<UInt32, FileNameAndParentFrn>();
        private string _drive = "";
        public static string soloMFTGetFullyQualifiedPath(UInt32 frn)
        {
            string retval = string.Empty; ;
            FileNameAndParentFrn fnFRN = null;
            if (frn >= 0)
            {
                if (soloMFTDictOffsets.TryGetValue(frn, out fnFRN))
                {
                    retval = fnFRN.Name;
                    while (fnFRN.ParentFrn != 0)
                    {
                        if (soloMFTDictOffsets.TryGetValue(fnFRN.ParentFrn, out fnFRN))
                        {
                            string name = fnFRN.Name;
                            retval = Path.Combine(name, retval);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                throw new ArgumentException("Invalid argument", "frn");
            }
            return retval;
        }

        public string Drive
        {
            get { return _drive; }
            set { _drive = value; }
        }

        public class FileNameAndParentFrn
        {
            #region Properties
            private string _name;
            public string Name
            {
                get { return _name; }
                set { _name = value; }
            }
            private UInt32 _parentFrn;
            public UInt32 ParentFrn
            {
                get { return _parentFrn; }
                set { _parentFrn = value; }
            }
            private UInt64 _recordOffset;
            public UInt64 RecordOffset
            {
                get { return _recordOffset; }
                set { _recordOffset = value; }
            }
            #endregion

            #region Constructor
            public FileNameAndParentFrn(string name, UInt32 parentFrn, UInt64 recordOffset = 0)
            {
                if (name != null && name.Length > 0)
                {
                    _name = name;
                }
                else
                {
                    throw new ArgumentException("Invalid argument: null or Length = zero", "name");
                }
                if (!(parentFrn < 0))
                {
                    _parentFrn = parentFrn;
                }
                else
                {
                    throw new ArgumentException("Invalid argument: less than zero", "parentFrn");
                }
                _recordOffset = recordOffset;
            }
            #endregion
        }
    }
}
