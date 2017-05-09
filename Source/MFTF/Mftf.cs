using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;
using System.Threading;
using System.Security.Cryptography;

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
        public static string desdeCuando = "0000/00/00";
        public static string hastaCuando = "9999/99/99";
        public static string encabezadoT = "Date,Time,[MACB],Filename,Record,Size,SHA1";
        public static List<UInt32> refCoincid = new List<UInt32>();
        public static bool copiado = false;
        public static string nombreArch;
        public static bool origenValido = false;
        public static SortedDictionary<ulong, dataParaCopia> diccDatosCopia = new SortedDictionary<ulong, dataParaCopia>();
        public static string nameOut;
        public static string mftFile;
        public static int recursion;
        public static Dictionary<UInt32,List<UInt32>> diccRecordHijos = new Dictionary<UInt32,List<UInt32>>();
        public static Dictionary<UInt32, Dictionary<string,UInt16>> diccRecordADS = new Dictionary<uint, Dictionary<string,UInt16>>();
        public static DateTime empieza = DateTime.Now;
        public static  List<uint> listaDataRunLength = new List<uint>();
        public static List<ulong> listaDataOffset = new List<ulong>();
        public static string letraDisco;
        //Para lectura en vivo
        public static IntPtr hDisk;
        public static StreamWriter writer;
        public static BinaryReader readBin;
        public static int tam;
        public static bool todo;

        static void Main(string[] args)
        {
            todo = false;
            empieza = DateTime.Now;
            List<string> buscadasList = new List<string>();
            List<string> referencesToCopyList = new List<string>();
            CommandLine = new Arguments(args);
            if (CommandLine["h"] != null || args.Length < 2) { Console.WriteLine(LaAyuda()); }
            else
            {
                if (!string.IsNullOrEmpty(CommandLine["n"]))
                {
                    try
                    { 
                        if (!File.Exists(CommandLine["n"]))
                        {
                            using (File.Create(CommandLine["n"])) { };
                            File.Delete(CommandLine["n"]);
                        }
                        else
                        {
                            Console.WriteLine("\nError: destination file exists.");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: can't create the file {0}\n{1}", CommandLine["n"], e.Message.ToString());
                        return;
                    }
                }
                if ((!string.IsNullOrEmpty(CommandLine["ip"])) || ((!string.IsNullOrEmpty(CommandLine["cp"])) && (!string.IsNullOrEmpty(CommandLine["n"]))))
                {
                    ulong mftOffset = 0;
                    string objet = "";
                    if (!string.IsNullOrEmpty(CommandLine["cp"]))
                    {
                        letraDisco = CommandLine["cp"].Substring(0, 1) + ":";
                        objet = CommandLine["cp"];
                    }
                    else
                    {
                        letraDisco = CommandLine["ip"].Substring(0, 1).ToLower() + ":";
                        objet = CommandLine["ip"];
                    }
                    origen = string.Format("\\\\.\\{0}", letraDisco);
                    GetPath getFullPath = new GetPath();
                    getFullPath.Drive = origen;
                    mftOffset = GetDiskInfo();
                    hDisk = PInvokeWin32.CreateFile(origen,
                        PInvokeWin32.GENERIC_READ,
                        PInvokeWin32.FILE_SHARE_READ | PInvokeWin32.FILE_SHARE_WRITE,
                        IntPtr.Zero,
                        PInvokeWin32.OPEN_EXISTING,
                        0,
                        IntPtr.Zero);
                    if (hDisk.ToInt32() == PInvokeWin32.INVALID_HANDLE_VALUE)
                    {
                        Console.WriteLine("Invalid disk or elevated privileges needed: {0}", letraDisco);
                        origenValido = false;
                    }
                    else
                    {
                        bool ok = false;
                        bool buscADS = false;
                        string[] nomYads = null;
                        string busc = objet.Substring(objet.LastIndexOf("\\") + 1).ToLower();
                        string pathBuscado = letraDisco + "\\";
                        if (objet.LastIndexOf("\\") > 2)
                        {
                            pathBuscado = objet.Substring(0, objet.LastIndexOf("\\")).ToLower();
                        }
                        MakeSoloMFTDict(mftOffset);
                        if (Regex.IsMatch(busc, "^.*:.*$"))
                        {
                            nomYads = busc.Split(':');
                            buscADS = true;
                            busc = nomYads[0];
                        }
                        foreach (var pagina in GetPath.soloMFTDictOffsets)
                        {
                            if (pagina.Value.Name.ToLower() == busc)
                            {
                                string nombPath = GetPath.soloMFTGetFullyQualifiedPath(pagina.Value.ParentFrn).ToLower();
                                if (((nombPath.Length - nombPath.Replace(pathBuscado, String.Empty).Length) / nombPath.Length) == 1)
                                {
                                    if (!string.IsNullOrEmpty(CommandLine["ip"]))
                                    {
                                        refCoincid.Add(pagina.Key);
                                        GetCoinciDetalles();
                                        copiado = true;
                                    }
                                    else
                                    {
                                        if (!buscADS)
                                        {
                                            BuscaMFTRecordDesdePath(pagina.Key, mftOffset, CommandLine["n"]);
                                            if (copiado) { Console.WriteLine("Copy finished: {0}", CommandLine["n"]); }
                                            break;
                                        }
                                        else
                                        {
                                            foreach (var adsItem in diccRecordADS[pagina.Key])
                                            {
                                                if (adsItem.Key.ToLower() == nomYads[1].ToLower())
                                                {
                                                    BuscaMFTRecord(pagina.Key.ToString() + ":128-" + adsItem.Value.ToString(), CommandLine["n"]);
                                                    if (copiado) { Console.WriteLine("Copy finished: {0}", CommandLine["n"]); }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (!copiado) { Console.WriteLine("\nFile not found."); }
                    }
                }
                else if (((CommandLine["d"] != null) & (CommandLine["o"] == null)) || ((CommandLine["d"] == null) & (CommandLine["o"] != null)))
                {
                    ulong mftOffset = 0;
                    if (!string.IsNullOrEmpty(CommandLine["d"]))
                    {
                        letraDisco = CommandLine["d"].ToUpper().Substring(0, 1) + ":";
                        //Console.Write("Unit: {0}\n", letraDisco.ToUpper());
                        origen = string.Format("\\\\.\\{0}", letraDisco);
                        GetPath getFullPath = new GetPath();
                        getFullPath.Drive = origen;
                        mftOffset = GetDiskInfo();
                        hDisk = PInvokeWin32.CreateFile(origen,
                            PInvokeWin32.GENERIC_READ,
                            PInvokeWin32.FILE_SHARE_READ | PInvokeWin32.FILE_SHARE_WRITE,
                            IntPtr.Zero,
                            PInvokeWin32.OPEN_EXISTING,
                            0,
                            IntPtr.Zero);
                        if (hDisk.ToInt32() == PInvokeWin32.INVALID_HANDLE_VALUE)
                        {
                            Console.WriteLine("Invalid disk or elevated privileges needed: {0}", origen);
                            origenValido = false;
                        }
                    }
                    else if (!string.IsNullOrEmpty(CommandLine["o"]))
                    {
                        letraDisco = "\\";
                        mftOffset = 0;
                        mftFile = CommandLine["o"];
                        //Console.Write("$MFT offline file: " + mftFile + "\n");
                        try
                        {
                            byte[] cabecera = { 0x46, 0x49, 0x4C, 0x45 };
                            readBin = new BinaryReader(File.Open(mftFile, FileMode.Open));
                            byte[] checkMftFile = new byte[4];
                            readBin.Read(checkMftFile, 0, 4);
                            readBin.BaseStream.Seek(0, SeekOrigin.Begin);
                            if (BitConverter.ToInt32(cabecera, 0) == BitConverter.ToInt32(checkMftFile, 0))
                            {
                                origenValido = true;
                            }
                            else
                            {
                                origenValido = false;
                                Console.WriteLine("\nCheck the mft file or the path: Invalid mft file.");
                            }
                        }
                        catch
                        {
                            Console.WriteLine("\nCheck the mft file or the path: Invalid mft file.");
                            origenValido = false;
                        }
                    }
                    if (origenValido)
                    {
                        if (CommandLine["tf"] != null)
                        {
                            if (Regex.IsMatch(CommandLine["tf"], "^[0-9]{4}/[0-9]{2}/[0-9]{2}$"))
                            {
                                desdeCuando = CommandLine["tf"];
                            }
                        }
                        if (CommandLine["tt"] != null)
                        {
                            if (Regex.IsMatch(CommandLine["tt"], "^[0-9]{4}/[0-9]{2}/[0-9]{2}$"))
                            {
                                hastaCuando = CommandLine["tt"];
                            }
                        }
                        nameOut = DateTime.Now.ToString("yyMMddHHmmss") + "_References.txt";
                        if (CommandLine["l2t"] != null)
                        {
                            encabezadoT = "date,time,timezone,MACB,source,sourcetype,type,user,host,short,desc,version,filename,inode,notes,format,extra";
                        }
                        if (CommandLine["fads"] != null)
                        {
                            //Console.Write("\nSearching all the ADSs.\n");
                            if (CommandLine["x"] != null) { writer = new StreamWriter(nameOut, true); }
                            MakeSoloMFTDict(mftOffset);
                            BuscaTodosADSs(mftOffset);
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["fr"]))
                        {
                            if (CommandLine["x"] != null) { writer = new StreamWriter(nameOut, true); }
                            string cadeBuscada = CommandLine["fr"];
                            //Console.Write("\nRaw search:");
                            //Console.WriteLine(CommandLine["fr"]);
                            MakeSoloMFTDict(mftOffset);
                            BuscaCadenaRaw(mftOffset, cadeBuscada);
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["ff"]))
                        {
                            if (CommandLine["x"] != null) { writer = new StreamWriter(nameOut, true); }
                            var buscadasFile = File.ReadAllLines(CommandLine["ff"]);
                            buscadasList.AddRange(buscadasFile);
                            //Console.WriteLine("\nFinding strings from file {0}", CommandLine["ff"]);
                            MakeSoloMFTDict(mftOffset);
                            BuscaCadenasO(mftOffset, buscadasList);
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["f"]))
                        {
                            if (!Regex.IsMatch(CommandLine["f"], "^.*:.*$"))
                            {
                                if (CommandLine["x"] != null) { writer = new StreamWriter(nameOut, true); }
                                //Console.Write("\nFind:");
                                char[] delimiters = new char[] { '|' };
                                string[] words = CommandLine["f"].Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                               // Console.WriteLine(String.Join("|", words));
                                buscadasList.AddRange(words);
                                MakeSoloMFTDict(mftOffset);
                                BuscaCadenasO(mftOffset, buscadasList);
                            }
                            else
                            {
                                Console.WriteLine("\nAre you trying to search a path? Use -fd");
                            }
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["fd"]))
                        {
                            if (!CommandLine["fd"].StartsWith("\\\\"))
                            {
                                Console.WriteLine("\nPath must start with \\\\");
                            }
                            else
                            {
                                if (CommandLine["r"] != null)
                                {
                                    if (Regex.IsMatch(CommandLine["r"], "^[0-9]{1,2}$"))
                                    {
                                        recursion = Convert.ToInt32(CommandLine["r"]);
                                    }
                                    else
                                    {
                                        Console.WriteLine("\nWrong recursion number.");
                                        Environment.Exit(0);
                                    }
                                }
                                else
                                {
                                    recursion = 0;
                                }
                                if (CommandLine["x"] != null) { writer = new StreamWriter(nameOut, true); }
                                //Console.Write("\nMatch directory:");
                                //Console.WriteLine(CommandLine["fd"]);
                                //Console.WriteLine("Recursion: " + recursion.ToString());
                                char[] delimiters = new char[] { '|' };
                                string[] words = CommandLine["fd"].Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string aux in words)
                                {
                                    buscadasList.Add(letraDisco + aux.Substring(1));
                                }
                                //Console.WriteLine(String.Join("|", words));
                                MakeSoloMFTDict(mftOffset);
                                BuscaCadenasO(mftOffset, buscadasList);
                            }
                        }
                        else if ((CommandLine["tl"] != null) || (CommandLine["l2t"] != null))
                        {
                            Console.WriteLine(encabezadoT);
                            MakeSoloMFTDict(mftOffset);
                            GeneraTimelineO(mftOffset);
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["i"]))
                        {
                            if (Regex.IsMatch(CommandLine["i"], "^[0-9]{1,9}$"))
                            {
                                MakeSoloMFTDict(mftOffset);
                                refCoincid.Add(Convert.ToUInt32(CommandLine["i"], 10));
                                GetCoinciDetalles();
                            }
                            else
                            {
                                Console.WriteLine("\nNot found. Check MFT number.");
                            }
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["w"]))
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
                        else if (!string.IsNullOrEmpty(CommandLine["cn"]))
                        {
                            if (Regex.IsMatch(CommandLine["cn"], "^[0-9]{1,9}$"))
                            {
                                UInt32 recordToCopy = Convert.ToUInt32(CommandLine["cn"], 10);
                                CopiaRawRecord(recordToCopy, mftOffset);
                            }
                            else
                            {
                                Console.WriteLine("\nCheck the reference.");
                            }
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["cr"]))
                        {
                            if (CommandLine["o"] != null)
                            {
                                Console.WriteLine("\nNothing to copy! It's an offline hive.");
                            }
                            else
                            {
                                char[] delimiters = new char[] { '|' };
                                string[] words = CommandLine["cr"].Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                                //Console.WriteLine(String.Join("|", words));
                                referencesToCopyList.AddRange(words);
                                MakeSoloMFTDict(mftOffset);
                                foreach (string referenceBuscada in referencesToCopyList)
                                {
                                    //Console.WriteLine("Copying:" + referenceBuscada);
                                    //De momento solo los DATA: 128
                                    if (Regex.IsMatch(referenceBuscada, "^[0-9]{1,9}:128-[0-9]{1,4}$"))
                                    {
                                        string[] recordRef = referenceBuscada.Split(':');
                                        UInt32 recordBuscado = Convert.ToUInt32(recordRef[0], 10);
                                        BuscaMFTRecord(referenceBuscada);
                                        if (copiado) { Console.WriteLine("Copy finished: {0}", nombreArch); }
                                        else { Console.WriteLine("Record not found."); }
                                    }
                                    else { Console.WriteLine("\nReference {0} is incorrect.", referenceBuscada); }
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(CommandLine["cl"]))
                        {
                            if (CommandLine["o"] != null)
                            {
                                Console.WriteLine("\nNothing to copy! It's an offline hive.");
                            }
                            else
                            {
                                if (File.Exists(CommandLine["cl"]))
                                {
                                    try
                                    {
                                        //Console.WriteLine("\nReading list of references in file {0}", CommandLine["cl"]);
                                        var listaParaCopia = File.ReadAllLines(CommandLine["cl"]);
                                        foreach (string linea in listaParaCopia)
                                        {
                                            referencesToCopyList.Add(linea.Substring(0, linea.IndexOf('\t')));
                                        }
                                    }
                                    catch
                                    {
                                        Console.WriteLine("\nError reading file {0}.", CommandLine["cl"]);
                                        return;
                                    }
                                    MakeSoloMFTDict(mftOffset);
                                    foreach (string referenceBuscada in referencesToCopyList)
                                    {
                                        //Console.WriteLine("Copying:" + referenceBuscada);
                                        if (Regex.IsMatch(referenceBuscada, "^[0-9]{1,9}:128-[0-9]{1,4}$"))
                                        {
                                            string[] recordRef = referenceBuscada.Split(':');
                                            UInt32 recordBuscado = Convert.ToUInt32(recordRef[0], 10);
                                            BuscaMFTRecord(referenceBuscada);
                                            if (copiado) { Console.WriteLine("Copy finished: {0}", nombreArch); }
                                            else { Console.WriteLine("Record not found."); }
                                        }
                                        else { Console.WriteLine("\nReference {0} is incorrect.", referenceBuscada); }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("\nError reading file {0}.", CommandLine["cl"]);
                                }
                            }
                        }
                        
                        if (CommandLine.Parameters.ContainsKey("x"))
                        {
                            Console.WriteLine("\n----------------------------------------");
                            Console.WriteLine("References saved to file: {0}.", nameOut);
                        }
                        //TimeSpan ts = DateTime.Now.Subtract(empieza);
                        //string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        //        ts.Hours, ts.Minutes, ts.Seconds,
                        //        ts.Milliseconds / 10);
                        //Console.WriteLine("RunTime: " + elapsedTime);
                    }
                }
                else Console.WriteLine(LaAyuda());
            }
            if (writer != null)
            {
                writer.Close();
                writer.Dispose();
            }
            if (readBin != null)
            {
                readBin.Close();
                readBin.Dispose();
            }
            if (hDisk != null) { PInvokeWin32.CloseHandle(hDisk); }
        }

        public static void CopiaRawRecord(UInt32 recordToCopy, ulong mftOffset) 
        {
            string nombreArch = "[" + recordToCopy + "]_" + DateTime.Now.ToString("yyMMddHHmmss") + ".dat";
            bool loTengo = false;
            if (CommandLine["o"] != null)
            {
                int pos = 0;
                tam = (int)readBin.BaseStream.Length;
                byte[] content2 = new byte[1024];
                while (pos < tam)
                {
                    content2 = readBin.ReadBytes(1024);
                    if (BitConverter.ToUInt32(content2, 44) == recordToCopy)
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
                                    Console.Write("{0:X2} ", content2[m + (16 * t)]);
                                }
                                for (int m = 0; m < 16; m++)
                                {
                                    if ((content2[m + (16 * t)] < 32) | (content2[m + (16 * t)] == 129) | (content2[m + (16 * t)] == 141)
                                     | (content2[m + (16 * t)] == 143) | (content2[m + (16 * t)] == 144) | (content2[m + (16 * t)] == 157)
                                     | (content2[m + (16 * t)] == 159))
                                    {
                                        Console.Write(".");
                                    }
                                    else
                                    {
                                        Console.Write(Encoding.Default.GetString(content2, m + (16 * t), 1));
                                    }
                                }
                                Console.WriteLine();
                            }
                            break;
                        }
                        else
                        {
                            loTengo = true;
                            File.WriteAllBytes(nombreArch, content2);
                            Console.WriteLine("\nMFT entry record copied to {0}", nombreArch);
                            break;
                        }
                    }
                    if (loTengo) { return; }
                    pos += 1024;
                }
            }
            else
            {
                MFT_ENTRY mftEntry = new MFT_ENTRY(ReadRaw(mftOffset, 1024));
                while (mftEntry.attributeSig != DATA_SIG)
                {
                    mftEntry.offsetToAttribute += mftEntry.attributeLength;
                    mftEntry.attributeSig = BitConverter.ToUInt32(mftEntry.rawRecord, mftEntry.offsetToAttribute);
                    mftEntry.attributeLength = BitConverter.ToInt16(mftEntry.rawRecord, mftEntry.offsetToAttribute + 4);
                }
                GETDATARUNLIST dataRunlist = new GETDATARUNLIST(mftEntry);
                dataRunlist.GETLISTS(mftEntry);
                foreach (var doff in listaDataOffset)
                {
                    uint runLength_ = listaDataRunLength[listaDataOffset.IndexOf(doff)];
                    var posIni = doff;
                    uint pos = 0;
                    uint clusterBytes = (sectorxCluster * bytesxSector);
                    byte[] cluster = new byte[clusterBytes];
                    byte[] content = new byte[1024];
                    ulong byteActual = posIni;
                    while (pos < runLength_)
                    {
                        cluster = ReadRaw(posIni + (pos * clusterBytes), clusterBytes);
                        for (ulong n = 0; n < (clusterBytes / 1024); n++)
                        {
                            Array.Copy(cluster, (int)(n * 1024), content, 0, 1024);
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
                                    break;
                                }
                                else
                                {
                                    loTengo = true;
                                    File.WriteAllBytes(nombreArch, content);
                                    Console.WriteLine("\nMFT entry record copied to {0}", nombreArch);
                                    break;
                                }
                            }
                        }
                        if (loTengo) { return; }
                        pos += 1;
                    }
                }
            }
            if (!loTengo)
            {
                Console.WriteLine("\nRecord not found.");
            }
        }

        public static void GeneraTimelineO(ulong mftOffset)
        {
            if (CommandLine["o"] != null)
            {
                byte[] content2 = new byte[1024];
                long pos = 0;
                MFT_ENTRY infoMFT = null;
                int nn = 0;
                long tam = readBin.BaseStream.Length;
                while (pos < tam)
                {
                    content2 = readBin.ReadBytes(1024);
                    UInt32 mftSig = BitConverter.ToUInt32(content2, 0);
                    if (mftSig != FILE_SIG)
                    {
                        pos += 1024;
                        continue;
                    }
                    infoMFT = new MFT_ENTRY(content2);
                    if (!infoMFT.recordValido)
                    {
                        pos += 1024;
                        continue;
                    }
                    if (infoMFT.fileReferenceToBaseFile == 0) 
                    {
                        try
                        {
                            GetCoinciDetallesInfo(infoMFT);
                            infoMFT.MFT_SHOW_DATA();
                        }
                        catch
                        {
                            //captura las excepciones por records validos pero que no contienen nada de nada
                        }
                    }
                    pos += 1024;
                }
            }
            else
            {
                foreach (var doff in listaDataOffset)
                {
                    uint runLength_ = listaDataRunLength[listaDataOffset.IndexOf(doff)];
                    var posIni = doff;
                    uint pos = 0;
                    uint clusterBytes = (sectorxCluster * bytesxSector);
                    byte[] cluster = new byte[clusterBytes];
                    byte[] record = new byte[1024];
                    ulong byteActual = posIni;
                    while (pos < runLength_)
                    {
                        cluster = ReadRaw(posIni + (pos * clusterBytes), clusterBytes);
                        for (ulong n = 0; n < (clusterBytes / 1024); n++)
                        {
                            Array.Copy(cluster, (int)(n * 1024), record, 0, 1024);
                            UInt32 mftSig = BitConverter.ToUInt32(record, 0);
                            if (mftSig != FILE_SIG) { continue; } //no valid record
                            MFT_ENTRY infoMFT = new MFT_ENTRY(record);
                            if (!infoMFT.recordValido)
                            {
                                continue;
                            }
                            if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde el nombre FN no está en el record base
                            {
                                try
                                {
                                    GetCoinciDetallesInfo(infoMFT);
                                    infoMFT.MFT_SHOW_DATA();
                                    infoMFT = null;
                                }
                                catch
                                {
                                    //captura las excepciones por records validos pero que no contienen nada de nada
                                }
                            }
                        }
                        pos += 1;
                    }
                }
            }

        }

        public static void BuscaTodosADSs(ulong mftOffset) //
        {
            if (CommandLine["o"] != null)
            {
                int pos = 0;
                tam = (int)readBin.BaseStream.Length;
                byte[] content2 = new byte[1024];
                while (pos < tam)
                {
                    content2 = readBin.ReadBytes(1024);
                    UInt32 mftSig = BitConverter.ToUInt32(content2, 0);
                    if (mftSig != FILE_SIG)
                    {
                        pos += 1024;
                        continue; //no valid record
                    }
                    MFT_ENTRY infoMFT = new MFT_ENTRY(content2);
                    if (!infoMFT.recordValido)
                    {
                        Console.WriteLine("I omit record {0}: has a wrong fixup value", infoMFT.recordNumber);
                        pos += 1024;
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
                    pos += 1024;
                }
            }
            else
            {
                foreach (var doff in listaDataOffset)
                {
                    uint runLength_ = listaDataRunLength[listaDataOffset.IndexOf(doff)];
                    var posIni = doff;
                    uint pos = 0;
                    uint clusterBytes = (sectorxCluster * bytesxSector);
                    byte[] cluster = new byte[clusterBytes];
                    byte[] content = new byte[1024];
                    ulong byteActual = posIni;
                    while (pos < runLength_)
                    {
                        cluster = ReadRaw(posIni + (pos * clusterBytes), clusterBytes);
                        for (ulong n = 0; n < (clusterBytes / 1024); n++)
                        {
                            Array.Copy(cluster, (int)(n * 1024), content, 0, 1024);
                            UInt32 mftSig = BitConverter.ToUInt32(content, 0);
                            if (mftSig != FILE_SIG) { continue; } //no valid record
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
                        pos += 1;
                    }
                }
            }
            if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null)) Console.WriteLine("Total: {0}\n", refCoincid.Count);
            GetCoinciDetalles();
        }

        public static ulong GetDiskInfo()
        {
            ulong mftClusterStart;
            byte[] diskInfo = ReadRawD(0);
            if (diskInfo != null)
            {
                string fileSystem = Encoding.UTF8.GetString(new byte[] { diskInfo[3], diskInfo[4], diskInfo[5], diskInfo[6] });
                bytesxSector = BitConverter.ToUInt16(diskInfo, 11);
                sectorxCluster = Convert.ToUInt16(diskInfo.GetValue(13));
                mftClusterStart = BitConverter.ToUInt64(diskInfo, 48);
                ulong offset = mftClusterStart * sectorxCluster * bytesxSector;
                if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null))
                {
                    Console.Write("FS: {0}\n", fileSystem);
                    Console.Write("Sector size: {0} bytes\n", bytesxSector);
                    Console.Write("Cluster size: {0} sectors\n", sectorxCluster);
                    Console.Write("Starting cluster of the MFT: " + mftClusterStart + " [Offset: 0x" + offset.ToString("X") + "]\n");
                }
                origenValido = true;
                return offset;
            }
            else
            {
                origenValido = false;
                return 0;
            }
        }

        public static void BuscaCadenaRaw(ulong mftOffset, string cadeBuscada) //
        {
            if (CommandLine["o"] != null)
            {
                int pos = 0;
                tam = (int)readBin.BaseStream.Length;
                byte[] content2 = new byte[1024];
                while (pos < tam)
                {
                    content2 = readBin.ReadBytes(1024);
                    UInt32 mftSig = BitConverter.ToUInt32(content2, 0);
                    if (mftSig != FILE_SIG)
                    {
                        pos += 1024;
                        continue;
                    }
                    MFT_ENTRY infoMFT = new MFT_ENTRY(content2);
                    if (!infoMFT.recordValido)
                    {
                        Console.WriteLine("I omit record {0}: has a wrong fixup value", infoMFT.recordNumber);
                        pos += 1024;
                        continue;
                    }
                    string cadenaRawa = Encoding.Default.GetString(content2).Replace("\0", "").ToLower(); //Al menos quito los 0
                    // Según parece es más rápido http://cc.davelozinski.com/c-sharp/fastest-way-to-check-if-a-string-occurs-within-a-string 
                    //(((infoMFT.nombres.Length - (infoMFT.nombres.Replace(nombreBuscado.ToLower(), String.Empty)).Length) / nombreBuscado.Length) > 0)
                    // que hacer:
                    //if (infoMFT.nombres.Contains(nombreBuscado.ToLower()))
                    if (((cadenaRawa.Length - (cadenaRawa.ToLower().Replace(cadeBuscada.ToLower(), String.Empty)).Length) / cadeBuscada.Length) > 0)
                    {
                        if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde hay record base
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
                    pos += 1024;
                }
            }
            else
            {
                foreach (var doff in listaDataOffset)
                {
                    uint runLength_ = listaDataRunLength[listaDataOffset.IndexOf(doff)];
                    var posIni = doff;
                    uint pos = 0;
                    uint clusterBytes = (sectorxCluster * bytesxSector);
                    byte[] cluster = new byte[clusterBytes];
                    byte[] entryInfo = new byte[1024];
                    ulong byteActual = posIni;
                    while (pos < runLength_)
                    {
                        cluster = ReadRaw(posIni + (pos * clusterBytes), clusterBytes);
                        for (ulong n = 0; n < (clusterBytes / 1024); n++)
                        {
                            Array.Copy(cluster, (int)(n * 1024), entryInfo, 0, 1024);
                            UInt32 mftSig = BitConverter.ToUInt32(entryInfo, 0);
                            if (mftSig != FILE_SIG) { continue; } 
                            MFT_ENTRY infoMFT = new MFT_ENTRY(entryInfo);
                            if (!infoMFT.recordValido)
                            {
                                Console.WriteLine("I omit record {0}: has a wrong fixup value", infoMFT.recordNumber);
                                continue;
                            }
                            string cadenaRawa = Encoding.Default.GetString(entryInfo).Replace("\0", "").ToLower(); //Al menos quito los 0
                            // Según parece es más rápido http://cc.davelozinski.com/c-sharp/fastest-way-to-check-if-a-string-occurs-within-a-string 
                            //(((infoMFT.nombres.Length - (infoMFT.nombres.Replace(nombreBuscado.ToLower(), String.Empty)).Length) / nombreBuscado.Length) > 0)
                            // que hacer:
                            //if (infoMFT.nombres.Contains(nombreBuscado.ToLower()))
                            if (((cadenaRawa.Length - (cadenaRawa.ToLower().Replace(cadeBuscada.ToLower(), String.Empty)).Length) / cadeBuscada.Length) > 0)
                            {
                                if (infoMFT.fileReferenceToBaseFile == 0) //Comprobacion para casos donde hay record base
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
                        pos += 1;
                    }
                }
            }
            if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null)) Console.WriteLine("Total: {0}\n", refCoincid.Count);
            GetCoinciDetalles();
        }

        public static void BuscaCadenasO(ulong mftOffset, List<string> buscadasList = null, string recordMFT = "")
        {
            if (CommandLine["o"] != null)
            {
                BuscaCoincidenciasO(0, 0, buscadasList);
                readBin.BaseStream.Seek(0, SeekOrigin.Begin);
            }
            else
            {
                if (buscadasList.Count != 0)
                {
                    foreach (var doff in listaDataOffset)
                    {
                        BuscaCoincidencias(listaDataRunLength[listaDataOffset.IndexOf(doff)], doff, buscadasList);
                    }
                }
            }
            if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null)) Console.WriteLine("Total: {0}\n", refCoincid.Count);
            GetCoinciDetalles();
        }

        public static byte[] ReadRaw(ulong _offset, UInt32 numBytesToRead = 512)
        {
            if (CommandLine["o"] != null)
            {
                byte[] buffer = new byte[numBytesToRead];
                long prevPos = readBin.BaseStream.Position;
                readBin.BaseStream.Position = (long)_offset;
                readBin.Read(buffer, 0, (int)numBytesToRead);
                readBin.BaseStream.Position = prevPos;
                return buffer;
            }
            else
            {
                Int64 newOffset;
                PInvokeWin32.SetFilePointerEx(hDisk, _offset, out newOffset, PInvokeWin32.FILE_BEGIN);
                UInt32 numBytesRead;
                byte[] buffer = new byte[numBytesToRead];
                if (PInvokeWin32.ReadFile(hDisk, buffer, numBytesToRead, out numBytesRead, IntPtr.Zero))
                {
                    return buffer;
                }
                else
                {
                    Console.WriteLine("Invalid disk or elevated privileges needed: {0}", origen);
                    return null;
                }
            }
        }


        public static unsafe void ReadRawV(byte[] buffer, ulong _offset, UInt32 numBytesToRead = 512)
        {
            if ((CommandLine["o"] != null) || (CommandLine["tl"] != null) || (CommandLine["l2t"] != null))
            {
            }
            else
            {
                Int64 newOffset;
                PInvokeWin32.SetFilePointerEx(hDisk, _offset, out newOffset, PInvokeWin32.FILE_BEGIN);
                UInt32 numBytesRead;

                fixed (byte* p = buffer)
                {
                    if (PInvokeWin32.ReadFile(hDisk, buffer, numBytesToRead, out numBytesRead, IntPtr.Zero))
                    {
                        Console.WriteLine("Disco leido");
                        PInvokeWin32.CloseHandle(hDisk);
                    }
                    else
                    {
                        Console.WriteLine("Invalid disk or elevated privileges needed: {0}", origen);
                    }
                }
            }
        }

        public static byte[] ReadRawD(ulong _offset, UInt32 numBytesToRead = 512)
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
                    Console.WriteLine("Invalid disk or elevated privileges needed: {0}", origen);
                    return null;
                }
            }
            else
            {
                Console.WriteLine("Invalid disk or elevated privileges needed: {0}", origen);
                return null;
            }
        }

        public static void MakeSoloMFTDict(ulong mftOffset)
        {
            if (CommandLine["o"] != null)
            {
                try
                {
                    int pos = 0;
                    tam = (int)readBin.BaseStream.Length;
                    byte[] content2 = new byte[1024];
                    while (pos < tam)
                    {
                        content2 = readBin.ReadBytes(1024);
                        string nombre = " ";
                        UInt32 parentDirectory = 0;
                        UInt32 mftSig = BitConverter.ToUInt32(content2, 0);
                        if (mftSig != FILE_SIG) {
                            pos += 1024;
                            continue; } //no valid record
                        MFT_ENTRY infoMFT = new MFT_ENTRY(content2);
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
                                GetPath.FileNameAndParentFrn actualizar = new GetPath.FileNameAndParentFrn(nNombre, parentDirectory, Convert.ToUInt64(pos));
                                GetPath.soloMFTDictOffsets.Remove(infoMFT.recordNumber);
                                GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, actualizar);
                            }
                            else
                            {
                                GetPath.FileNameAndParentFrn f = new GetPath.FileNameAndParentFrn(nombre, parentDirectory, Convert.ToUInt64(pos));
                                GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, f);
                            }
                        }
                        else
                        {
                            if (diccRecordHijos.ContainsKey(infoMFT.fileReferenceToBaseFile))
                            {
                                diccRecordHijos[infoMFT.fileReferenceToBaseFile].Add(infoMFT.recordNumber);
                            }
                            else
                            {
                                diccRecordHijos.Add(infoMFT.fileReferenceToBaseFile, new List<UInt32> { infoMFT.recordNumber });
                            }
                            GetPath.FileNameAndParentFrn f = new GetPath.FileNameAndParentFrn("Metadata/System File", 1, Convert.ToUInt64(pos));
                            GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, f);
                            if (GetPath.soloMFTDictOffsets.ContainsKey(infoMFT.fileReferenceToBaseFile))
                            {
                                string nNombre = GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].Name.Length < nombre.Length ? nombre : GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].Name;
                                ulong nOffset = GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].RecordOffset != 0 ? GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].RecordOffset : 0;
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
                        pos += 1024;
                    }
                    GetPath.FileNameAndParentFrn actualizarRoot = new GetPath.FileNameAndParentFrn("\\\\", 0, GetPath.soloMFTDictOffsets[5].RecordOffset);
                    GetPath.soloMFTDictOffsets.Remove(5);
                    GetPath.soloMFTDictOffsets.Add(5, actualizarRoot);
                    if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null)) Console.WriteLine("Records: {0}", GetPath.soloMFTDictOffsets.Count.ToString("N0"));
                    readBin.BaseStream.Seek(0, SeekOrigin.Begin);
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0} Exception caught.", e.Message);
                    Environment.Exit(0);
                }
            }
            else
            {
                MFT_ENTRY mftEntry = new MFT_ENTRY(ReadRaw(mftOffset, 1024));
                while (mftEntry.attributeSig != DATA_SIG)
                {
                    mftEntry.offsetToAttribute += mftEntry.attributeLength;
                    mftEntry.attributeSig = BitConverter.ToUInt32(mftEntry.rawRecord, mftEntry.offsetToAttribute);
                    mftEntry.attributeLength = BitConverter.ToInt16(mftEntry.rawRecord, mftEntry.offsetToAttribute + 4);
                }
                GETDATARUNLIST dataRunlist = new GETDATARUNLIST(mftEntry);
                dataRunlist.GETLISTS(mftEntry);
                foreach (var doff in listaDataOffset)
                {
                    uint runLength_ = listaDataRunLength[listaDataOffset.IndexOf(doff)];
                    var posIni = doff;
                    uint pos = 0;
                    uint clusterBytes = (sectorxCluster * bytesxSector);
                    byte[] cluster = new byte[clusterBytes];
                    byte[] record = new byte[1024];
                    ulong byteActual = posIni;
                    while (pos < runLength_)
                    {
                        cluster = ReadRaw(posIni + (pos * clusterBytes), clusterBytes);
                        for (ulong n = 0; n < (clusterBytes / 1024); n++)
                        {
                            Array.Copy(cluster, (int)(n * 1024), record, 0, 1024);
                            byteActual = posIni + (pos * clusterBytes) + (1024 * n);
                            string nombre = " ";
                            Dictionary<string,UInt16> nombADS = new Dictionary<string,UInt16>();
                            UInt32 parentDirectory = 0;
                            UInt32 mftSig = BitConverter.ToUInt32(record, 0);
                            if (mftSig != FILE_SIG)
                            {
                                continue;
                            }
                            MFT_ENTRY infoMFT = new MFT_ENTRY(record);
                            infoMFT.MFT_NEXT_ATTRIBUTE();
                            while (infoMFT.attributeSig != END_RECORD_SIG)
                            {
                                if (infoMFT.attributeSig == FN_SIG)
                                {
                                    infoMFT.MFT_NEXT_ATTRIBUTE_VALIDO();
                                    int datesOffset = infoMFT.offsetToAttribute + infoMFT.attributeContentOffset + 8;
                                    int fnNameLen = infoMFT.rawRecord[datesOffset + 56];
                                    if ((fnNameLen > nombre.Length) || (nombre == " "))
                                    {
                                        nombre = Encoding.Unicode.GetString(infoMFT.rawRecord, datesOffset + 58, fnNameLen * 2);
                                    }
                                    parentDirectory = BitConverter.ToUInt32(infoMFT.rawRecord, infoMFT.offsetToAttribute + infoMFT.attributeContentOffset);
                                }
                                else if (infoMFT.attributeSig == DATA_SIG)
                                {
                                    infoMFT.MFT_NEXT_ATTRIBUTE_VALIDO();
                                    if (infoMFT.attributeNameLength != 0) //Solo es necesario parsearlo para los ADS,s para buscar sus nombres
                                    {
                                        byte adsNameLen = infoMFT.rawRecord[infoMFT.offsetToAttribute + 9];
                                        UInt16 adsNameOffset = infoMFT.rawRecord[infoMFT.offsetToAttribute + 10];
                                        UInt16 attID = infoMFT.rawRecord[infoMFT.offsetToAttribute + 14]; 
                                        nombADS.Add(Encoding.Unicode.GetString(infoMFT.rawRecord, infoMFT.offsetToAttribute + adsNameOffset, adsNameLen * 2), attID);
                                    }
                                }
                                infoMFT.MFT_NEXT_ATTRIBUTE();
                            }
                            // Records del 12 al 15: marcados como en uso (1) pero sin nada
                            // Records del 16 al 23: marcados como no en uso (0) pero con número de record 0 (como la $MFT) en algunas $MFT
                                if (infoMFT.fileReferenceToBaseFile == 0)
                                {
                                    if (GetPath.soloMFTDictOffsets.ContainsKey(infoMFT.recordNumber))
                                    {
                                        string nNombre = GetPath.soloMFTDictOffsets[infoMFT.recordNumber].Name.Length < nombre.Length ? nombre : GetPath.soloMFTDictOffsets[infoMFT.recordNumber].Name;
                                        UInt32 nparentDirectory = parentDirectory == 0 ? GetPath.soloMFTDictOffsets[infoMFT.recordNumber].ParentFrn : 0;
                                        GetPath.FileNameAndParentFrn actualizar = new GetPath.FileNameAndParentFrn(nNombre, parentDirectory, byteActual);
                                        GetPath.soloMFTDictOffsets.Remove(infoMFT.recordNumber);
                                        GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, actualizar);
                                        if (nombADS != null)
                                        {
                                            if (diccRecordADS.ContainsKey(infoMFT.recordNumber))
                                            {
                                                foreach ( var diccItem in diccRecordADS[infoMFT.recordNumber].Keys )
                                                {
                                                    if (!nombADS.ContainsKey(diccItem)) {
                                                        nombADS.Add(diccItem, diccRecordADS[infoMFT.recordNumber][diccItem]);
                                                    }
                                                }
                                            }
                                            diccRecordADS.Remove(infoMFT.recordNumber);
                                            diccRecordADS.Add(infoMFT.recordNumber, nombADS);
                                        }
                                    }
                                    else
                                    {
                                    GetPath.FileNameAndParentFrn f = new GetPath.FileNameAndParentFrn(nombre, parentDirectory, byteActual);
                                    GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, f);
                                    diccRecordADS.Add(infoMFT.recordNumber, nombADS);
                                    }
                                }
                                else
                                {
                                    if (diccRecordHijos.ContainsKey(infoMFT.fileReferenceToBaseFile))
                                    {
                                        diccRecordHijos[infoMFT.fileReferenceToBaseFile].Add(infoMFT.recordNumber);
                                    }
                                    else
                                    {
                                        diccRecordHijos.Add(infoMFT.fileReferenceToBaseFile, new List<UInt32> { infoMFT.recordNumber });
                                    }
                                    GetPath.FileNameAndParentFrn f = new GetPath.FileNameAndParentFrn("Metadata/System File", 1, byteActual);
                                    GetPath.soloMFTDictOffsets.Add(infoMFT.recordNumber, f);
                                    if (GetPath.soloMFTDictOffsets.ContainsKey(infoMFT.fileReferenceToBaseFile))
                                    {
                                        string nNombre = GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].Name.Length < nombre.Length ? nombre : GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].Name;
                                    ulong nOffset = GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].RecordOffset != 0 ? GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].RecordOffset : 0;
                                        UInt32 nparentDirectory = parentDirectory == 0 ? GetPath.soloMFTDictOffsets[infoMFT.fileReferenceToBaseFile].ParentFrn : parentDirectory;
                                        GetPath.FileNameAndParentFrn actualizar = new GetPath.FileNameAndParentFrn(nNombre, nparentDirectory, nOffset);
                                        GetPath.soloMFTDictOffsets.Remove(infoMFT.fileReferenceToBaseFile);
                                        GetPath.soloMFTDictOffsets.Add(infoMFT.fileReferenceToBaseFile, actualizar);
                                        if (nombADS != null)
                                        {
                                            if (diccRecordADS.ContainsKey(infoMFT.fileReferenceToBaseFile))
                                            {
                                                foreach (var diccItem in diccRecordADS[infoMFT.fileReferenceToBaseFile].Keys)
                                                {
                                                    if (!nombADS.ContainsKey(diccItem))
                                                    {
                                                        nombADS.Add(diccItem, diccRecordADS[infoMFT.fileReferenceToBaseFile][diccItem]);
                                                    }
                                                }
                                            }
                                            diccRecordADS.Remove(infoMFT.fileReferenceToBaseFile);
                                            diccRecordADS.Add(infoMFT.fileReferenceToBaseFile, nombADS);
                                        }
                                    }
                                    else
                                    {
                                        GetPath.FileNameAndParentFrn actualizar = new GetPath.FileNameAndParentFrn(nombre, parentDirectory, 0);
                                        GetPath.soloMFTDictOffsets.Add(infoMFT.fileReferenceToBaseFile, actualizar);
                                        diccRecordADS.Add(infoMFT.fileReferenceToBaseFile, nombADS);
                                    }
                                }
                        }
                        pos += 1;
                    }
                }
                GetPath.FileNameAndParentFrn actualizarRoot = new GetPath.FileNameAndParentFrn(letraDisco + "\\", 0, GetPath.soloMFTDictOffsets[5].RecordOffset );
                GetPath.soloMFTDictOffsets.Remove(5);
                GetPath.soloMFTDictOffsets.Add(5, actualizarRoot);
                if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null)) Console.WriteLine("Records: {0}", GetPath.soloMFTDictOffsets.Count.ToString("N0"));
            }
        }

        public static void BuscaMFTRecordDesdePath(UInt32 record, ulong mftOffset, string nombreArch)
        {
            string referenceBuscada = "";
            ushort attIDBuscado = 0;
            ulong llevoCopiado = 0;
            GetPath.FileNameAndParentFrn localizado = GetPath.soloMFTDictOffsets[record];
            byte[] refRecord = ReadRaw(localizado.RecordOffset, 1024);
            MFT_ENTRY infoRecord = new MFT_ENTRY(refRecord);
            if (infoRecord.valFileFlags == 1 || infoRecord.valFileFlags == 5 || infoRecord.valFileFlags == 0)
            {
                infoRecord.MFT_NEXT_ATTRIBUTE();
                while (infoRecord.attributeSig != END_RECORD_SIG)
                {
                    if (infoRecord.attributeSig == AL_SIG) //Busco el DATA en la lista
                    {
                        infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                        if (infoRecord.attributeNonResident == 0)
                        {
                            Int16 prevAttributeLength = infoRecord.attributeLength;
                            Int32 prevOffsetToAttribute = infoRecord.offsetToAttribute;
                            infoRecord.attributeLength = infoRecord.attributeContentOffset;
                            ProcessAttrListParaCopiaDesdePath(infoRecord, Convert.ToInt32(infoRecord.attributeContentLength));
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
                                    ProcessAttrListParaCopiaDesdePath(infoRecord, contentLength);
                                }
                                dataRunlist.NEXTDATARUNLIST(prevRawRecord[dataRunlist.runlistOffset]);
                            }
                            infoRecord.rawRecord = prevRawRecord;
                            infoRecord.attributeLength = prevAttributeLength;
                            infoRecord.attributeSig = 0x20;
                            infoRecord.offsetToAttribute = prevOffsetToAttribute;
                        }
                        if (copiado)
                        {
                            if (diccDatosCopia[infoRecord.attrListStartVCN].isResident) //Si el contenido era residente lo escribo
                            {
                                File.WriteAllBytes(nombreArch, diccDatosCopia[infoRecord.attrListStartVCN].contentResident);
                            }
                            else // PARSEO EL diccDatosCopia y ya tengo el DATA copiado
                            {
                                ulong sizeArchivo = 0;
                                Int32 elementos = diccDatosCopia.Count;
                                int n = 0;
                                foreach (KeyValuePair<ulong, dataParaCopia> datarun in diccDatosCopia)
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
                                        CopiaNoResidentDATA(infoRecordDatarun, n, elementos, sizeArchivo, nombreArch, ref llevoCopiado);
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
                        if (infoRecord.attributeNameLength == 0)
                        {
                            if (infoRecord.attributeNonResident == 1)
                            {
                                ulong sizeArchivo = BitConverter.ToUInt64(infoRecord.rawRecord, infoRecord.offsetToAttribute + 48);
                                CopiaNoResidentDATA(infoRecord, 0, 0, sizeArchivo, nombreArch, ref llevoCopiado);
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
                if (!copiado)
                {
                    Console.WriteLine("\nReference {0} not found", referenceBuscada);
                }
                else
                {
                    Console.WriteLine("File copied to: {0}", nombreArch);
                }
                refRecord = null;
            }
            else
            {
                switch (infoRecord.valFileFlags)
                {
                    //case 0:
                    //    Console.WriteLine("\nNot suported: deleted file");
                    //    break;
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

        public static void ProcessAttrListParaCopiaDesdePath(MFT_ENTRY infoRecord, Int32 contentLength) //
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
                    byte lengthName = infoRecord.rawRecord[infoRecord.offsetToAttribute + 6];
                    byte attID = infoRecord.rawRecord[infoRecord.offsetToAttribute + 24];
                    if (lengthName == 0)
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
                                if (entryData.attributeID == (ushort)attID)
                                {
                                    copiado = true;
                                    ulong fileSize;
                                    if (entryData.attributeNonResident == 0)
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

        public static void BuscaMFTRecord(string referenceBuscada, string archFinal = "") //
        {
            ulong llevoCopiado = 0;
            char[] delimiters = new char[] { ':', '-' };
            string[] referencePartes = referenceBuscada.Split(delimiters);
            UInt16 attIDBuscado = Convert.ToUInt16(referencePartes[2], 10);
            UInt32 mftRefBuscada = Convert.ToUInt32(referencePartes[0], 10);
            try
            {
                GetPath.FileNameAndParentFrn localizado = GetPath.soloMFTDictOffsets[mftRefBuscada];
                byte[] refRecord = ReadRaw(localizado.RecordOffset, 1024);
                if (string.IsNullOrEmpty(archFinal))
                {
                    nombreArch = "[" + referenceBuscada.Replace(":", "-") + "]";
                    nombreArch = nombreArch + "-" + localizado.Name;
                    foreach (var adsItem in diccRecordADS[mftRefBuscada])
                    {
                        if (adsItem.Value == attIDBuscado)
                        {
                            nombreArch = nombreArch + "-" + adsItem.Key;
                        }
                    }
                    nombreArch = nombreArch + ".dat";
                }
                else { nombreArch = archFinal; }
                MFT_ENTRY infoRecord = new MFT_ENTRY(refRecord);
                if (infoRecord.valFileFlags == 1 || infoRecord.valFileFlags == 5 || infoRecord.valFileFlags == 0)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE();
                    while (infoRecord.attributeSig != END_RECORD_SIG)
                    {
                        if (infoRecord.attributeSig == AL_SIG) //Busco el DATA en la lista
                        {
                            infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                            if (infoRecord.attributeNonResident == 0)
                            {
                                Int16 prevAttributeLength = infoRecord.attributeLength;
                                Int32 prevOffsetToAttribute = infoRecord.offsetToAttribute;
                                infoRecord.attributeLength = infoRecord.attributeContentOffset;
                                ProcessAttrListParaCopia(infoRecord, Convert.ToInt32(infoRecord.attributeContentLength), attIDBuscado);
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
                                        ProcessAttrListParaCopia(infoRecord, contentLength, attIDBuscado);
                                    }
                                    dataRunlist.NEXTDATARUNLIST(prevRawRecord[dataRunlist.runlistOffset]);
                                }
                                infoRecord.rawRecord = prevRawRecord;
                                infoRecord.attributeLength = prevAttributeLength;
                                infoRecord.attributeSig = 0x20;
                                infoRecord.offsetToAttribute = prevOffsetToAttribute;
                            }
                            if (copiado)
                            {
                                if (diccDatosCopia[infoRecord.attrListStartVCN].isResident) //Si el contenido era residente lo escribo
                                {
                                    File.WriteAllBytes(nombreArch, diccDatosCopia[infoRecord.attrListStartVCN].contentResident);
                                }
                                else // PARSEO EL diccDatosCopia y ya tengo el DATA copiado
                                {
                                    ulong sizeArchivo = 0;
                                    Int32 elementos = diccDatosCopia.Count;
                                    int n = 0;
                                    foreach (KeyValuePair<ulong, dataParaCopia> datarun in diccDatosCopia)
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
                                            CopiaNoResidentDATA(infoRecordDatarun, n, elementos, sizeArchivo, nombreArch, ref llevoCopiado);
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
                                if (infoRecord.attributeNonResident == 1)
                                {
                                    ulong sizeArchivo = BitConverter.ToUInt64(infoRecord.rawRecord, infoRecord.offsetToAttribute + 48);
                                    CopiaNoResidentDATA(infoRecord, 0, 0, sizeArchivo, nombreArch, ref llevoCopiado);
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
                        //case 0:
                        //    Console.WriteLine("\nNot suported: deleted file");
                        //    break;
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

        public static void CopiaNoResidentDATA(MFT_ENTRY infoRecord, Int32 n, Int32 elementos, ulong sizeArchivo, string nombreArch, ref ulong llevoCopiado) //
        {
            uint bytesxCluster = sectorxCluster * bytesxSector;
            Int32 sizeCachos = 65536; // 65536 131072 262144 524288
            GETDATARUNLIST dataRunlist = new GETDATARUNLIST(infoRecord);
            while (dataRunlist.runlist != (byte)(0x00))
            {
                dataRunlist.GETCLUSTERS(infoRecord);
                Console.WriteLine("Writing run length: {0} [Real file size: {1} bytes].", (dataRunlist.runLength * bytesxCluster).ToString("N0"), sizeArchivo.ToString("N0"));
                ulong count = 0;
                if (!dataRunlist.isSparse)
                {
                    UInt32 trozos = Convert.ToUInt32(sizeCachos) * bytesxCluster;
                    if ((sizeArchivo - llevoCopiado) >= (dataRunlist.runLength * bytesxCluster))
                    {
                        Int64 aux = Convert.ToInt64(dataRunlist.runLength) - sizeCachos;
                        llevoCopiado = llevoCopiado + Convert.ToUInt64(dataRunlist.runLength * bytesxCluster);
                        ulong offsetParcial = 0;
                        byte[] buscados = new byte[sizeCachos * bytesxCluster];
                        while (aux > 0) // Copia de archivos enormes en trozos
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
                            // large objects belong to generation 2: https://msdn.microsoft.com/en-us/magazine/cc534993.aspx
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
                        ulong pendiente = sizeArchivo - llevoCopiado;
                        ulong offsetParcial = 0;
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
                            if (count == 10)
                            {
                                GC.Collect(2); //Experimentalmente parece que acumula al llegar a esta repeticion
                            } // large objects belong to generation 2: https://msdn.microsoft.com/en-us/magazine/cc534993.aspx
                        }
                        buscados = null;
                        ulong remanente = pendiente % bytesxCluster;
                        ulong ultimoClusterOffset = dataRunlist.offsetBytesMFT + count * Convert.ToUInt64(trozos) + pendiente - remanente;
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

        public static void ProcessAttrListParaCopia(MFT_ENTRY infoRecord, Int32 contentLength, UInt16 attIDBuscado) //
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
                    byte id = infoRecord.rawRecord[infoRecord.offsetToAttribute + 24];
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
                                    ulong fileSize;
                                    if (entryData.attributeNonResident == 0)
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

        public static void BuscaCoincidencias(uint runLength, ulong offsetBytesMFT, List<string> buscadasList) //
        {
            uint runLength_ = listaDataRunLength[listaDataOffset.IndexOf(offsetBytesMFT)];
            var posIni = offsetBytesMFT;
            uint pos = 0;
            uint clusterBytes = (sectorxCluster * bytesxSector);
            byte[] cluster = new byte[clusterBytes];
            byte[] entryInfo = new byte[1024];
            ulong byteActual = posIni;
            while (pos < runLength_)
            {
                cluster = ReadRaw(posIni + (pos * clusterBytes), clusterBytes);
                for (ulong n = 0; n < (clusterBytes / 1024); n++)
                {
                    Array.Copy(cluster, (int)(n * 1024), entryInfo, 0, 1024);
                    UInt32 mftSig = BitConverter.ToUInt32(entryInfo, 0);
                    if (mftSig != FILE_SIG) { continue; } //no valid record
                    MFT_ENTRY infoMFT = new MFT_ENTRY(entryInfo);
                    if (!infoMFT.recordValido)
                    {
                        Console.WriteLine("Record {0} has a wrong fixup value. Skipped.", infoMFT.recordNumber);
                        continue;
                    }
                    Busquedas(infoMFT, buscadasList);
                    #region antiguo
                    #endregion
                }
                pos += 1;
            }
        }

        public static void BuscaCoincidenciasO(uint runLength, ulong offsetBytesMFT, List<string> buscadasList) //
        {
            byte[] entryInfo = new byte[1024];
            int pos = 0;
            while (pos < tam)
            {
                entryInfo= readBin.ReadBytes(1024);
                UInt32 mftSig = BitConverter.ToUInt32(entryInfo, 0);
                if (mftSig != FILE_SIG)
                {
                    pos += 1024;
                    continue;
                }
                MFT_ENTRY infoMFT = new MFT_ENTRY(entryInfo);
                if (!infoMFT.recordValido)
                {
                    Console.WriteLine("Record {0} has a wrong fixup value. Skipped", infoMFT.recordNumber);
                    pos += 1024;
                    continue;
                }
                Busquedas(infoMFT, buscadasList);
                #region antiguo
                #endregion
                pos += 1024;
            }
        }

        public static void GetCoinciDetalles()
        {
            if ((CommandLine["tl"] != null) || (CommandLine["l2t"] != null))
            {
                if (!CommandLine.Parameters.ContainsKey("x"))
                {
                    Console.WriteLine(encabezadoT);
                }
            }
            foreach (UInt32 entryCoincid in refCoincid)
            {
                try
                {
                    GetPath.FileNameAndParentFrn localiza = GetPath.soloMFTDictOffsets[entryCoincid];
                    byte[] refRecord = ReadRaw(localiza.RecordOffset, 1024);
                    MFT_ENTRY infoEntryCoincid = new MFT_ENTRY(refRecord);
                    UInt32 baseRef = infoEntryCoincid.fileReferenceToBaseFile;
                    if ((baseRef != 0) && (infoEntryCoincid.valFileFlags != 0)) //si esta borrado no leo la referencia porque no es de fiar
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
                    Console.WriteLine("\nReading record {0}. Error: {1}", entryCoincid, ex.Message.ToString());
                }
            }
        }

        public static void BuscaCoincidenciasInfo(MFT_ENTRY infoRecord) //
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
                    Info_SI(infoRecord);
                }
                else if (infoRecord.attributeSig == AL_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    Info_AL(infoRecord);
                }
                else if (infoRecord.attributeSig == FN_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    Info_FN(infoRecord);
                }
                else if (infoRecord.attributeSig == DATA_SIG)
                {
                    infoRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                    Info_DATA(infoRecord);
                }
                infoRecord.MFT_NEXT_ATTRIBUTE();
            }

        }

        public class MFT_ENTRY
        {
            #region vars and constants
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
            public byte attributeNonResident;
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
            public Dictionary<string,string> deduplicarFNtimeline = new Dictionary<string,string>();
            public ulong realFileSize = 0;
            public ulong diskFileSize = 0;
            public string dataBase = "";
            public Dictionary<string,dataADSInfo> diccIDDataADSInfo = new Dictionary<string, dataADSInfo>();
            public ulong attrListStartVCN;
            private char[] macb = "M...".ToCharArray();
            public string calcSHA1 = "";

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

            #endregion vars and constants
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
                attributeNonResident = rawRecord[offsetToAttribute + A_IR_OFFSET];
                attributeNameLength = rawRecord[offsetToAttribute + A_NL_OFFSET];
                attributeNameOffset = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_NO_OFFSET);
                attributeID = BitConverter.ToUInt16(rawRecord, offsetToAttribute + A_ID_OFFSET);
                attributeContentLength = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_COL_OFFSET);
                attributeContentOffset = BitConverter.ToInt16(rawRecord, offsetToAttribute + A_COO_OFFSET);
            }

            public void MFT_SHOW_DATA()
            {
                Dictionary<string, char[]> dictioFechasSI = new Dictionary<string, char[]>();
                if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null) && (!CommandLine.Parameters.ContainsKey("s")))
                {
                    if (diccRecordHijos.ContainsKey(recordNumber))
                    {
                        Console.Write("\nRecord: {0}", recordNumber.ToString());
                        Console.Write(" [Attribute List points to records numbers:");
                        foreach (var rec in diccRecordHijos[recordNumber])
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
                    if ((valFileFlags == 0) || (valFileFlags == 2))
                    {
                        nombreFN[i] = string.Concat("?", nombreFN[i]);
                    }
                    if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null) && (!CommandLine.Parameters.ContainsKey("s")))
                    {
                        Console.WriteLine("{0}{1}{2}", fileFlags, "  ", nombreFN[i]);
                    }
                    longName = longName.Length < nombreFN[i].Length ? nombreFN[i] : longName;
                }
                if ((CommandLine["tl"] == null) && (CommandLine["l2t"] == null))
                {
                    if (!CommandLine.Parameters.ContainsKey("s"))
                    {
                        Console.WriteLine("SI[MACB]: {0}   {1}   {2}   {3}", dateModificado_SI, dateAccessed_SI, dateMFTModif_SI, dateCreated_SI);
                        for (int i = 0; i < dateCreated_FN.Count; i++)
                        {
                            Console.WriteLine("FN[MACB]: {0}   {1}   {2}   {3}", dateModificado_FN[i], dateAccessed_FN[i], dateMFTModif_FN[i], dateCreated_FN[i]);
                        }
                        Console.WriteLine("Reference: {0} [Size: {1} bytes|| Size on disk: {2} bytes]", dataBase, realFileSize.ToString("N0"), diskFileSize.ToString("N0"));
                        if (CommandLine.Parameters.ContainsKey("x") && (fileFlags == "[File]"))
                        {
                            writer.Write(dataBase + "\t" + longName + "\n");
                        }
                        foreach (var datas in diccIDDataADSInfo)
                        {
                            Console.WriteLine("[ADS] Name: {0} [Reference: {1} || Size: {2} bytes]", datas.Value.name, datas.Key, datas.Value.size.ToString("N0"));
                            if (CommandLine.Parameters.ContainsKey("x"))
                            {
                                writer.Write(datas.Key + "\t" + longName + ":" + datas.Value.name + "\n");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(longName);
                    }
                }
                else
                {
                    calcSHA1 = "";
                    bool enRango = false;
                    if ((string.Compare(desdeCuando, dateCreated_SI) <= 0) && (string.Compare(hastaCuando, dateCreated_SI) >= 0))
                    {
                        enRango = true;
                    }
                    else
                    {
                        for (int i = 0; i < dateCreated_FN.Count; i++)
                        {
                            if ((string.Compare(desdeCuando, dateCreated_FN[i]) <= 0) && (string.Compare(hastaCuando, dateCreated_FN[i]) >= 0))
                            {
                                enRango = true;
                                break;
                            }
                        }
                    }
                    if (enRango)
                    {
                        if (fileFlags == "[File]")
                        {
                            if (CommandLine.Parameters.ContainsKey("x"))
                            {
                                writer.Write(dataBase + "\t" + longName + "\n");
                            }
                            if (CommandLine["sha1"] != null)
                            {
                                calcSHA1 = HashSHA1(longName);
                            }
                        }
                        imprimeFechas(ref dictioFechasSI, "SI", longName, dateModificado_SI, dateAccessed_SI, dateMFTModif_SI, dateCreated_SI);
                        deduplicarFNtimeline.Clear();
                        for (int i = 0; i < dateCreated_FN.Count; i++)
                        {
                            Dictionary<string, char[]> dictioFechas = new Dictionary<string, char[]>();
                            imprimeFechas(ref dictioFechas, "FN", longName, dateModificado_FN[i], dateAccessed_FN[i], dateMFTModif_FN[i], dateCreated_FN[i]);
                        }
                        foreach (var datas in diccIDDataADSInfo)
                        {
                            var enumerator = dictioFechasSI.GetEnumerator();
                            while (enumerator.MoveNext())
                            {
                                var pair = enumerator.Current;
                                string[] fecha = pair.Key.Split(' ');
                                    if (CommandLine["tl"] != null)
                                    {
                                        Console.WriteLine("{0},{1},SI[{2}],{3}:{4},{5},{6},{7}", fecha[0], fecha[1], new string(pair.Value), longName, datas.Value.name, recordNumber, datas.Value.size.ToString("N0"), calcSHA1);
                                    }
                                    else
                                    {
                                        Console.WriteLine("{0},{1},UTC,[{2}],MFT_FILETIME,SI,{7},-,-,\"{3}:{4}\",\"{3}:{4}\",1,\"{3}:{4}\",{6},{8},-,Size: [{5} B]", fecha[0], fecha[1], new string(pair.Value), longName, datas.Value.name, datas.Value.size.ToString("G", System.Globalization.CultureInfo.InvariantCulture), recordNumber, fileFlags, calcSHA1);
                                    }
                            }
                        }
                    }
                }
            }


            public void imprimeFechas(ref Dictionary<string, char[]> dictioFechas, string tipoFecha, string _longName, string dateModificado, string dateAccessed, string dateMFTModif, string dateCreated)
            {
                dictioFechas.Add(dateModificado, macb);
                if (dictioFechas.ContainsKey(dateAccessed))
                {
                    dictioFechas[dateAccessed] = "MA..".ToCharArray();
                }
                else
                {
                    dictioFechas.Add(dateAccessed, ".A..".ToCharArray()); 
                }
                if (dictioFechas.ContainsKey(dateMFTModif))
                {
                    dictioFechas[dateMFTModif][2] = 'C';
                }
                else 
                {
                    dictioFechas.Add(dateMFTModif, "..C.".ToCharArray());
                }
                if (dictioFechas.ContainsKey(dateCreated))
                {
                    dictioFechas[dateCreated][3] = 'B';
                }
                else 
                {
                    dictioFechas.Add(dateCreated, "...B".ToCharArray());
                }
                var enumerator = dictioFechas.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var pair = enumerator.Current;
                    string tempo = string.Join("_", new string[] { pair.Key, tipoFecha, new string(pair.Value), _longName });
                    if (!deduplicarFNtimeline.ContainsKey(tempo))
                    {
                        string[] fecha = pair.Key.Split(' ');
                            if (CommandLine["tl"] != null)
                            {
                                Console.WriteLine("{0},{1},{2}[{3}],{4},{5},{6},{7}", fecha[0], fecha[1], tipoFecha, new string(pair.Value), _longName, recordNumber, realFileSize.ToString("N0"), calcSHA1);
                            }
                            else
                            {
                                Console.WriteLine("{0},{1},UTC,[{3}],MFT_FILETIME,{2},{7},-,-,\"{4}\",\"{4}\",1,\"{4}\",{6},{8},-,Size: [{5} B]", fecha[0], fecha[1], tipoFecha, new string(pair.Value), _longName, realFileSize.ToString("G", System.Globalization.CultureInfo.InvariantCulture), recordNumber, fileFlags, calcSHA1);
                            }
                        deduplicarFNtimeline.Add(tempo, "");
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
            public ulong sizeCopiar;
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
            public ulong size;

            public dataADSInfo(string name_, ulong size_)
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
            public ulong offsetBytesMFT;
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
                //Console.Write("\n-Nibbles: " + runlistHighNibble + " - " + runlistLowNibble);
                offsetBytesMFT = 0;
                isSparse = runlistHighNibble == 0 ? true : false; //sparse runlists
            }

            public void GETLISTS(MFT_ENTRY recordActual)
            {
                while (runlist != (byte)(0x00))
                {
                    GETCLUSTERS(recordActual);
                    if (!isSparse)
                    {
                        listaDataRunLength.Add(runLength);
                        listaDataOffset.Add(offsetBytesMFT);
                    }
                    NEXTDATARUNLIST(recordActual.rawRecord[runlistOffset]);
                }
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
                    //Console.Write(" -> runLength: " + runLength);
                    byte[] runOffsetLeido = new byte[runlistHighNibble];
                    Array.Copy(recordActual.rawRecord, runlistOffset + 1 + runlistLowNibble, runOffsetLeido, 0, runlistHighNibble);
                    if ((int)runOffsetLeido[runlistHighNibble - 1] > 127)
                    {
                        int runOffset;
                        Array.Copy(arrayNegativos, runOffsetComplt, 4);
                        Array.Copy(runOffsetLeido, runOffsetComplt, runlistHighNibble);
                        runOffset = BitConverter.ToInt32(runOffsetComplt, 0);
                        offsetBytesMFT = offsetBytesMFT + (ulong)(runOffset * bytesxSector * sectorxCluster);
                        //Console.WriteLine(" -> runOffset (neg): " + runOffset);
                    }
                    else
                    {
                        uint runOffset;
                        Array.Copy(arrayPositivos, runOffsetComplt, 4);
                        Array.Copy(runOffsetLeido, runOffsetComplt, runlistHighNibble);
                        runOffset = BitConverter.ToUInt32(runOffsetComplt, 0);
                        offsetBytesMFT = offsetBytesMFT + (ulong)runOffset * (ulong)bytesxSector * (ulong)sectorxCluster;
                        //Console.WriteLine(" -> runOffset: " + runOffset);
                    }
                }
                runlistOffset = runlistOffset + 1 + runlistLowNibble + runlistHighNibble;
            }

            public void NEXTDATARUNLIST(byte newRunlist)
            {
                runlist = newRunlist;
                runlistLowNibble = (byte)(runlist & 0x0F);
                runlistHighNibble = (byte)((runlist & 0xF0) >> 4);
                //Console.Write("\n-Nibbles: " + runlistHighNibble + " - " + runlistLowNibble);
                isSparse = runlistHighNibble == 0 ? true : false; //sparse runlists
            }
        }

        public static void Info_SI(MFT_ENTRY entryData)
        {
            int datesOffset = entryData.offsetToAttribute + entryData.attributeContentOffset;
            entryData.dateCreated_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 4), BitConverter.ToUInt32(entryData.rawRecord, datesOffset));
            entryData.dateModificado_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 12), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 8));
            entryData.dateMFTModif_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 20), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 16));
            entryData.dateAccessed_SI = GetDateTimeFromFiletime((long)BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 28), BitConverter.ToUInt32(entryData.rawRecord, datesOffset + 24));
        }

        public static void Info_AL(MFT_ENTRY entryData)
        {
            if (entryData.attributeNonResident == 0)
            {
                Int16 prevAttributeLength = entryData.attributeLength;
                Int32 prevOffsetToAttribute = entryData.offsetToAttribute;
                entryData.attributeLength = entryData.attributeContentOffset;
                ProcesaAttrList(entryData, Convert.ToInt32(entryData.attributeContentLength));
                entryData.attributeLength = prevAttributeLength;
                entryData.attributeSig = 0x20;
                entryData.offsetToAttribute = prevOffsetToAttribute;
            }
            else if ((entryData.valFileFlags != 0) && (entryData.valFileFlags != 2))
            {
                if (CommandLine["o"] == null)
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
                            ProcesaAttrList(entryData, contentLength);
                        }
                        dataRunlist.NEXTDATARUNLIST(prevRawRecord[dataRunlist.runlistOffset]);
                    }
                    entryData.rawRecord = prevRawRecord;
                    entryData.attributeLength = prevAttributeLength;
                    entryData.attributeSig = 0x20;
                    entryData.offsetToAttribute = prevOffsetToAttribute;
                    prevRawRecord = null;
                }
                else
                {
                    foreach (var hijo in diccRecordHijos[entryData.recordNumber])
                    {
                        GetPath.FileNameAndParentFrn localiza = GetPath.soloMFTDictOffsets[hijo];
                        byte[] refRecord = ReadRaw(localiza.RecordOffset, 1024);
                        MFT_ENTRY infoEntryCoincid = new MFT_ENTRY(refRecord);
                        GetCoinciDetallesInfo(infoEntryCoincid);
                        infoEntryCoincid.MFT_SHOW_DATA();
                    }
                }
            }
        }

        public static void ProcesaAttrList(MFT_ENTRY entryData, Int32 contentLength)
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
                        //***
                        byte[] refRecord = ReadRaw(localiza.RecordOffset, 1024);
                        MFT_ENTRY infoRefRecord = new MFT_ENTRY(refRecord);
                        infoRefRecord.MFT_NEXT_ATTRIBUTE();
                        while (infoRefRecord.attributeSig != END_RECORD_SIG)
                        {
                            if (infoRefRecord.attributeSig == FN_SIG)
                            {
                                infoRefRecord.MFT_NEXT_ATTRIBUTE_VALIDO();
                                Info_FN(infoRefRecord);
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
                                Info_DATA(infoRefRecord);
                            }
                            infoRefRecord.MFT_NEXT_ATTRIBUTE();
                        }
                        if ((entryData.dataBase.Length == 0) & (infoRefRecord.dataBase.Length != 0))
                        { 
                            entryData.dataBase = entryData.recordNumber + infoRefRecord.dataBase.Substring(infoRefRecord.dataBase.IndexOf(":"));
                        }
                        if (entryData.realFileSize < infoRefRecord.realFileSize)
                        {
                            entryData.realFileSize = infoRefRecord.realFileSize;
                            entryData.diskFileSize = infoRefRecord.diskFileSize;
                        }
                        foreach (var resultados in infoRefRecord.diccIDDataADSInfo)
                        { 
                            dataADSInfo esta;
                            if (entryData.diccIDDataADSInfo.TryGetValue(entryData.recordNumber + resultados.Key.Substring(resultados.Key.IndexOf(":")), out esta))
                            {
                                entryData.diccIDDataADSInfo[entryData.recordNumber + resultados.Key.Substring(resultados.Key.IndexOf(":"))].size = esta.size + resultados.Value.size;
                            }
                            else
                            {
                                entryData.diccIDDataADSInfo.Add(entryData.recordNumber + resultados.Key.Substring(resultados.Key.IndexOf(":")), resultados.Value);
                            }
                        }
                        entryData.attributeLength = intprevAttributeLength;
                        entryData.attributeSig = 0x30;
                        entryData.offsetToAttribute = intprevOffsetToAttribute;
                        refRecord = null;
                    }
                }
            }
        }

        public static void Info_FN(MFT_ENTRY entryData)
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
            ulong fileSize;
            ulong fileSizeOnDisk;
            byte[] tempSize = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            Array.Copy(entryData.rawRecord, datesOffset + 32, tempSize, 0, 8);
            fileSizeOnDisk = BitConverter.ToUInt64(tempSize, 0);
            Array.Copy(entryData.rawRecord, datesOffset + 40, tempSize, 0, 8);
            fileSize = BitConverter.ToUInt64(tempSize, 0);
            if (entryData.realFileSize < fileSize)
            {
                entryData.realFileSize = fileSize;
                entryData.diskFileSize = fileSizeOnDisk;
            }
        }

        public static void Info_DATA(MFT_ENTRY entryData)
        {
            if (entryData.attributeNameLength != 0) //Solo para los ADS,s
            {
                byte adsNameLen = entryData.rawRecord[entryData.offsetToAttribute + 9];
                UInt16 adsNameOffset = entryData.rawRecord[entryData.offsetToAttribute + 10];
                string nombreEncontrado = Encoding.Unicode.GetString(entryData.rawRecord, entryData.offsetToAttribute + adsNameOffset, adsNameLen * 2);
                string idData = entryData.recordNumber.ToString() +  ":128-" + entryData.attributeID.ToString();
                ulong fileSize;
                ulong fileSizeOnDisk;
                if (!entryData.diccIDDataADSInfo.ContainsKey(idData))
                {
                    entryData.diccIDDataADSInfo.Add(idData, new dataADSInfo(nombreEncontrado, 0));
                    if (entryData.attributeNonResident == 0)
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
                ulong fileSize;
                ulong fileSizeOnDisk;
                if (entryData.attributeNonResident == 0)
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
            string formato = "yyyy'/'MM'/'dd HH:mm:ss.fffffff";
            long returnDateTime = highBytes << 32;
            returnDateTime = returnDateTime | lowBytes;
            if (returnDateTime >= DateTime.MinValue.Ticks && returnDateTime <= DateTime.MaxValue.Ticks)
            {
                return DateTime.FromFileTimeUtc(returnDateTime).ToString(formato);
            }
            else
            {
                return "No_valid_date No_valid_time";
            }
        }

        public static void CompruFNRecordBase(uint fileReferenceToBaseFile_, uint recordNumber_, ref List<uint>refCoincid) {
            if (fileReferenceToBaseFile_ == 0) //Comprobacion para casos donde el nombre FN no está en el record base
            {
                if (!refCoincid.Contains(recordNumber_))
                {
                    refCoincid.Add(recordNumber_);
                }
            }
            else
            {
                if (!refCoincid.Contains(fileReferenceToBaseFile_))
                {
                    refCoincid.Add(fileReferenceToBaseFile_);
                }
            }
        }

        public static void Busquedas(MFT_ENTRY infoMFT, List<string> buscadasList)
        {
            // Según parece es más rápido http://cc.davelozinski.com/c-sharp/fastest-way-to-check-if-a-string-occurs-within-a-string 
            //(((infoMFT.nombres.Length - (infoMFT.nombres.Replace(nombreBuscado.ToLower(), String.Empty)).Length) / nombreBuscado.Length) > 0)
            // que hacer:
            //if (infoMFT.nombres.Contains(nombreBuscado.ToLower()))
            BuscaCoincidenciasInfo(infoMFT);
            bool result = false;
            foreach (string nombreBuscado in buscadasList)
            {
                todo = false;
                bool incluyePath = false;
                string pathBuscado = "";
                string nombreArchivo = nombreBuscado;
                string nombPath = "";
                string auxNombreBuscado = nombreBuscado;
                if (auxNombreBuscado.LastIndexOf("\\") > 1)  //<-----------------------------------OJO ------------------------------------
                {
                    incluyePath = true;
                    pathBuscado = auxNombreBuscado.Substring(0, auxNombreBuscado.LastIndexOf("\\")).Replace("\\", String.Empty).ToLower();
                    nombreArchivo = auxNombreBuscado.Substring(auxNombreBuscado.LastIndexOf("\\") + 1, auxNombreBuscado.Length - auxNombreBuscado.LastIndexOf("\\") - 1);
                }
                if (nombreArchivo == "*") { todo = true; }
                int countSubdirs = nombreBuscado.Split('\\').Length - 1 + recursion;
                for (int i = 0; i < infoMFT.nombreFN.Count; i++)
                {
                    if (result) { break; }
                    if ((auxNombreBuscado.EndsWith("<")) || (auxNombreBuscado.EndsWith("/")))
                    {
                        string newNombreBuscado = nombreBuscado.Replace("<", String.Empty);
                        if (CommandLine["fd"] != null)
                        {
                            bool pathCorrecto = false;
                            string pathNombre = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).ToLower();
                            if ((newNombreBuscado.Split('\\').Length <= (pathNombre.Split('\\').Length) + 1) && ((pathNombre.Split('\\').Length - 1) <= countSubdirs))
                            {
                                newNombreBuscado = newNombreBuscado + "\\";
                                if (pathNombre == "\\\\") pathNombre = pathNombre + infoMFT.nombreFN[i] + "\\";
                                else pathNombre = pathNombre + "\\" + infoMFT.nombreFN[i] + "\\";
                                if (((pathNombre.Length - (pathNombre.ToLower().Replace(newNombreBuscado.ToLower(), String.Empty)).Length) / newNombreBuscado.Length) > 0) pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    CompruFNRecordBase(infoMFT.fileReferenceToBaseFile, infoMFT.recordNumber, ref refCoincid);
                                }
                            }
                        }
                        else
                        {
                            bool encontr = false;
                            if ((auxNombreBuscado.EndsWith("<")) && (infoMFT.nombreFN[i].ToLower() == nombreArchivo.Replace("<", String.Empty).ToLower())) encontr = true;
                            else if (infoMFT.nombreFN[i].ToLower().EndsWith(nombreArchivo.Replace("/", String.Empty).ToLower())) encontr = true;
                            if (encontr)
                            {
                                bool pathCorrecto = false;
                                if (incluyePath)
                                {
                                    nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\", String.Empty).ToLower();
                                    if (((nombPath.Length - (nombPath.Replace(pathBuscado, String.Empty)).Length) / pathBuscado.Length) > 0) pathCorrecto = true;
                                }
                                else pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    if (infoMFT.valFileFlags != 0) CompruFNRecordBase(infoMFT.fileReferenceToBaseFile, infoMFT.recordNumber, ref refCoincid);
                                    else // archivo borrado y la info de referencia es cuestionable
                                    {
                                        if (!refCoincid.Contains(infoMFT.recordNumber)) refCoincid.Add(infoMFT.recordNumber);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (CommandLine["fd"] != null)
                        {
                            bool pathCorrecto = false;
                            string pathNombre = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).ToLower();
                            if ((nombreBuscado.Split('\\').Length <= (pathNombre.Split('\\').Length + 1)) & ((pathNombre.Split('\\').Length - 1) <= countSubdirs))
                            {
                                if (pathNombre == "\\\\") pathNombre = pathNombre + infoMFT.nombreFN[i];
                                else pathNombre = pathNombre + "\\" + infoMFT.nombreFN[i];
                                if (((pathNombre.Length - (pathNombre.ToLower().Replace(nombreBuscado.ToLower(), String.Empty)).Length) / nombreBuscado.Length) > 0) pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    CompruFNRecordBase(infoMFT.fileReferenceToBaseFile, infoMFT.recordNumber, ref refCoincid);
                                }
                            }
                        }
                        else
                        {
                            if (todo || (((infoMFT.nombreFN[i].Length - (infoMFT.nombreFN[i].ToLower().Replace(nombreArchivo.ToLower(), String.Empty)).Length) / nombreArchivo.Length) > 0))
                            {
                                bool pathCorrecto = false;
                                if (incluyePath)
                                {
                                    nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\", String.Empty).ToLower();
                                    if (((nombPath.Length - (nombPath.Replace(pathBuscado, String.Empty)).Length) / pathBuscado.Length) > 0) pathCorrecto = true;
                                }
                                else pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    if (infoMFT.valFileFlags != 0) CompruFNRecordBase(infoMFT.fileReferenceToBaseFile, infoMFT.recordNumber, ref refCoincid);
                                    else // archivo borrado y la info de referencia es cuestionable
                                    {
                                        if (!refCoincid.Contains(infoMFT.recordNumber)) refCoincid.Add(infoMFT.recordNumber);
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
                        if ((nombreArchivo.EndsWith("<")) || (nombreArchivo.EndsWith("/")))
                        {
                            string newNombreBuscado = nombreArchivo.Replace("<", String.Empty);
                            bool encontr = false;
                            if ((nombreArchivo.EndsWith("<")) && (datas.Value.name.ToLower() == newNombreBuscado.ToLower())) encontr = true;
                            else if (datas.Value.name.ToLower().EndsWith(nombreArchivo.Replace("/", String.Empty).ToLower())) encontr = true;
                            if (encontr)
                            {
                                bool pathCorrecto = false;
                                if (incluyePath)
                                {
                                    nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\", String.Empty).ToLower();
                                    if (((nombPath.Length - (nombPath.Replace(pathBuscado, String.Empty)).Length) / pathBuscado.Length) > 0) pathCorrecto = true;
                                }
                                else pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    if (infoMFT.valFileFlags != 0) CompruFNRecordBase(infoMFT.fileReferenceToBaseFile, infoMFT.recordNumber, ref refCoincid);
                                    else
                                    {
                                        if (!refCoincid.Contains(infoMFT.recordNumber)) refCoincid.Add(infoMFT.recordNumber);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (todo || (((datas.Value.name.Length - (datas.Value.name.ToLower().Replace(nombreArchivo.ToLower(), String.Empty)).Length) / nombreArchivo.Length) > 0))
                            {
                                bool pathCorrecto = false;
                                if (incluyePath)
                                {
                                    nombPath = GetPath.soloMFTGetFullyQualifiedPath(infoMFT.parentDirectoryFN).Replace("\\", String.Empty).ToLower();
                                    if (((nombPath.Length - (nombPath.Replace(pathBuscado, String.Empty)).Length) / pathBuscado.Length) > 0) pathCorrecto = true;
                                }
                                else pathCorrecto = true;
                                if (pathCorrecto)
                                {
                                    result = true;
                                    if (infoMFT.valFileFlags != 0) CompruFNRecordBase(infoMFT.fileReferenceToBaseFile, infoMFT.recordNumber, ref refCoincid);
                                    else
                                    {
                                        if (!refCoincid.Contains(infoMFT.recordNumber)) refCoincid.Add(infoMFT.recordNumber);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static string HashSHA1(string filepath)
        {
            try
            {
                using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                {
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        byte[] hash = sha1.ComputeHash(fs);
                        string checksumSHA1 = BitConverter.ToString(hash).Replace("-", string.Empty);
                        return checksumSHA1;
                    }
                }
            }
            catch(Exception e)
            {
                return "Hashing failed. Exception: " + e.Message;
            }
        }

        public static string LaAyuda() {
            return (@"mftf.exe v.2.6
The tool can parse the $MFT from a live system, from a mounted (read-only
included) logical drive or from a copy of the $MFT.
It can copy files or ADS,s using the references provided in the results.
The copy is made by reading the data from the clusters so that you can copy
protected system files or files in use.
Deleted files and folders have their path with the prefix ""?"".

Copyright 2015 Ignacio Perez
nachpj@gmail.com

Licensed under the Apache License, Version 2.0 (the ""License"");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an ""AS IS"" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

Usage: mftf.exe SOURCE ACTIONS [OPTIONS]
 1. SOURCE:
            -d drive_letter       ----- Logical unit.
            -o MFT_file           ----- Offline $MFT file.
 2. ACTIONS:
 2.1. COPY. Physical level copy of files (no file level locking)
 2.1.1. ACTION: Copy by filename
        -cp src_file -n dest_file
 2.1.2. ACTION: Copy file by reference
        -cr ""ref1[|ref2..]""    -----| Copy the referenced file/ads to this folder.
                                    | Use | as separator.
        -cl list.txt        -----| Copy all the files referenced in the file list.txt.
                                 | Each line MUST start with: reference + [TAB].
 2.2. SEARCH:
 2.2.1. ACTION: Logical string search:
        -f ""string1|string2 with spaces|string3<""
        -f ""folder\string""
        -f ""folder\*""
                     | The results are filtered using the string ""folder"".
                     | The match is always case insensitive.
                     | "" as delimiters for the whole group of strings.
                     | | is the separator.
                     | < at the end of ""string"" for an exact coincidence.
                     | Use * to search any string 
        -ff file.txt   ----- The strings to search for are in file.txt.
                             One string per line. You can use <.
 2.2.2. ACTION: Raw search
        -fr string     ----- Search in the 1024 bytes of each MFT record.
 2.2.3. ACTION: Root based search: files and folders under the tree
        -fd ""\\Dir1\dir2|\\Dir1\dir3<"" 
                      | It will match any directories like dir2...
                      | Use < for an exact coincidence.
                      | Use | as separator
        -r N     -----  Recursion level (number). Default is 0.
 2.2.4. ACTION: ADS,s search
        -fads         ----- Find all the ADS,s.
 2.2.5. Search OPTIONS:       
       > Timeline mode: if no search is specified the entire MFT will be in the output. Two formats available:
         -tl 
            Format: Date  Time  [MACB]  filename  record  size
         -l2t
            Format: date,time,timezone,MACB,source,sourcetype,type,user,host,short,desc,version,filename,inode,notes,format,extra
         [-tf yyyy/MM/dd ] Filter from this date
         [-tt yyyy/MM/dd ] to this date
         [-sha1] Add the SHA1 to the output
      > No timeline mode:
        -x     - Save the results in a file in order to use the option -cl.
        -s     - Display only the file name.

 2.3. RETRIEVE INFORMATION:
        -i record_number    -----  Show information of the MFT record.
        -ip path_to_file    -----  Show information of the MFT record.
        -w record_number    -----  Write on screen the 1024 bytes of the MFT record.

 2.4. COPY MFT RECORDS:
        -cn record_number     Copy the 1024 bytes of the MFT record to this folder.

 2.5. Help: 
        -h

Examples:
> mftf.exe -cp c:\$MFT -n d:\maleta\mft.bin
> mftf.exe -o mft.bin -f ""svchost"" -tl -tf ""2015/10/18"" -tt ""2016/01/25""
> mftf.exe -d e -f ""svchost|mvui.dll|string with spaces|exact match<"" -l2t
> mftf.exe -d e -cr 4292:128-1");
        }
    }
}
