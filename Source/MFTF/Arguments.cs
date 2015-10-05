/*
* Arguments class: application arguments interpreter
*
* Authors:		R. LOPES
* Contributors:	R. LOPES
* Created:		25 October 2002
* Modified:		28 October 2002
*
* Version:		1.0
2015 - Modified by Ignacio J. Perez J.
*/

using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace MFT_fileoper
{
    public class Arguments
    {
        // Variables
        public StringDictionary Parameters = new StringDictionary();

        // Constructor
        public Arguments(string[] Args)
        {
            Regex Spliter = new Regex(@"^-{1,2}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Regex Remover = new Regex(@"^[""]?(.*?)[""]?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            string Parameter = null;
            string[] Parts;
            foreach (string Txt in Args)
            {
                string newTxt = "";
                if (Txt == "\\") { newTxt = Txt + "\\"; } else { newTxt = Txt; }
                Parts = Spliter.Split(newTxt, 3);
                switch (Parts.Length)
                {
                    case 1:
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter))
                            {
                                Parts[0] = Remover.Replace(Parts[0], "$1");
                                Parameters.Add(Parameter, Parts[0]);
                            }
                            Parameter = null;
                        }
                        break;
                    case 2:
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter)) Parameters.Add(Parameter, "");
                        }
                        Parameter = Parts[1];
                        break;
                    case 3:
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter)) Parameters.Add(Parameter, "true");
                        }
                        Parameter = Parts[1];
                        if (!Parameters.ContainsKey(Parameter))
                        {
                            Parts[2] = Remover.Replace(Parts[2], "$1");
                            Parameters.Add(Parameter, Parts[2]);
                        }
                        Parameter = null;
                        break;
                }
            }
            if (Parameter != null)
            {
                if (!Parameters.ContainsKey(Parameter)) Parameters.Add(Parameter, "");
            }
        }
        public string this[string Param]
        {
            get
            {
                return (Parameters[Param]);
            }
        }
    }
}
