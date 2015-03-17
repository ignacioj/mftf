using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;
using System.Threading;

namespace MFT_fileoper
{
    class MFT_get_details
    {
        public static Arguments CommandLine;
        public const byte NTFS_ROOT_MFT = 5;
        public const UInt32 FILE_SIG = 0x454C4946;
        public const UInt32 END_RECORD_SIG = 0xFFFFFFFF;
        public const UInt32 SI_SIG = 0x10;
        public const UInt32 AL_SIG = 0x20;
        public const UInt32 FN_SIG = 0x30;
        public const UInt32 DATA_SIG = 0x80;
        public static UInt32 bytesxSector;
        public static UInt32 sectorxCluster;
        public static string origen;
        public static List<UInt32> refCoincid = new List<UInt32>();
        public static bool copiado = false;
        public static SortedDictionary<UInt64, dataParaCopia> diccDatosCopia = new SortedDictionary<UInt64, dataParaCopia>();
        public static string nameOut;
        public static Dictionary<UInt32,List<UInt32>> listaRecordHijos = new Dictionary<UInt32,List<UInt32>>();

        static void Main(string[] args)
        {
            DateTime StartTime = DateTime.Now;
            List<string> buscadasList = new List<string>();
            List<string> referencesToCopyList = new List<string>();
            CommandLine = new Arguments(args);
                if (CommandLine["h"] != null || args.Length < 3)
                {
                    Console.WriteLine(@"mftf.exe v. 1.1 by Ignacio J. Perez J.
This tool searches for filenames and alternate data streams names by parsing the content of the MFT in a
live system or in a mounted (read-only included) logical drive.
The results are shown with the filetime information from the attributes $SI and $FN and the sizes.
You can also copy files or alternate data streams using the data or ADS references provided in the results.
The copy is made reading directly the clusters in use. You can copy protected system files or files in use.
Options:
    -d drive_letter............................Search/copy files from this logical unit.
    -h........................................This help.
    -f ""string1[|string2 with spaces|string3?...]"".....Find file/directory/ADS names with any of the strings.
    -f ""d:\folder\string""                         .....The path will limit the results to the subfolders. 
                The match is always case insensitive.
                "" as delimiters for the whole group of strings.
                | is the boundary between strings.
                ? al the end of the string specifies an exact coincidence.
    -ff file.txt....................The strings to search for are in file.txt.
                                    One string per line, no separator, use ? as needed.
    -fr string......................Find the string doing a raw search in the 1024 bytes of the MFT record.
                                    It will report coincidences in the unallocated space of the MFT record.
    -fads...........................Find all the ADSs in the logical unit.
    >Can be used with any of the previous find options:
        -fx..................................Save the results in a file in order to use the option -c.
        -ft..................................Show the results in timeline format.
    -i full_path_to_file/directory.......Show information about the path.
    -i record_number.....................Show information of the MFT record.
    -w record_number.....................Write on screen the 1024 bytes of the MFT record.
    -c ""reference1[|reference2...]""......Copy the file/s referenced to this folder.
                                           | is the separator.
    -c list.txt..........................Copy all the files referenced in the file list.txt.
                                           Each line MUST start with: reference + [TAB].
    -cr record_number....................Copy the 1024 bytes of the MFT record to this folder.

Examples:
> MFT-fileoper.exe -d e: -f ""svchost|mvui.dll|string with spaces|exact match?""
> MFT-fileoper.exe -d e -fx -f ""c:\folder\temp.dll|snbclog.exe""
> MFT-fileoper.exe -d e -c ""33:128-1|5623:128-4""");
                }
                else if (CommandLine["d"] != null)
                {
                    string letraDisco = CommandLine["d"].Substring(0, 1) + ":";
                    Console.Write("Unit: {0}\n", letraDisco.ToUpper());
                    origen = string.Format("\\\\.\\{0}", letraDisco);
                    GetPath getFullPath = new GetPath();
                    getFullPath.Drive = MFT_get_details.origen;
                    UInt64 mftOffset = GetDiskInfo();
                    if (mftOffset != 0)
                    {
                        if (CommandLine["fads"] != null)
                        {
                            nameOut = DateTime.Now.ToString("yyMMddHHmmss") + "_References.txt";
                            Console.Write("\nSearching all the ADSs.\n");
                            MakeSoloMFTDict(mftOffset);
                            BuscaTodosADSs(mftOffset);
                        }
                        else if ((CommandLine["fr"] != null) & (CommandLine["fr"] != ""))
                        {
                            nameOut = DateTime.Now.ToString("yyMMddHHmmss") + "_References.txt";
                            string cadeBuscada = CommandLine["fr"];
                            Console.Write("\nRaw search:");
                            Console.WriteLine(CommandLine["fr"]);
                            MakeSoloMFTDict(mftOffset);
                            BuscaCadenaRaw(mftOffset, cadeBuscada);
                        }
                        else if ((CommandLine["ff"] != null) & (CommandLine["f"] != ""))
                        {
                            nameOut = DateTime.Now.ToString("yyMMddHHmmss") + "_References.txt";
                            var buscadasFile = File.ReadAllLines(CommandLine["f"]);
                            buscadasList.AddRange(buscadasFile);
                            Console.WriteLine("\nFinding strings in file {0}", CommandLine["f"]);
                            MakeSoloMFTDict(mftOffset);
                            BuscaCadenas(mftOffset, buscadasList);
                        }
                        else if ((CommandLine["f"] != null) & (CommandLine["f"] != ""))
                        {
                            nameOut = DateTime.Now.ToString("yyMMddHHmmss") + "_References.txt";
                            Console.Write("\nFind:");
                            char[] delimiters = new char[] { '|' };
                            string[] words = CommandLine["f"].Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                            Console.WriteLine(String.Join("|", words));
                            buscadasList.AddRange(words);
                            MakeSoloMFTDict(mftOffset);
                            BuscaCadenas(mftOffset, buscadasList);
                        }
                        else if ((CommandLine["i"] != null) & (CommandLine["i"] != ""))
                        {
                            string path = CommandLine["i"];
                            if (Path.GetPathRoot(path) == (letraDisco + Path.DirectorySeparatorChar))
                            {
                                bool valido = true;
                                MakeSoloMFTDict(mftOffset);
                                refCoincid.Add(GetPath.GetMFTRecordFromPath(path, ref valido));
                                if (valido)
                                {
                                    GetCoinciDetalles();
                                }
                            }
                            else if (Regex.IsMatch(CommandLine["i"], "^[0-9]{1,9}$"))
                            {
                                MakeSoloMFTDict(mftOffset);
                                refCoincid.Add(Convert.ToUInt32(CommandLine["i"], 10));
                                GetCoinciDetalles();
                            }
                            else
                            {
                                Console.WriteLine("\nPath root and disk do not match or wrong record.");
                            }
                        }
                        else if ((CommandLine["w"] != null) & (CommandLine["w"] != ""))
                        {
                            if (Regex.IsMatch(CommandLine["w"], "^[0-9]{1,9}$"))
                            {
                                UInt32 recordToCopy = Convert.ToUInt32(CommandLine["w"], 10);
                                CopiaRawRecord(recordToCopy, mftOffset);
                            }
                            else
                            {
                                Console.WriteLine("\nCheck the reference.");
                            }
                        }
                        else if ((CommandLine["cr"] != null) & (CommandLine["cr"] != ""))
                        {
                            if (Regex.IsMatch(CommandLine["cr"], "^[0-9]{1,9}$"))
                            {
                                UInt32 recordToCopy = Convert.ToUInt32(CommandLine["cr"], 10);
                                CopiaRawRecord(recordToCopy, mftOffset);
                            }
                            else
                            {
                                Console.WriteLine("\nCheck the reference.");
                            }
                        }
                        else if ((CommandLine["c"] != null) & (CommandLine["c"] != ""))
                        {
                            try
                            {
                                if (File.Exists(CommandLine["c"]))
                                {
                                    var listaParaCopia = File.ReadAllLines(CommandLine["c"]);
                                    foreach (string linea in listaParaCopia)
                                    {
                                        referencesToCopyList.Add(linea.Substring(0, linea.IndexOf('\t')));
                                    }
                                    Console.WriteLine("\nReading list of references in file {0}", CommandLine["c"]);
                                }
                                else
                                {
                                    char[] delimiters = new char[] { '|' };
                                    string[] words = CommandLine["c"].Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                                    Console.WriteLine(String.Join("|", words));
                                    referencesToCopyList.AddRange(words);
                                }
                                MakeSoloMFTDict(mftOffset);
                                foreach (string referenceBuscada in referencesToCopyList)
                                {
                                    Console.Write("Copying:" + referenceBuscada);
                                    if (Regex.IsMatch(referenceBuscada, "^[0-9]{1,9}:128-[0-9]{1,4}$")) //De momento solo los DATA: 128
                                    {
                                        string[] recordRef = referenceBuscada.Split(':');
                                        UInt32 recordBuscado = Convert.ToUInt32(recordRef[0], 10);
                                        BuscaMFTRecord(referenceBuscada);
                                        if (copiado) { Console.WriteLine("Copy finished."); }
                                        else { Console.WriteLine("Record not found."); }
                                    }
                                    else { Console.WriteLine("\nReference {0} is incorrect.", referenceBuscada); }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }

                        }
                        Console.WriteLine("\n----------------------------------------");
                        if (CommandLine.Parameters.ContainsKey("fx"))
                        {
                            Console.WriteLine("References saved to file: {0}.", nameOut);
                        }
                        TimeSpan ts = DateTime.Now.Subtract(StartTime);
                        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                                ts.Hours, ts.Minutes, ts.Seconds,
                                ts.Milliseconds / 10);
                        Console.WriteLine("RunTime: " + elapsedTime);
                    }
                }
                else Console.WriteLine("Disk not defined!. Use -d drive_letter.");
        }

        public static void CopiaRawRecord(UInt32 recordToCopy, UInt64 mftOffset)
        {
            string nombreArch = "[" + recordToCopy + "]_" + DateTime.Now.ToString("yyMMddHHmmss") + ".dat";
            bool sigue = true;
            bool loTengo = false;
            MFT_ENTRY mftEntry = new MFT_ENTRY(ReadRaw(mftOffset, 1024));
            while (mftEntry.attributeSig != DATA_SIG)
            {
                mftEntry.offsetToAttribute += mftEntry.attributeLength;
                mftEntry.attributeSig = BitConverter.ToUInt32(mftEntry.rawRecord, mftEntry.offsetToAttribute);
                mftEntry.attributeLength = BitConverter.ToInt16(mftEntry.rawRecord, mftEntry.offsetToAttribute + 4);
            }
            GETDATARUNLIST dataRunlist = new GETDATARUNLIST(mftEntry);
            while ((dataRunlist.runlist != (byte)(0x00)) & (sigue))
            {
                dataRunlist.GETCLUSTERS(mftEntry);
                if (!dataRunlist.isSparse)
                {
                    uint runLength_ = dataRunlist.runLength;
                    byte[] runActualMFT = ReadRaw(dataRunlist.offsetBytesMFT, runLength_ * sectorxCluster * bytesxSector);
                    runLength_ = (runLength_ * sectorxCluster * bytesxSector) / 1024; //clusters a grupos de 1024 (tamaño de entrada de mft)
                    byte[] content = new byte[1024];
                    for (int n = 0; n < runLength_; n++)
                    {
                        Array.Copy(runActualMFT, n * 1024, content, 0, 1024);
                        if (BitConverter.ToUInt32(content, 44) == recordToCopy)
                        {
                            loTengo = true;
                            if (CommandLine["w"] != null)
                            {
                                Console.WriteLine("\n");
                                for (int t = 0; t < 64; t++)
                                {
                                    Console.Write("{0:X3} - ", 0 + (16 * t));
                                    for (int m = 0; m < 16; m++)
                                    {
                                        Console.Write("{0:X2} ", content[m + (16 * t)]);
                                    }
                                    for (int m = 0; m < 16; m++)
                                    {
                                        if ((content[m + (16 * t)] < 32) | (content[m + (16 * t)] == 129) | (content[m + (16 * t)] == 141)
                                         | (content[m + (16 * t)] == 143) | (content[m + (16 * t)] == 144) | (content[m + (16 * t)] == 157)
                                         | (content[m + (16 * t)] == 159))
                                        {
                                            Console.Write(".");
                                        }
                                        else
                                        {
                                            Console.Write(Encoding.Default.GetString(content, m + (16 * t), 1));
                                        }
                                    }
                                    Console.WriteLine();
                                }
                                sigue = false;
                                break;
                            }
                            else
                            {
                                loTengo = true;
                                File.WriteAllBytes(nombreArch, content);
                                Console.WriteLine("\nMFT entry record copied to {0}", nombreArch);
                                sigue = false;
                                break;
                            }
                        }
                    }
                    runActualMFT = null;
                }
                dataRunlist.NEXTDATARUNLIST(mftEntry.rawRecord[dataRunlist.runlistOffset]);
            }
            if (!loTengo)
            {
                Console.WriteLine("\nRecord not found.");
            }
        }

        public static void BuscaTodosADSs(UInt64 mftOffset)
        {
            MFT_ENTRY mftEntry = new MFT_ENTRY(ReadRaw(mftOffset, 1024));
            while (mftEntry.attributeSig != DATA_SIG)
            {
                mftEntry.offsetToAttribute += mftEntry.attributeLength;
                mftEntry.attributeSig = BitConverter.ToUInt32(mftEntry.rawRecord, mftEntry.offsetToAttribute);
                mftEntry.attributeLength = BitConverter.ToInt16(mftEntry.rawRecord, mftEntry.offsetToAttribute + 4);
            }
            GETDATARUNLIST dataRunlist = new GETDATARUNLIST(mftEntry);
            while (dataRunlist.runlist != (byte)(0x00))
            {
                dataRunlist.GETCLUSTERS(mftEntry);
                if (!dataRunlist.isSparse)
                {
                    uint runLength_ = dataRunlist.runLength;
                    byte[] runActualMFT = ReadRaw(dataRunlist.offsetBytesMFT, runLength_ * sectorxCluster * bytesxSector);
                    runLength_ = (runLength_ * sectorxCluster * bytesxSector) / 1024; //clusters a grupos de 1024 (tamaño de entrada de mft)
                    byte[] content = new byte[1024];
                    for (int n = 0; n < runLength_; n++)
                    {
                        Array.Copy(runActualMFT, n * 1024, content, 0, 1024);
                        UInt32 mftSig = BitConverter.ToUInt32(content, 0);
                        MFT_ENTRY infoMFT = new MFT_ENTRY(content);
                        if (!infoMFT.recordValido)
                        {
                            Console.WriteLine("I omit record {0}: has a wrong fixup value", infoMFT.recordNumber);
                            continue;
                        }
                        infoMFT.MFT_NEXT_ATTRIBUTE();
                        while (infoMFT.attributeSig != END_RECORD_SIG)
                        {
                            if (infoMFT.attributeSig == DATA_SIG)
                            {
                                infoMFT.MFT_NEXT_ATTRIBUTE_VALIDO();
                                if (infoMFT.attributeNameLength != 0) //Solo es necesario parsearlo para los ADS,s para buscar sus nombres
                                {
                                    if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde el nombre FN no está en el record base
                                    {
                                        if (!refCoincid.Contains(infoMFT.recordNumber))
                                        {
                                            refCoincid.Add(infoMFT.recordNumber);
                                        }
                                    }
                                    else
                                    {
                                        if (!refCoincid.Contains(infoMFT.fileReferenceToBaseFile))
                                        {
                                            refCoincid.Add(infoMFT.fileReferenceToBaseFile);
                                        }
                                    }
                                }
                                else
                                {
                                    infoMFT.dataBase = infoMFT.recordNumber.ToString() + ":128-" + infoMFT.attributeID.ToString();
                                }
                            }
                            infoMFT.MFT_NEXT_ATTRIBUTE();
                        }
                    }
                    runActualMFT = null;
                }
                dataRunlist.NEXTDATARUNLIST(mftEntry.rawRecord[dataRunlist.runlistOffset]);
            }
            Console.WriteLine("Results: {0}", refCoincid.Count);
            GetCoinciDetalles();
        }

        public static UInt64 GetDiskInfo()
        {
            UInt64 mftClusterStart;
            byte[] diskInfo = ReadRaw(0);
            if (diskInfo != null)
            {
                string sistOper = Encoding.UTF8.GetString(new byte[] { diskInfo[3], diskInfo[4], diskInfo[5], diskInfo[6] });
                Console.Write("OS: {0}\n",sistOper);
                bytesxSector = BitConverter.ToUInt16(diskInfo, 11);
                Console.Write("Bytes per Sector: {0}\n",bytesxSector);
                sectorxCluster = Convert.ToUInt16(diskInfo.GetValue(13));
                Console.Write("Sectors per cluster: {0}\n",sectorxCluster);
                mftClusterStart = BitConverter.ToUInt64(diskInfo, 48);
                UInt64 offset = mftClusterStart * sectorxCluster * bytesxSector;
                Console.Write("Starting cluster of the MFT: " + mftClusterStart + " [Offset: 0x" + offset.ToString("X") + "]\n");
                return offset;
            }
            else
            {
                return 0;
            }
        }

        public static void BuscaCadenaRaw(UInt64 mftOffset, string cadeBuscada)
        {
            MFT_ENTRY mftEntry = new MFT_ENTRY(ReadRaw(mftOffset, 1024));
            while (mftEntry.attributeSig != DATA_SIG)
            {
                mftEntry.offsetToAttribute += mftEntry.attributeLength;
                mftEntry.attributeSig = BitConverter.ToUInt32(mftEntry.rawRecord, mftEntry.offsetToAttribute);
                mftEntry.attributeLength = BitConverter.ToInt16(mftEntry.rawRecord, mftEntry.offsetToAttribute + 4);
            }
            GETDATARUNLIST dataRunlist = new GETDATARUNLIST(mftEntry);
            while (dataRunlist.runlist != (byte)(0x00))
            {
                dataRunlist.GETCLUSTERS(mftEntry);
                if (!dataRunlist.isSparse)
                {
                    uint runLength = dataRunlist.runLength;
                    UInt64 offsetBytesMFT = dataRunlist.offsetBytesMFT;
                    byte[] runActualMFT = ReadRaw(offsetBytesMFT, runLength * sectorxCluster * bytesxSector);
                    byte[] entryInfo = new byte[1024];
                    runLength = (runLength * sectorxCluster * bytesxSector) / 1024; //clusters a grupos de 1024 (tamaño de entrada de mft)
                    for (int n = 0; n < runLength; n++)
                    {
                        Array.Copy(runActualMFT, n * 1024, entryInfo, 0, 1024);
                        UInt32 mftSig = BitConverter.ToUInt32(entryInfo, 0);
                        if (mftSig != FILE_SIG) { continue; } //no valid record
                        MFT_ENTRY infoMFT = new MFT_ENTRY(entryInfo);
                        if (!infoMFT.recordValido)
                        { 
                            Console.WriteLine("I omit record {0}: has a wrong fixup value", infoMFT.recordNumber);
                            continue;
                        }
                        string cadenaRawa = Encoding.Default.GetString(entryInfo).Replace("\0", "").ToLower(); //Al menos quito los 0
                        if (((cadenaRawa.Length - (cadenaRawa.ToLower().Replace(cadeBuscada.ToLower(), String.Empty)).Length) / cadeBuscada.Length) > 0)
                        {
                            {
                                if (!refCoincid.Contains(infoMFT.recordNumber))
                                {
                                    refCoincid.Add(infoMFT.recordNumber);
                                }
                            }
                            else
                            {
                                if (!refCoincid.Contains(infoMFT.fileReferenceToBaseFile))
                                {
                                    refCoincid.Add(infoMFT.fileReferenceToBaseFile);
                                }
                            }
                        }
                    }
                    runActualMFT = null;
                    entryInfo = null;
                }
                dataRunlist.NEXTDATARUNLIST(mftEntry.rawRecord[dataRunlist.runlistOffset]);
            }
            Console.WriteLine("Results: {0}", refCoincid.Count);
            GetCoinciDetalles();
        }

        public static void BuscaCadenas(UInt64 mftOffset, List<string> buscadasList = null, string recordMFT = "")
        {
            MFT_ENTRY mftEntry = new MFT_ENTRY(ReadRaw(mftOffset, 1024));
            while (mftEntry.attributeSig != DATA_SIG)
            {
                mftEntry.offsetToAttribute += mftEntry.attributeLength;
                mftEntry.attributeSig = BitConverter.ToUInt32(mftEntry.rawRecord, mftEntry.offsetToAttribute);
                mftEntry.attributeLength = BitConverter.ToInt16(mftEntry.rawRecord, mftEntry.offsetToAttribute + 4);
            }
            GETDATARUNLIST dataRunlist = new GETDATARUNLIST(mftEntry);
            while (dataRunlist.runlist != (byte)(0x00))
            {
                dataRunlist.GETCLUSTERS(mftEntry);
                if (!dataRunlist.isSparse)
                {
                    if (buscadasList.Count != 0)
                    {
                        BuscaCoincidencias(dataRunlist.runLength, dataRunlist.offsetBytesMFT, buscadasList);
                    }
                }
                dataRunlist.NEXTDATARUNLIST(mftEntry.rawRecord[dataRunlist.runlistOffset]);
            }
            Console.WriteLine("Results: {0}", refCoincid.Count);
            GetCoinciDetalles();
        }

        public static byte[] ReadRaw(UInt64 _offset, UInt32 numBytesToRead = 512)
        {
            IntPtr hDisk = PInvokeWin32.CreateFile(origen,
                PInvokeWin32.GENERIC_READ,
                PInvokeWin32.FILE_SHARE_READ | PInvokeWin32.FILE_SHARE_WRITE,
                IntPtr.Zero,
                PInvokeWin32.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDisk.ToInt32() != PInvokeWin32.INVALID_HANDLE_VALUE)
            {
                Int64 newOffset;
                PInvokeWin32.SetFilePointerEx(hDisk, _offset, out newOffset, PInvokeWin32.FILE_BEGIN);
                UInt32 numBytesRead;
                byte[] buffer = new byte[numBytesToRead];
                if (PInvokeWin32.ReadFile(hDisk, buffer, numBytesToRead, out numBytesRead, IntPtr.Zero))
                {
                    PInvokeWin32.CloseHandle(hDisk);
                    return buffer;
                }
                else
                {
                    Console.WriteLine("Invalid disk: {0}", origen);
                    return null;
                }
            }
            else
            {
                Console.WriteLine("Invalid disk: {0}", origen);
                return null;
            }
        }

        public static void MakeSoloMFTDict(UInt64 mftOffset)
        {
            MFT_ENTRY mftEntry = new MFT_ENTRY(ReadRaw(mftOffset, 1024));
            while (mftEntry.attributeSig != DATA_SIG)
            {
                mftEntry.offsetToAttribute += mftEntry.attributeLength;
                mftEntry.attributeSig = BitConverter.ToUInt32(mftEntry.rawRecord, mftEntry.offsetToAttribute);
                mftEntry.attributeLength = BitConverter.ToInt16(mftEntry.rawRecord, mftEntry.offsetToAttribute + 4);
            }
            GETDATARUNLIST dataRunlist = new GETDATARUNLIST(mftEntry);
            while (dataRunlist.runlist != (byte)(0x00))
            {
                dataRunlist.GETCLUSTERS(mftEntry);
                if (!dataRunlist.isSparse)
                {
                    uint runLength_ = dataRunlist.runLength;
                    byte[] runActualMFT = ReadRaw(dataRunlist.offsetBytesMFT, runLength_ * sectorxCluster * bytesxSector);
                    runLength_ = (runLength_ * sectorxCluster * bytesxSector) / 1024; //clusters a grupos de 1024 (tamaño de entrada de mft)
                    byte[] content = new byte[1024];
                    for (int n = 0; n < runLength_; n++)
                    {
                        string nombre = " ";
                        UInt32 parentDirectory = 0;
                        Array.Copy(runActualMFT, n * 1024, content, 0, 1024);
                        UInt32 mftSig = BitConverter.ToUInt32(content, 0);
                        if (mftSig != FILE_SIG) { continue; } //no valid record
                        MFT_ENTRY infoMFT = new MFT_ENTRY(content);
                        infoMFT.MFT_NEXT_ATTRIBUTE();
                        while (infoMFT.attributeSig != END_RECORD_SIG)
                        {
                            if (infoMFT.attributeSig == FN_SIG)
                            {
                                infoMFT.MFT_NEXT_ATTRIBUTE_VALIDO();
                                int datesOffset = infoMFT.offsetToAttribute + infoMFT.attributeContentOffset + 8;
                                int fnNameLen = infoMFT.rawRecord[datesOffset + 56];
                                if ((fnNameLen > nombre.Length) | (nombre == " "))
                                {
                                    nombre = Encoding.Unicode.GetString(infoMFT.rawRecord, datesOffset + 58, fnNameLen * 2);
                                }
                                parentDirectory = BitConverter.ToUInt32(infoMFT.rawRecord, infoMFT.offsetToAttribute + infoMFT.attributeContentOffset);
                            }
                            infoMFT.MFT_NEXT_ATTRIBUTE();
                        }
                        if (infoMFT.fileReferenceToBaseFile == 0)
                        {
                            if (GetPath.soloMFTDictOffsets.ContainsKey(infoMFT.recordNumber))
                            {
                                string nNombre = GetPath.soloMFTDictOffsets[infoMFT.recordNumber].Name.Length < nombre.Length ? nombre : GetPath.soloMFTDictOffsets[infoMFT.recordNumber].Name;
                                UInt32 nparentDirectory = parentDirectory == 0 ? GetPath.soloMFTDictOffsets[infoMFT.recordNumber].ParentFrn : 0;
                                GetPath.FileNameAndParentFrn actualizar = new GetPath.FileNameAndParentFrn(nNombre, parentDirectory, dataRunlist.offsetBytesMFT + Convert.ToUInt64(1024 * n));
                                GetPath.soloMFTDictOffsets.Remove(infoMFT.recordNumber);
                                GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, actualizar);

                            }
                            else
                            {
                                GetPath.FileNameAndParentFrn f = new GetPath.FileNameAndParentFrn(nombre, parentDirectory, dataRunlist.offsetBytesMFT + Convert.ToUInt64(1024 * n));
                                GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, f);
                            }
                        }
                        else
                        {
                            if (listaRecordHijos.ContainsKey(infoMFT.fileReferenceToBaseFile))
                            {
                                listaRecordHijos[infoMFT.fileReferenceToBaseFile].Add(infoMFT.recordNumber);
                            }
                            else
                            {
                                listaRecordHijos.Add(infoMFT.fileReferenceToBaseFile, new List<UInt32> { infoMFT.recordNumber });
                            }
                            GetPath.FileNameAndParentFrn f = new GetPath.FileNameAndParentFrn("Metadata/System File", 1, dataRunlist.offsetBytesMFT + Convert.ToUInt64(1024 * n));
                            GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, f);
                            if (GetPath.soloMFTDictOffsets.ContainsKey(infoMFT.fileReferenceToBaseFile))
                            {
                                string nNombre = GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].Name.Length < nombre.Length ? nombre : GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].Name;
                                UInt64 nOffset = GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].RecordOffset != 0 ? GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].RecordOffset : 0;
                                UInt32 nparentDirectory = parentDirectory == 0 ? GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].ParentFrn : parentDirectory;
                                GetPath.FileNameAndParentFrn actualizar = new GetPath.FileNameAndParentFrn(nNombre, nparentDirectory, nOffset);
                                GetPath.soloMFTDictOffsets.Remove(infoMFT.fileReferenceToBaseFile);
                                GetPath.soloMFTDictOffsets.Add(infoMFT.fileReferenceToBaseFile, actualizar);
                               
                            }
                            else
                            {
                                GetPath.FileNameAndParentFrn actualizar = new GetPath.FileNameAndParentFrn(nombre, parentDirectory, 0);
                                GetPath.soloMFTDictOffsets.Add(infoMFT.fileReferenceToBaseFile, actualizar);
                            }
                        }
                    }
                    runActualMFT = null;
                }
                dataRunlist.NEXTDATARUNLIST(mftEntry.rawRecord[dataRunlist.runlistOffset]);
            }
            GetPath.soloMFTDictOffsets.Remove(5);
            GetPath.FileNameAndParentFrn actualizarRoot = new GetPath.FileNameAndParentFrn(origen + Path.DirectorySeparatorChar, 0, 0);
            GetPath.soloMFTDictOffsets.Add(5, actualizarRoot);
            Console.WriteLine("\n{0} records in the MFT.", GetPath.soloMFTDictOffsets.Count.ToString("N0"));
        }

        public static void BuscaMFTRecord(string referenceBuscada)
        {
            UInt64 llevoCopiado = 0;
            string nombreArch;
            char[] delimiters = new char[] { ':', '-' };
            string[] referencePartes = referenceBuscada.Split(delimiters);
            UInt16 attIDBuscado = Convert.ToUInt16(referencePartes[2], 10);
            nombreArch = "[" + referenceBuscada.Replace(":", "-") + "]";
            UInt32 mftRefBuscada = Convert.ToUInt32(referencePartes[0], 10);
            try
            {
                GetPath.FileNameAndParentFrn localizado = GetPath.soloMFTDictOffsets[mftRefBuscada];
                byte[] refRecord = ReadRaw(localizado.RecordOffset, 1024);
                nombreArch = nombreArch + "-" + localizado.Name;
                MFT_ENTRY infoRecord = new MFT_ENTRY(refRecord);
                if (infoRecord.valFileFlags == 1 || infoRecord.valFileFlags == 5 || infoRecord.valFileFlags == 0)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE();
                    while (infoRecord.attributeSig != END_RECORD_SIG)
                    {
                        if (infoRecord.attributeSig == AL_SIG) //Busco el DATA en la lista
                        {
                            infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                            if (infoRecord.attributeIsResident == 0)
                            {
                                Int16 prevAttributeLength = infoRecord.attributeLength;
                                Int32 prevOffsetToAttribute = infoRecord.offsetToAttribute;
                                infoRecord.attributeLength = infoRecord.attributeContentOffset;
                                processAttrListParaCopia(infoRecord, Convert.ToInt32(infoRecord.attributeContentLength), attIDBuscado, ref nombreArch);
                                infoRecord.attributeLength = prevAttributeLength;
                                infoRecord.attributeSig = 0x20;
                                infoRecord.offsetToAttribute = prevOffsetToAttribute;
                            }
                            else // NO RESIDENTE
                            {
                                MFT_ENTRY attListNoResident = infoRecord;
                                GETDATARUNLIST dataRunlist = new GETDATARUNLIST(attListNoResident);
                                byte[] prevRawRecord = infoRecord.rawRecord;
                                Int16 prevAttributeLength = infoRecord.attributeLength;
                                Int32 prevOffsetToAttribute = infoRecord.offsetToAttribute;
                                Int32 contentLength = BitConverter.ToInt32(infoRecord.rawRecord, infoRecord.offsetToAttribute + 48);
                                while (dataRunlist.runlist != (byte)(0x00))
                                {
                                    dataRunlist.GETCLUSTERS(attListNoResident);
                                    if (!dataRunlist.isSparse)
                                    {
                                        uint runLength_ = dataRunlist.runLength;
                                        infoRecord.rawRecord = ReadRaw(dataRunlist.offsetBytesMFT, runLength_ * sectorxCluster * bytesxSector);
                                        infoRecord.attributeLength = 0;
                                        infoRecord.offsetToAttribute = 0;
                                        processAttrListParaCopia(infoRecord, contentLength, attIDBuscado, ref nombreArch);
                                    }
                                    dataRunlist.NEXTDATARUNLIST(attListNoResident.rawRecord[dataRunlist.runlistOffset]);
                                }
                                infoRecord.rawRecord = prevRawRecord;
                                infoRecord.attributeLength = prevAttributeLength;
                                infoRecord.attributeSig = 0x20;
                                infoRecord.offsetToAttribute = prevOffsetToAttribute;

                            }
                            Console.WriteLine(" to file {0}", nombreArch);
                            if (copiado)
                            {
                                if (diccDatosCopia[infoRecord.attrListStartVCN].isResident) //Si el contenido era residente lo escribo
                                {
                                    File.WriteAllBytes(nombreArch, diccDatosCopia[infoRecord.attrListStartVCN].contentResident);
                                }
                                else // PARSEO EL diccDatosCopia y ya tengo el DATA copiado
                                {
                                    UInt64 sizeArchivo = 0;
                                    Int32 elementos = diccDatosCopia.Count;
                                    int n = 0;
                                    foreach (KeyValuePair<UInt64, dataParaCopia> datarun in diccDatosCopia)
                                    {
                                        n += 1;
                                        if (sizeArchivo < datarun.Value.sizeCopiar) { sizeArchivo = datarun.Value.sizeCopiar; }
                                        GetPath.FileNameAndParentFrn localizaRecordDatarun = GetPath.soloMFTDictOffsets[datarun.Value.mftFRN];
                                        byte[] recordDatarun = ReadRaw(localizaRecordDatarun.RecordOffset, 1024);
                                        MFT_ENTRY infoRecordDatarun = new MFT_ENTRY(recordDatarun);
                                        infoRecordDatarun.offsetToAttribute = datarun.Value.offsetHastaData;
                                        infoRecordDatarun.attributeSig = DATA_SIG;
                                        infoRecordDatarun.attributeLength = BitConverter.ToInt16(infoRecordDatarun.rawRecord, infoRecordDatarun.offsetToAttribute + 4);
                                        if (infoRecordDatarun.rawRecord[infoRecordDatarun.offsetToAttribute + 8] == 1)
                                        {
                                            copiaNoResidentDATA(infoRecordDatarun, n, elementos, sizeArchivo, nombreArch, ref llevoCopiado);
                                        }
                                        else
                                        {
                                            infoRecordDatarun.GET_RESIDENT_DATA();
                                            byte[] dataResidente = new byte[infoRecordDatarun.attributeContentLength];
                                            Array.Copy(infoRecordDatarun.rawRecord, infoRecordDatarun.offsetToAttribute + infoRecordDatarun.attributeContentOffset, dataResidente, 0, infoRecordDatarun.attributeContentLength);
                                            File.WriteAllBytes(nombreArch, dataResidente);
                                        }
                                        recordDatarun = null;
                                    }
                                }
                            }
                            break; //Salgo del WHILE porque he terminado la copia
                        }
                        else if (infoRecord.attributeSig == DATA_SIG) //No hay lista ergo busco los datas
                        {
                            infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                            if (infoRecord.attributeID == attIDBuscado)
                            {
                                if (infoRecord.attributeNameLength != 0) //el nombre de los ADS
                                {
                                    string nameAtt = Encoding.Unicode.GetString(infoRecord.rawRecord, infoRecord.offsetToAttribute + infoRecord.attributeNameOffset, infoRecord.attributeNameLength * 2);
                                    nombreArch = nombreArch + "@" + nameAtt + ".dat";
                                }
                                else
                                {
                                    nombreArch = nombreArch + ".dat";
                                }
                                Console.WriteLine(" to file {0}", nombreArch);
                                if (infoRecord.attributeIsResident == 1)
                                {
                                    UInt64 sizeArchivo = BitConverter.ToUInt64(infoRecord.rawRecord, infoRecord.offsetToAttribute + 48);
                                    copiaNoResidentDATA(infoRecord, 0, 0, sizeArchivo, nombreArch, ref llevoCopiado);
                                    copiado = true;
                                    break;
                                }
                                else
                                {
                                    infoRecord.GET_RESIDENT_DATA();
                                    byte[] dataResidente = new byte[infoRecord.attributeContentLength];
                                    Array.Copy(infoRecord.rawRecord, infoRecord.offsetToAttribute + infoRecord.attributeContentOffset, dataResidente, 0, infoRecord.attributeContentLength);
                                    File.WriteAllBytes(nombreArch, dataResidente);
                                    copiado = true;
                                    break;
                                }
                            }
                        }
                        infoRecord.MFT_NEXT_ATTRIBUTE();
                    }
                    if (!copiado) { Console.WriteLine("\nReference {0} not found", referenceBuscada); }
                    refRecord = null;
                }
                else
                {
                    switch (infoRecord.valFileFlags)
                    {
                        case 2:
                            Console.WriteLine("\nNot suported: deleted directory.");
                            break;
                        case 3:
                            Console.WriteLine("\nNot suported: directory.");
                            break;
                        default:
                            Console.WriteLine("\nNot suported.");
                            break;
                    }
                }
            }
            catch
            {
                Console.WriteLine("\nUnable to make the copy. Please check the reference.");
            }
        }

        public static void copiaNoResidentDATA(MFT_ENTRY infoRecord, Int32 n, Int32 elementos, UInt64 sizeArchivo, string nombreArch, ref UInt64 llevoCopiado)
        {
            uint bytesxCluster = sectorxCluster * bytesxSector;
            Int32 sizeCachos = 64000;
            GETDATARUNLIST dataRunlist = new GETDATARUNLIST(infoRecord);
            while (dataRunlist.runlist != (byte)(0x00))
            {
                dataRunlist.GETCLUSTERS(infoRecord);
                Console.WriteLine("Writing run length: {0} [Real file size: {1} bytes].", (dataRunlist.runLength * bytesxCluster).ToString("N0"), sizeArchivo.ToString("N0"));
                UInt64 count = 0;
                if (!dataRunlist.isSparse)
                {
                    UInt32 trozos = Convert.ToUInt32(sizeCachos) * bytesxCluster;
                    if ((sizeArchivo - llevoCopiado) >= (dataRunlist.runLength * bytesxCluster))
                    {
                        Int64 aux = Convert.ToInt64(dataRunlist.runLength) - sizeCachos;
                        llevoCopiado = llevoCopiado + Convert.ToUInt64(dataRunlist.runLength * bytesxCluster);
                        UInt64 offsetParcial = 0;
                        byte[] buscados = new byte[sizeCachos * bytesxCluster];
                        while (aux > 0) // Copia de archivos enormes en trozos de 250 Mb
                        {
                            buscados = ReadRaw(dataRunlist.offsetBytesMFT + count * Convert.ToUInt64(trozos), trozos);
                            offsetParcial += Convert.ToUInt64(trozos);
                            if (File.Exists(nombreArch))
                            {
                                using (var stream = new FileStream(nombreArch, FileMode.Append))
                                {
                                    stream.Write(buscados, 0, buscados.Length);
                                    stream.Close();
                                }
                            }
                            else
                            {
                                File.WriteAllBytes(nombreArch, buscados);
                            }
                            aux = aux - sizeCachos;
                            dataRunlist.runLength = dataRunlist.runLength - Convert.ToUInt32(sizeCachos);
                            count += 1;
                            buscados = null;
                            if (count == 10) { GC.Collect(2); } //Experimentalmente parece que acumula al llegar a esta repeticion
                        }
                        buscados = null;
                        byte[] buscadoUlt = ReadRaw(dataRunlist.offsetBytesMFT + offsetParcial, dataRunlist.runLength * bytesxCluster);
                        if (File.Exists(nombreArch))
                        {
                            using (var stream = new FileStream(nombreArch, FileMode.Append))
                            {
                                stream.Write(buscadoUlt, 0, buscadoUlt.Length);
                                stream.Close();
                            }
                        }
                        else
                        {
                            File.WriteAllBytes(nombreArch, buscadoUlt);
                        }
                    }
                    else //Voy a escribir  solo lo que diga el tamaño y no lo que diga el runlist porque hay sparse que no es del tipo 0X
                    { //Lo que queda es menor que este runlist luego hay sparse o el último cluster no está lleno
                        UInt64 pendiente = sizeArchivo - llevoCopiado;
                        UInt64 offsetParcial = 0;
                        byte[] buscados = new byte[sizeCachos * bytesxCluster];
                        while (trozos < pendiente) // Copia de archivos enormes en trozos de 250 Mb
                        {
                            buscados = ReadRaw(dataRunlist.offsetBytesMFT + count * Convert.ToUInt64(trozos), trozos);
                            offsetParcial += Convert.ToUInt64(trozos);
                            if (File.Exists(nombreArch))
                            {
                                using (var stream = new FileStream(nombreArch, FileMode.Append))
                                {
                                    stream.Write(buscados, 0, buscados.Length);
                                    stream.Close();
                                }
                            }
                            else
                            {
                                File.WriteAllBytes(nombreArch, buscados);
                            }
                            pendiente = pendiente - trozos;
                            count += 1;
                            buscados = null;
                            if (count == 10) { GC.Collect(2); } //Experimentalmente parece que acumula al llegar a esta repeticion
                        }
                        buscados = null;
                        UInt64 remanente = pendiente % bytesxCluster;
                        UInt64 ultimoClusterOffset = dataRunlist.offsetBytesMFT + count * Convert.ToUInt64(trozos) + pendiente - remanente;
                        byte[] buscadoUltCluster = ReadRaw(dataRunlist.offsetBytesMFT + count * Convert.ToUInt64(trozos), Convert.ToUInt32(pendiente - remanente));
                        if (File.Exists(nombreArch))
                        {
                            using (var stream = new FileStream(nombreArch, FileMode.Append))
                            {
                                stream.Write(buscadoUltCluster, 0, buscadoUltCluster.Length);
                                stream.Close();
                            }
                        }
                        else
                        {
                            File.WriteAllBytes(nombreArch, buscadoUltCluster);
                        }
                        byte[] buscadoUltClusterResto = ReadRaw(ultimoClusterOffset, bytesxCluster);
                        if (sizeArchivo != 0)
                        {
                            Array.Resize(ref buscadoUltClusterResto, Convert.ToInt32(remanente));
                        }
                        if (File.Exists(nombreArch))
                        {
                            using (var stream = new FileStream(nombreArch, FileMode.Append))
                            {
                                stream.Write(buscadoUltClusterResto, 0, buscadoUltClusterResto.Length);
                                stream.Close();
                            }
                        }
                        else
                        {
                            File.WriteAllBytes(nombreArch, buscadoUltClusterResto);
                        }
                    }
                }
                else { Console.WriteLine("Sparse chunk not saved: {0} bytes.", dataRunlist.runLength.ToString("N0")); }
                dataRunlist.NEXTDATARUNLIST(infoRecord.rawRecord[dataRunlist.runlistOffset]);
            }
        }

        public static void processAttrListParaCopia(MFT_ENTRY infoRecord, Int32 contentLength, UInt16 attIDBuscado, ref string nombreArch)
        {
            int cuentaLengthRecorrido = 0;
            while (cuentaLengthRecorrido < contentLength)
            {
                infoRecord.MFT_NEXT_ATTRIBUTE();
                if (infoRecord.attributeSig == END_RECORD_SIG) //Hasta Data segun MFT_NEXT_ATTRIBUTE
                {
                    break;
                }
                else if (infoRecord.attributeSig == 0x80)
                {
                    cuentaLengthRecorrido += Convert.ToInt32(infoRecord.attributeLength);
                    UInt16 attID = BitConverter.ToUInt16(infoRecord.rawRecord, infoRecord.offsetToAttribute + 24);
                    if (attID == attIDBuscado)
                    {
                        infoRecord.attrListStartVCN = BitConverter.ToUInt64(infoRecord.rawRecord, infoRecord.offsetToAttribute + 8);
                        UInt32 attRecordNumber = BitConverter.ToUInt32(infoRecord.rawRecord, infoRecord.offsetToAttribute + 16);
                        diccDatosCopia[infoRecord.attrListStartVCN] = new dataParaCopia(attRecordNumber); //Cargo el mftFRN
                        Int16 intprevAttributeLength = infoRecord.attributeLength;
                        Int32 intprevOffsetToAttribute = infoRecord.offsetToAttribute;
                        GetPath.FileNameAndParentFrn localiza = GetPath.soloMFTDictOffsets[attRecordNumber];
                        byte[] referenceRecord = ReadRaw(localiza.RecordOffset, 1024);
                        MFT_ENTRY entryData = new MFT_ENTRY(referenceRecord);
                        entryData.MFT_NEXT_ATTRIBUTE();
                        while (entryData.attributeSig != END_RECORD_SIG)
                        {
                            if (entryData.attributeSig == DATA_SIG)
                            {
                                entryData.MFT_NEXT_ATTRIBUTE_VALIDO();
                                if (entryData.attributeID == attIDBuscado)
                                {
                                    copiado = true;
                                    UInt64 fileSize;
                                    if (entryData.attributeNameLength != 0) //el nombre de los ADS
                                    {
                                        string nameAtt = Encoding.Unicode.GetString(entryData.rawRecord, entryData.offsetToAttribute + entryData.attributeNameOffset, entryData.attributeNameLength * 2);
                                        nombreArch = nombreArch + "@" + nameAtt + ".dat";
                                    }
                                    else
                                    {
                                        nombreArch = nombreArch + ".dat";
                                    }
                                    if (entryData.attributeIsResident == 0)
                                    {
                                        fileSize = Convert.ToUInt64(entryData.attributeContentLength);
                                        entryData.GET_RESIDENT_DATA();
                                        Array.Resize(ref diccDatosCopia[infoRecord.attrListStartVCN].contentResident, Convert.ToInt32(entryData.attributeContentLength));
                                        Array.Copy(entryData.rawRecord, entryData.offsetToAttribute + entryData.attributeContentOffset, diccDatosCopia[infoRecord.attrListStartVCN].contentResident, 0, entryData.attributeContentLength);
                                        diccDatosCopia[infoRecord.attrListStartVCN].isResident = true;
                                    }
                                    else
                                    {
                                        byte[] tempSize = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                                        Array.Copy(entryData.rawRecord, entryData.offsetToAttribute + 48, tempSize, 0, 8);
                                        fileSize = BitConverter.ToUInt64(tempSize, 0);
                                        diccDatosCopia[infoRecord.attrListStartVCN].offsetHastaData = entryData.offsetToAttribute;
                                    }
                                    entryData.realFileSize = fileSize;
                                }
                            }
                            entryData.MFT_NEXT_ATTRIBUTE();
                        }
                        if (diccDatosCopia[infoRecord.attrListStartVCN].sizeCopiar < entryData.realFileSize)
                        {
                            diccDatosCopia[infoRecord.attrListStartVCN].sizeCopiar = entryData.realFileSize;
                        }
                        infoRecord.attributeLength = intprevAttributeLength;
                        infoRecord.attributeSig = 0x30;
                        infoRecord.offsetToAttribute = intprevOffsetToAttribute;
                        referenceRecord = null;
                    }
                }
                else { cuentaLengthRecorrido += Convert.ToInt32(infoRecord.attributeLength); }
            }
        }

        public static void BuscaCoincidencias(uint runLength, UInt64 offsetBytesMFT, List<string> buscadasList)
        {
            byte[] runActualMFT = ReadRaw(offsetBytesMFT, runLength * sectorxCluster * bytesxSector);
            byte[] entryInfo = new byte[1024];
            runLength = (runLength * sectorxCluster * bytesxSector) / 1024; //clusters a grupos de 1024 (tamaño de entrada de mft)
            for (int n = 0; n < runLength; n++)
            {
                Array.Copy(runActualMFT, n * 1024, entryInfo, 0, 1024);
                UInt32 mftSig = BitConverter.ToUInt32(entryInfo, 0);
                if (mftSig != FILE_SIG) { continue; } //no valid record
                MFT_ENTRY infoMFT = new MFT_ENTRY(entryInfo);
                if (!infoMFT.recordValido)
                { 
                    Console.WriteLine("I omit record {0}: has a wrong fixup value", infoMFT.recordNumber);
                    continue;
                }
                BuscaCoincidenciasInfo(infoMFT);
                bool result = false;
                foreach (string nombreBuscado in buscadasList)
                {
                    bool incluyePath = false;
                    string pathBuscado = "";
                    string nombreArchivo = nombreBuscado;
                    string nombPath = "";
                    if (nombreBuscado.LastIndexOf("\\") > 0)
                    {
                        incluyePath = true;
                        pathBuscado = nombreBuscado.Substring(0, nombreBuscado.LastIndexOf("\\")).ToLower();
                        nombreArchivo = nombreBuscado.Substring(nombreBuscado.LastIndexOf("\\") + 1, nombreBuscado.Length - nombreBuscado.LastIndexOf("\\") - 1);
                    }
                    for (int i = 0; i < infoMFT.nombreFN.Count; i++)
                    {
                        if (result) { break; }
                        if (nombreBuscado.EndsWith("?"))
                        {
                            string newNombreBuscado = nombreArchivo.Replace("?", String.Empty);
                            if (infoMFT.nombreFN[i].ToLower() == newNombreBuscado.ToLower())
                            {
                                bool pathCorrecto = false;
                                if (incluyePath)
                                {
                                    nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\\\.\\", String.Empty).ToLower();
                                    if (nombPath.StartsWith(pathBuscado))
                                    {
                                        pathCorrecto = true;
                                    }
                                }
                                else pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde el nombre FN no está en el record base
                                    {
                                        if (!refCoincid.Contains(infoMFT.recordNumber))
                                        {
                                            refCoincid.Add(infoMFT.recordNumber);
                                        }
                                    }
                                    else
                                    {
                                        if (!refCoincid.Contains(infoMFT.fileReferenceToBaseFile))
                                        {
                                            refCoincid.Add(infoMFT.fileReferenceToBaseFile);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (((infoMFT.nombreFN[i].Length - (infoMFT.nombreFN[i].ToLower().Replace(nombreArchivo.ToLower(), String.Empty)).Length) / nombreArchivo.Length) > 0)
                            {
                                bool pathCorrecto = false;
                                if (incluyePath)
                                {
                                    nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\\\.\\", String.Empty).ToLower();
                                    if (nombPath.StartsWith(pathBuscado))
                                    {
                                        pathCorrecto = true;
                                    }
                                }
                                else pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde el nombre FN no está en el record base
                                    {
                                        if (!refCoincid.Contains(infoMFT.recordNumber))
                                        {
                                            refCoincid.Add(infoMFT.recordNumber);
                                        }
                                    }
                                    else
                                    {
                                        if (!refCoincid.Contains(infoMFT.fileReferenceToBaseFile))
                                        {
                                            refCoincid.Add(infoMFT.fileReferenceToBaseFile);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (!result)
                    {
                        foreach (var datas in infoMFT.diccIDDataADSInfo)
                        {
                            if (nombreArchivo.EndsWith("?"))
                            {
                                string newNombreBuscado = nombreArchivo.Replace("?", String.Empty);
                                if (datas.Value.name.ToLower() == newNombreBuscado.ToLower())
                                {
                                    bool pathCorrecto = false;
                                    if (incluyePath)
                                    {
                                        nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\\\.\\", String.Empty).ToLower();
                                        if (nombPath.StartsWith(pathBuscado))
                                        {
                                            pathCorrecto = true;
                                        }
                                    }
                                    else pathCorrecto = true;
                                    if (pathCorrecto)
                                    {
                                        result = true;
                                        if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde el nombre no está en el record base
                                        {
                                            if (!refCoincid.Contains(infoMFT.recordNumber))
                                            {
                                                refCoincid.Add(infoMFT.recordNumber);
                                            }
                                        }
                                        else
                                        {
                                            if (!refCoincid.Contains(infoMFT.fileReferenceToBaseFile))
                                            {
                                                refCoincid.Add(infoMFT.fileReferenceToBaseFile);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (((datas.Value.name.Length - (datas.Value.name.ToLower().Replace(nombreArchivo.ToLower(), String.Empty)).Length) / nombreArchivo.Length) > 0)
                                {
                                    bool pathCorrecto = false;
                                    if (incluyePath)
                                    {
                                        nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\\\.\\", String.Empty).ToLower();
                                        if (nombPath.StartsWith(pathBuscado))
                                        {
                                            pathCorrecto = true;
                                        }
                                    }
                                    else pathCorrecto = true;
                                    if (pathCorrecto)
                                    {
                                        result = true;
                                        if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde el nombre no está en el record base
                                        {
                                            if (!refCoincid.Contains(infoMFT.recordNumber))
                                            {
                                                refCoincid.Add(infoMFT.recordNumber);
                                            }
                                        }
                                        else
                                        {
                                            if (!refCoincid.Contains(infoMFT.fileReferenceToBaseFile))
                                            {
                                                refCoincid.Add(infoMFT.fileReferenceToBaseFile);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            runActualMFT = null;
        }

        public static void GetCoinciDetalles()
        {
            if (CommandLine.Parameters.ContainsKey("ft"))
            {
                Console.WriteLine("\n[MACB],filetime,path,record,size,is_ADS");
            }
            foreach (UInt32 entryCoincid in refCoincid)
            {
                try
                {
                    GetPath.FileNameAndParentFrn localiza = GetPath.soloMFTDictOffsets[entryCoincid];
                    byte[] refRecord = ReadRaw(localiza.RecordOffset, 1024);
                    MFT_ENTRY infoEntryCoincid = new MFT_ENTRY(refRecord);
                    UInt32 baseRef = infoEntryCoincid.fileReferenceToBaseFile;
                    if (baseRef != 0)
                    {
                        localiza = GetPath.soloMFTDictOffsets[baseRef];
                        refRecord = ReadRaw(localiza.RecordOffset, 1024);
                        infoEntryCoincid = new MFT_ENTRY(refRecord);
                    }
                    GetCoinciDetallesInfo(infoEntryCoincid);
                    infoEntryCoincid.MFT_SHOW_DATA();
                    refRecord = null;
                    infoEntryCoincid = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\nRecord {0} not found.", entryCoincid);
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public static void BuscaCoincidenciasInfo(MFT_ENTRY infoRecord)
        {
            infoRecord.MFT_NEXT_ATTRIBUTE();
            while (infoRecord.attributeSig != END_RECORD_SIG)
            {
                if (infoRecord.attributeSig == FN_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    infoRecord.parentDirectoryFN = BitConverter.ToUInt32(infoRecord.rawRecord, infoRecord.offsetToAttribute + infoRecord.attributeContentOffset);
                    int datesOffset = infoRecord.offsetToAttribute + infoRecord.attributeContentOffset + 8;
                    int fnNameLen = infoRecord.rawRecord[datesOffset + 56];
                    infoRecord.nombreFN.Add(Encoding.Unicode.GetString(infoRecord.rawRecord, datesOffset + 58, fnNameLen * 2));
                }
                else if (infoRecord.attributeSig == DATA_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    if (infoRecord.attributeNameLength != 0) //Solo es necesario parsearlo para los ADS,s para buscar sus nombres
                    {
                        byte adsNameLen = infoRecord.rawRecord[infoRecord.offsetToAttribute + 9];
                        UInt16 adsNameOffset = infoRecord.rawRecord[infoRecord.offsetToAttribute + 10];
                        string nombreEncontrado = Encoding.Unicode.GetString(infoRecord.rawRecord, infoRecord.offsetToAttribute + adsNameOffset, adsNameLen * 2);
                        if (!infoRecord.diccIDDataADSInfo.ContainsKey(infoRecord.recordNumber.ToString() + ":128-" + infoRecord.attributeID.ToString()))
                        {
                            infoRecord.diccIDDataADSInfo.Add(infoRecord.recordNumber.ToString() + ":128-" + infoRecord.attributeID.ToString(), new dataADSInfo(nombreEncontrado, 0));
                        }
                    }
                    else
                    {
                        infoRecord.dataBase = infoRecord.recordNumber.ToString() + ":128-" + infoRecord.attributeID.ToString();
                    }
                }
                infoRecord.MFT_NEXT_ATTRIBUTE();
            }
        }

        public static void GetCoinciDetallesInfo(MFT_ENTRY infoRecord)
        {
            infoRecord.MFT_NEXT_ATTRIBUTE();
            while (infoRecord.attributeSig != END_RECORD_SIG)
            {
                if (infoRecord.attributeSig == SI_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    info_SI(infoRecord);
                }
                else if (infoRecord.attributeSig == AL_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    info_AL(infoRecord);
                }
                else if (infoRecord.attributeSig == FN_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    info_FN(infoRecord);
                }
                else if (infoRecord.attributeSig == DATA_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    info_DATA(infoRecord);
                }
                infoRecord.MFT_NEXT_ATTRIBUTE();
            }

        }

        public class MFT_ENTRY
        {
            public bool recordValido = true;
            public byte[] rawRecord = new byte[1024];
            public UInt32 mftSig;
            public byte[] mftSeqN = new byte[2];
            public Int32 offsetToAttribute;
            public Int16 valFileFlags;
            public string fileFlags;
            public Int32 usedSize;
            public UInt32 fileReferenceToBaseFile;
            public UInt32 recordNumber;
            public UInt32 attributeSig;
            public Int16 attributeLength = 0;
            public byte attributeIsResident;
            public Int16 attributeNameLength;
            public Int16 attributeNameOffset;
            public UInt16 attributeID;
            public Int16 attributeContentLength;
            public Int16 attributeContentOffset;
            public string dateCreated_SI;
            public string dateModificado_SI;
            public string dateMFTModif_SI;
            public string dateAccessed_SI;
            public UInt32 parentDirectoryFN;
            public List<string> dateCreated_FN = new List<string>();
            public List<string> dateModificado_FN = new List<string>();
            public List<string> dateMFTModif_FN = new List<string>();
            public List<string> dateAccessed_FN = new List<string>();
            public List<string> nombreFN = new List<string>();
            public UInt64 realFileSize = 0;
            public UInt64 diskFileSize = 0;
            public string dataBase = "";
            public Dictionary<string,dataADSInfo> diccIDDataADSInfo = new Dictionary<string, dataADSInfo>();
            public UInt64 attrListStartVCN;
            
            private const byte SIG_OFFSET = 0;
            private const byte FIXUP_ARRAY_OFFSET = 4;
            private const byte FIXUP_ARRAY_LENGTH_OFFSET = 6;
            private const byte SEQ_N_OFFSET = 16;
            private const byte FA_OFFSET = 20;
            public const byte FF_OFFSET = 22;
            private const byte US_OFFSET = 24;
            private const byte FRTBF_OFFSET = 32;
            private const byte RN_OFFSET = 44;
            private const byte A_LE_OFFSET = 4;
            private const byte A_IR_OFFSET = 8;
            private const byte A_NL_OFFSET = 9;
            private const byte A_NO_OFFSET = 10;
            private const byte A_ID_OFFSET = 14;
            private const byte A_COL_OFFSET = 16;
            private const byte A_COO_OFFSET = 20;

            public MFT_ENTRY(byte[] p)
            {
                UInt32 mftSig = BitConverter.ToUInt32(rawRecord, 0);
                rawRecord = p;
                byte[] fixupSeqArray = new byte[BitConverter.ToUInt16(rawRecord, FIXUP_ARRAY_LENGTH_OFFSET) * 2];
                Array.Copy(rawRecord, BitConverter.ToUInt16(rawRecord, FIXUP_ARRAY_OFFSET), fixupSeqArray, 0, BitConverter.ToUInt16(rawRecord, FIXUP_ARRAY_LENGTH_OFFSET) * 2);
                Array.Copy(rawRecord, SEQ_N_OFFSET, mftSeqN, 0, 2);
                offsetToAttribute = BitConverter.ToInt16(rawRecord, FA_OFFSET);
                valFileFlags = BitConverter.ToInt16(rawRecord, MFT_ENTRY.FF_OFFSET);
                switch (valFileFlags)
                {
                    case 0:
                        fileFlags = "[Deleted file]";
                        break;
                    case 1:
                        fileFlags = "[File]";
                        break;
                    case 2:
                        fileFlags = "[Deleted directory]";
                        break;
                    case 3:
                        fileFlags = "[Directory]";
                        break;
                    case 4:
                        fileFlags = "[Deactivated $UsnJrnl]";
                        break;
                    case 5:
                        fileFlags = "[Active $UsnJrnl]";
                        break;
                    case 13: //$Reparse-$ObjId-$Quota...
                        fileFlags = "[System]";
                        break;
                    default:
                        fileFlags = "[Unknown]";
                        break;
                }
                usedSize = BitConverter.ToInt32(rawRecord, US_OFFSET);
                if (BitConverter.ToUInt16(fixupSeqArray, 0) == BitConverter.ToUInt16(rawRecord, 510))
                {
                    Array.Copy(fixupSeqArray, 2, rawRecord, 510, 2);
                }
                else { recordValido = false; }
                if (usedSize >= 0x200)
                {
                    if (BitConverter.ToUInt16(fixupSeqArray, 0) == BitConverter.ToUInt16(rawRecord, 1022))
                    {
                        Array.Copy(fixupSeqArray, 4, rawRecord, 1022, 2);
                    }
                    else { recordValido = false; }
                }
                if (recordValido) { Array.Resize(ref rawRecord, usedSize); }
                fileReferenceToBaseFile = BitConverter.ToUInt32(rawRecord, FRTBF_OFFSET);
                recordNumber = BitConverter.ToUInt32(rawRecord, RN_OFFSET);
            }

            public void MFT_NEXT_ATTRIBUTE()
            {
                offsetToAttribute += attributeLength;
                attributeSig = BitConverter.ToUInt32(rawRecord, offsetToAttribute);
                if (attributeSig <= DATA_SIG)
                {
                    attributeLength = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_LE_OFFSET);
                }
                else
                {
                    attributeSig = 0xFFFFFFFF;
                }
            }

            public void MFT_NEXT_ATTRIBUTE_VALIDO()
            {
                attributeIsResident = rawRecord[offsetToAttribute + A_IR_OFFSET];
                attributeNameLength = rawRecord[offsetToAttribute + A_NL_OFFSET];
                attributeNameOffset = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_NO_OFFSET);
                attributeID = BitConverter.ToUInt16(rawRecord, offsetToAttribute + A_ID_OFFSET);
                attributeContentLength = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_COL_OFFSET);
                attributeContentOffset = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_COO_OFFSET);
            }

            public void MFT_SHOW_DATA()
            {
                if (!CommandLine.Parameters.ContainsKey("ft"))
                {
                    if (listaRecordHijos.ContainsKey(recordNumber))
                    {
                        Console.Write("\nRecord: {0}", recordNumber.ToString());
                        Console.Write(" [Attribute List points to records numbers:");
                        foreach (var rec in listaRecordHijos[recordNumber])
                        {
                            Console.Write(" {0}", rec.ToString());
                        }
                        Console.WriteLine("]");
                    }
                    else
                    {
                        Console.WriteLine("\nRecord: {0}", recordNumber.ToString());
                    }
                }
                string longName = "";
                for (int i = 0; i < nombreFN.Count; i++)
                {
                    nombreFN[i] = Path.Combine(GetPath.soloMFTGetFullyQualifiedPath(parentDirectoryFN), nombreFN[i]);
                    if (!CommandLine.Parameters.ContainsKey("ft"))
                    {
                        Console.WriteLine(fileFlags + nombreFN[i]);
                    }
                    longName = longName.Length < nombreFN[i].Length ? nombreFN[i] : longName;
                }
                if (!CommandLine.Parameters.ContainsKey("ft"))
                {
                    Console.WriteLine("SI[MACB]: {0}   {1}   {2}   {3}", dateModificado_SI, dateAccessed_SI, dateMFTModif_SI, dateCreated_SI);
                }
                else
                {
                    Console.WriteLine("SI[M...],{0},{1},{2},{3},", dateModificado_SI, longName, recordNumber, realFileSize.ToString("N0"));
                    Console.WriteLine("SI[.A..],{0},{1},{2},{3},", dateAccessed_SI, longName, recordNumber, realFileSize.ToString("N0"));
                    Console.WriteLine("SI[..C.],{0},{1},{2},{3},", dateMFTModif_SI, longName, recordNumber, realFileSize.ToString("N0"));
                    Console.WriteLine("SI[...B],{0},{1},{2},{3},", dateCreated_SI, longName, recordNumber, realFileSize.ToString("N0"));
                }
                if (!CommandLine.Parameters.ContainsKey("ft"))
                {
                    for (int i = 0; i < dateCreated_FN.Count; i++)
                    {
                        Console.WriteLine("FN[MACB]: {0}   {1}   {2}   {3}", dateModificado_FN[i], dateAccessed_FN[i], dateMFTModif_FN[i], dateCreated_FN[i]);
                    }
                    Console.WriteLine("Reference: {0} [Size: {1} bytes|| Size on disk: {2} bytes]", dataBase, realFileSize.ToString("N0"), diskFileSize.ToString("N0"));
                }
                else
                {
                    for (int i = 0; i < dateCreated_FN.Count; i++)
                    {
                        Console.WriteLine("FN[M...],{0},{1},{2},{3},", dateModificado_FN[i], nombreFN[i], recordNumber, realFileSize.ToString("N0"));
                        Console.WriteLine("FN[.A..],{0},{1},{2},{3},", dateAccessed_FN[i], nombreFN[i], recordNumber, realFileSize.ToString("N0"));
                        Console.WriteLine("FN[..C.],{0},{1},{2},{3},", dateMFTModif_FN[i], nombreFN[i], recordNumber, realFileSize.ToString("N0"));
                        Console.WriteLine("FN[...B],{0},{1},{2},{3},", dateCreated_FN[i], nombreFN[i], recordNumber, realFileSize.ToString("N0"));
                    }
                }
                if (CommandLine.Parameters.ContainsKey("fx") & (fileFlags == "[File]"))
                {
                    using (StreamWriter outfile = new StreamWriter(nameOut, true))
                    {
                        outfile.Write(dataBase + "\t" + longName + "\n");
                    }
                }
                foreach (var datas in diccIDDataADSInfo)
                {
                    if (!CommandLine.Parameters.ContainsKey("ft"))
                    {
                        Console.WriteLine("[ADS] Name: {0} [Reference: {1} || Size: {2} bytes]", datas.Value.name, datas.Key, datas.Value.size.ToString("N0"));
                    }
                    else
                    {
                        Console.WriteLine("SI[M...],{0},{1}:{2},{3},{4},{5}", dateModificado_SI, longName, datas.Value.name, recordNumber, datas.Value.size.ToString("N0"), "Yes");
                        Console.WriteLine("SI[.A..],{0},{1}:{2},{3},{4},{5}", dateAccessed_SI, longName, datas.Value.name, recordNumber, datas.Value.size.ToString("N0"), "Yes");
                        Console.WriteLine("SI[..C.],{0},{1}:{2},{3},{4},{5}", dateMFTModif_SI, longName, datas.Value.name, recordNumber, datas.Value.size.ToString("N0"), "Yes");
                        Console.WriteLine("SI[...B],{0},{1}:{2},{3},{4},{5}", dateCreated_SI, longName, datas.Value.name, recordNumber, datas.Value.size.ToString("N0"), "Yes");
                    }
                    if (CommandLine.Parameters.ContainsKey("fx"))
                    {
                        using (StreamWriter outfile = new StreamWriter(nameOut, true))
                        {
                            outfile.Write(datas.Key + "\t" + longName + ":" + datas.Value.name + "\n");
                        }
                    }
                }
            }

            public void GET_RESIDENT_DATA()
            {
                attributeContentLength = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_COL_OFFSET);
                attributeContentOffset = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_COO_OFFSET);
            }
        }

        public class dataParaCopia
        {
            public UInt64 sizeCopiar;
            public Int32 offsetHastaData;
            public UInt32 mftFRN;
            public bool isResident = false;
            public byte[] contentResident = new byte[1024];

            public dataParaCopia(UInt32 mftFRN_)
            {
                mftFRN = mftFRN_;
            }
        }

        public class dataADSInfo
        {
            public string name;
            public UInt64 size;

            public dataADSInfo(string name_, UInt64 size_)
            {
                name = name_;
                size = size_;
            }
        }

        public class GETDATARUNLIST
        {
            public int runlistOffset;
            public byte runlist;
            public int runlistLowNibble;
            public int runlistHighNibble;
            public uint runLength;
            public UInt64 offsetBytesMFT;
            byte[] runLengthComplt = new byte[4];
            byte[] runOffsetComplt = new byte[4];
            byte[] arrayPositivos = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            byte[] arrayNegativos = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            public bool isSparse = false;

            public GETDATARUNLIST(MFT_ENTRY recordActual)
            {
                runlistOffset = recordActual.offsetToAttribute + BitConverter.ToInt16(recordActual.rawRecord, recordActual.offsetToAttribute + 32);
                runlist = recordActual.rawRecord[runlistOffset];
                runlistLowNibble = (byte)(runlist & 0x0F);
                runlistHighNibble = (byte)((runlist & 0xF0) >> 4);
                offsetBytesMFT = 0;
                isSparse = runlistHighNibble == 0 ? true : false; //sparse runlists
            }

            public void GETCLUSTERS(MFT_ENTRY recordActual)
            {
                if (!isSparse)
                {
                    Array.Copy(arrayPositivos, runLengthComplt, 4);
                    byte[] runLengthLeido = new byte[runlistLowNibble];
                    Array.Copy(recordActual.rawRecord, runlistOffset + 1, runLengthLeido, 0, runlistLowNibble);
                    Array.Copy(runLengthLeido, runLengthComplt, runlistLowNibble);
                    runLength = BitConverter.ToUInt32(runLengthComplt, 0);
                    byte[] runOffsetLeido = new byte[runlistHighNibble];
                    Array.Copy(recordActual.rawRecord, runlistOffset + 1 + runlistLowNibble, runOffsetLeido, 0, runlistHighNibble);
                    if ((int)runOffsetLeido[runlistHighNibble - 1] > 127)
                    {
                        int runOffset;
                        Array.Copy(arrayNegativos, runOffsetComplt, 4);
                        Array.Copy(runOffsetLeido, runOffsetComplt, runlistHighNibble);
                        runOffset = BitConverter.ToInt32(runOffsetComplt, 0);
                        offsetBytesMFT = offsetBytesMFT + (ulong)(runOffset * bytesxSector * sectorxCluster);
                    }
                    else
                    {
                        uint runOffset;
                        Array.Copy(arrayPositivos, runOffsetComplt, 4);
                        Array.Copy(runOffsetLeido, runOffsetComplt, runlistHighNibble);
                        runOffset = BitConverter.ToUInt32(runOffsetComplt, 0);
                        offsetBytesMFT = offsetBytesMFT + (ulong)runOffset * (ulong)bytesxSector * (ulong)sectorxCluster;
                    }
                }
                runlistOffset = runlistOffset + 1 + runlistLowNibble + runlistHighNibble;
            }

            public void NEXTDATARUNLIST(byte newRunlist)
            {
                runlist = newRunlist;
                runlistLowNibble = (byte)(runlist & 0x0F);
                runlistHighNibble = (byte)((runlist & 0xF0) >> 4);
                isSparse = runlistHighNibble == 0 ? true : false; //sparse runlists
            }
        }

        public static void info_SI(MFT_ENTRY entryData)
        {
            int datesOffset = entryData.offsetToAttribute + entryData.attributeContentOffset;
            entryData.dateCreated_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 4), BitConverter.ToUInt32(entryData.rawRecord, datesOffset));
            entryData.dateModificado_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 12), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 8));
            entryData.dateMFTModif_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 20), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 16));
            entryData.dateAccessed_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 28), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 24));
        }

        public static void info_AL(MFT_ENTRY entryData)
        {
            if (entryData.attributeIsResident == 0)
            {
                Int16 prevAttributeLength = entryData.attributeLength;
                Int32 prevOffsetToAttribute = entryData.offsetToAttribute;
                entryData.attributeLength = entryData.attributeContentOffset;
                procesaAttrList(entryData, Convert.ToInt32(entryData.attributeContentLength));
                entryData.attributeLength = prevAttributeLength;
                entryData.attributeSig = 0x20;
                entryData.offsetToAttribute = prevOffsetToAttribute;
            }
            else
            {
                MFT_ENTRY attListNoResident = entryData;
                GETDATARUNLIST dataRunlist = new GETDATARUNLIST(attListNoResident);
                byte[] prevRawRecord = entryData.rawRecord;
                Int16 prevAttributeLength = entryData.attributeLength;
                Int32 prevOffsetToAttribute = entryData.offsetToAttribute;
                Int32 contentLength = BitConverter.ToInt32(entryData.rawRecord, entryData.offsetToAttribute + 48);
                while (dataRunlist.runlist != (byte)(0x00))
                {
                    dataRunlist.GETCLUSTERS(attListNoResident);
                    if (!dataRunlist.isSparse)
                    {
                        uint runLength_ = dataRunlist.runLength;
                        entryData.rawRecord = ReadRaw(dataRunlist.offsetBytesMFT, runLength_ * sectorxCluster * bytesxSector);
                        entryData.attributeLength = 0;
                        entryData.offsetToAttribute = 0;
                        procesaAttrList(entryData, contentLength);
                    }
                    dataRunlist.NEXTDATARUNLIST(attListNoResident.rawRecord[dataRunlist.runlistOffset]);
                }
                entryData.rawRecord = prevRawRecord;
                entryData.attributeLength = prevAttributeLength;
                entryData.attributeSig = 0x20;
                entryData.offsetToAttribute = prevOffsetToAttribute;
                prevRawRecord = null;
            }
        }

        public static void procesaAttrList(MFT_ENTRY entryData, Int32 contentLength)
        {
            List<UInt32> listRecordsFNRef = new List<UInt32> { entryData.recordNumber };
            List<UInt32> listRecordsDataRef = new List<UInt32> { entryData.recordNumber };
            int cuentaLengthRecorrido = 0;
            while (cuentaLengthRecorrido < contentLength)
            {
                entryData.MFT_NEXT_ATTRIBUTE();
                if (entryData.attributeSig == 0xFFFFFFFF) //Hasta Data segun MFT_NEXT_ATTRIBUTE
                {
                    break;
                }
                else
                {
                    cuentaLengthRecorrido += Convert.ToInt32(entryData.attributeLength);
                    UInt32 attRecordNumber = BitConverter.ToUInt32(entryData.rawRecord, entryData.offsetToAttribute + 16);
                    UInt16 attID = BitConverter.ToUInt16(entryData.rawRecord, entryData.offsetToAttribute + 24);
                    if (entryData.attributeSig == 0x30 & !listRecordsFNRef.Contains(attRecordNumber))
                    {
                        listRecordsFNRef.Add(attRecordNumber);
                        Int16 intprevAttributeLength = entryData.attributeLength;
                        Int32 intprevOffsetToAttribute = entryData.offsetToAttribute;
                        GetPath.FileNameAndParentFrn localiza = GetPath.soloMFTDictOffsets[attRecordNumber];
                        byte[] refRecord = ReadRaw(localiza.RecordOffset, 1024);
                        MFT_ENTRY infoRefRecord = new MFT_ENTRY(refRecord);
                        infoRefRecord.MFT_NEXT_ATTRIBUTE();
                        while (infoRefRecord.attributeSig != END_RECORD_SIG)
                        {
                            if (infoRefRecord.attributeSig == FN_SIG)
                            {
                                infoRefRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                                info_FN(infoRefRecord);
                            }
                            infoRefRecord.MFT_NEXT_ATTRIBUTE();
                        }
                        entryData.nombreFN.AddRange(infoRefRecord.nombreFN);
                        entryData.dateModificado_FN.AddRange(infoRefRecord.dateModificado_FN);
                        entryData.dateAccessed_FN.AddRange(infoRefRecord.dateAccessed_FN);
                        entryData.dateMFTModif_FN.AddRange(infoRefRecord.dateMFTModif_FN);
                        entryData.dateCreated_FN.AddRange(infoRefRecord.dateCreated_FN);
                        entryData.parentDirectoryFN = infoRefRecord.parentDirectoryFN;
                        entryData.attributeLength = intprevAttributeLength;
                        entryData.attributeSig = 0x30;
                        entryData.offsetToAttribute = intprevOffsetToAttribute;
                        refRecord = null;
                    }
                    else if (entryData.attributeSig == 0x80 & !listRecordsDataRef.Contains(attRecordNumber))
                    {
                        listRecordsDataRef.Add(attRecordNumber);
                        Int16 intprevAttributeLength = entryData.attributeLength;
                        Int32 intprevOffsetToAttribute = entryData.offsetToAttribute;
                        GetPath.FileNameAndParentFrn localiza = GetPath.soloMFTDictOffsets[attRecordNumber];
                        byte[] refRecord = ReadRaw(localiza.RecordOffset, 1024);
                        MFT_ENTRY infoRefRecord = new MFT_ENTRY(refRecord);
                        infoRefRecord.MFT_NEXT_ATTRIBUTE();
                        while (infoRefRecord.attributeSig != END_RECORD_SIG)
                        {
                            if (infoRefRecord.attributeSig == DATA_SIG)
                            {
                                infoRefRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                                info_DATA(infoRefRecord);
                            }
                            infoRefRecord.MFT_NEXT_ATTRIBUTE();
                        }
                        if ((entryData.dataBase.Length == 0) & (infoRefRecord.dataBase.Length != 0)) //OJO!! Antes de asignar comprobar  & (infoRefRecord.dataBase.Length != 0)
                        { // Siempre debe apuntar la referencia al record origen porque es donde está la info de los clusters que ocupa
                            entryData.dataBase = entryData.recordNumber + infoRefRecord.dataBase.Substring(infoRefRecord.dataBase.IndexOf(":"));
                        }
                        if (entryData.realFileSize < infoRefRecord.realFileSize)
                        {
                            entryData.realFileSize = infoRefRecord.realFileSize;
                            entryData.diskFileSize = infoRefRecord.diskFileSize;
                        }
                        foreach (var resultados in infoRefRecord.diccIDDataADSInfo)
                        { // Siempre debe apuntar la referencia al record origen porque es donde está la info de los clusters que ocupa
                            entryData.diccIDDataADSInfo.Add(entryData.recordNumber + resultados.Key.Substring(resultados.Key.IndexOf(":")), resultados.Value);
                        }
                        entryData.attributeLength = intprevAttributeLength;
                        entryData.attributeSig = 0x30;
                        entryData.offsetToAttribute = intprevOffsetToAttribute;
                        refRecord = null;
                    }
                }
            }
        }

        public static void info_FN(MFT_ENTRY entryData)
        {
            entryData.parentDirectoryFN = BitConverter.ToUInt32(entryData.rawRecord, entryData.offsetToAttribute + entryData.attributeContentOffset);
            int datesOffset = entryData.offsetToAttribute + entryData.attributeContentOffset + 8;
            int fnNameLen = entryData.rawRecord[datesOffset + 56];
            string nombreEncontrado = Encoding.Unicode.GetString(entryData.rawRecord, datesOffset + 58, fnNameLen * 2);
            entryData.nombreFN.Add(nombreEncontrado);
            entryData.dateCreated_FN.Add(GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 4), BitConverter.ToUInt32(entryData.rawRecord, datesOffset)));
            entryData.dateModificado_FN.Add(GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 12), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 8)));
            entryData.dateMFTModif_FN.Add(GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 20), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 16)));
            entryData.dateAccessed_FN.Add(GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 28), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 24)));
        }

        public static void info_DATA(MFT_ENTRY entryData)
        {
            if (entryData.attributeNameLength != 0) //Solo para los ADS,s
            {
                byte adsNameLen = entryData.rawRecord[entryData.offsetToAttribute + 9];
                UInt16 adsNameOffset = entryData.rawRecord[entryData.offsetToAttribute + 10];
                string nombreEncontrado = Encoding.Unicode.GetString(entryData.rawRecord, entryData.offsetToAttribute + adsNameOffset, adsNameLen * 2);
                string idData = entryData.recordNumber.ToString() +  ":128-" + entryData.attributeID.ToString();
                UInt64 fileSize;
                UInt64 fileSizeOnDisk;
                if (!entryData.diccIDDataADSInfo.ContainsKey(idData))
                {
                    entryData.diccIDDataADSInfo.Add(idData, new dataADSInfo(nombreEncontrado, 0));
                    if (entryData.attributeIsResident == 0)
                    {
                        fileSizeOnDisk = 0;
                        fileSize = Convert.ToUInt64(entryData.attributeContentLength);
                    }
                    else
                    {
                        byte[] tempSize = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                        Array.Copy(entryData.rawRecord, entryData.offsetToAttribute + 40, tempSize, 0, 8);
                        fileSizeOnDisk = BitConverter.ToUInt64(tempSize, 0); //No lo voy a almacenar de momento...
                        Array.Copy(entryData.rawRecord, entryData.offsetToAttribute + 48, tempSize, 0, 8);
                        fileSize = BitConverter.ToUInt64(tempSize, 0);
                    }
                    entryData.diccIDDataADSInfo[idData].size = fileSize;
                }
            }
            else
            {
                entryData.dataBase = entryData.recordNumber.ToString() + ":128-" + entryData.attributeID.ToString();
                UInt64 fileSize;
                UInt64 fileSizeOnDisk;
                if (entryData.attributeIsResident == 0)
                {
                    fileSizeOnDisk = 0;
                    fileSize = Convert.ToUInt64(entryData.attributeContentLength);
                }
                else
                {
                    byte[] tempSize = new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    Array.Copy(entryData.rawRecord, entryData.offsetToAttribute + 40, tempSize, 0, 8);
                    fileSizeOnDisk = BitConverter.ToUInt64(tempSize, 0);
                    Array.Copy(entryData.rawRecord, entryData.offsetToAttribute + 48, tempSize, 0, 8);
                    fileSize = BitConverter.ToUInt64(tempSize, 0);
                }
                if (entryData.realFileSize < fileSize)
                {
                    entryData.realFileSize = fileSize;
                    entryData.diskFileSize = fileSizeOnDisk;
                }
            }
        }

        public static string GetDateTimeFromFiletime(long highBytes, uint lowBytes)
        {
            string formato = "yyyy/MM/dd HH:mm:ss.fffffff";
            long returnDateTime = highBytes << 32;
            returnDateTime = returnDateTime | lowBytes;
            if (returnDateTime >= DateTime.MinValue.Ticks && returnDateTime <= DateTime.MaxValue.Ticks)
            {
                return DateTime.FromFileTimeUtc(returnDateTime).ToString(formato);
            }
            else
            {
                return "La fecha no es válida";
            }
        }
    }
}

