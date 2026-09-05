using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FileOrgy.Core.Services
{
    public static class WindowsRecycleBin
    {
        private const int FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_SILENT = 0x0004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string? pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string? lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        /// <summary>
        /// Sends a file or directory to the Windows Recycle Bin safely.
        /// </summary>
        public static bool SendToRecycleBin(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return false;
            }

            try
            {
                // SHFileOperation requires a double null-terminated string
                string doubleNullPath = Path.GetFullPath(path) + '\0' + '\0';

                var fileOp = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = doubleNullPath,
                    pTo = null,
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT,
                    fAnyOperationsAborted = false,
                    hNameMappings = IntPtr.Zero,
                    lpszProgressTitle = null
                };

                int result = SHFileOperation(ref fileOp);
                return result == 0 && !fileOp.fAnyOperationsAborted;
            }
            catch
            {
                // Fallback if P/Invoke fails (e.g. network share that doesn't support recycle bin)
                return false;
            }
        }

        /// <summary>
        /// Permanently deletes a file or directory.
        /// </summary>
        public static bool PermanentDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
