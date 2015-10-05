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

        [DllImport("kernel32.dll")]
        public static extern bool ReadFile(
            IntPtr hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll")]
        public static extern bool SetFilePointerEx(
            IntPtr hFile,
            ulong liDistanceToMove,
            out long lpNewFilePointer,
            uint dwMoveMethod);
    }
}