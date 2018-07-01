using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;

namespace MFT_fileoper
{
    class PInvokeWin32
    {
        public const UInt32 FSCTL_ENUM_USN_DATA = 0x000900b3;
        public const UInt32 FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        public const UInt32 FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        public const UInt32 FSCTL_CREATE_USN_JOURNAL = 0x000900e7;
        public const UInt32 FSCTL_QUERY_USN_JOURNAL = 0x000900f4;

        public const UInt32 GENERIC_READ = 0x80000000;
        public const UInt32 GENERIC_WRITE = 0x40000000;
        public const UInt32 FILE_SHARE_READ = 0x00000001;
        public const UInt32 FILE_SHARE_WRITE = 0x00000002;
        public const UInt32 OPEN_EXISTING = 3;
        public const Int32 INVALID_HANDLE_VALUE = -1;
        public const UInt32 FILE_BEGIN = 0x00000000;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
                                                  uint dwShareMode, IntPtr lpSecurityAttributes,
                                                  uint dwCreationDisposition, uint dwFlagsAndAttributes,
                                                  IntPtr hTemplateFile);

/// <summary>
/// para advanced format
/// </summary>
///
        [DllImport("kernel32")]
        public static extern bool DeviceIoControl
            (IntPtr deviceHandle, uint ioControlCode,
             IntPtr inBuffer, int inBufferSize,
             IntPtr outBuffer, int outBufferSize,
             ref int bytesReturned, IntPtr overlapped);

        public const int FSCTL_GET_NTFS_VOLUME_DATA = 0x00090064;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct NTFS_VOLUME_DATA_BUFFER
        {
            public UInt64 VolumeSerialNumber;
            public UInt64 NumberSectors;
            public UInt64 TotalClusters;
            public UInt64 FreeClusters;
            public UInt64 TotalReserved;
            public UInt32 BytesPerSector;
            public UInt32 BytesPerCluster;
            public UInt32 BytesPerFileRecordSegment;
            public UInt32 ClustersPerFileRecordSegment;
            public UInt64 MftValidDataLength;
            public UInt64 MftStartLcn;
            public UInt64 Mft2StartLcn;
            public UInt64 MftZoneStart;
            public UInt64 MftZoneEnd;
        }

/// <summary>
/// fin advanced format
/// </summary>

        // Used to read in a file

        [DllImport("kernel32.dll")]
        public static extern bool ReadFile(
            IntPtr hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        // Used to set the offset in file to start reading
        [DllImport("kernel32.dll")]
        public static extern bool SetFilePointerEx(
            IntPtr hFile,
            ulong liDistanceToMove,
            out long lpNewFilePointer,
            uint dwMoveMethod);


    }
}