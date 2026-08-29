using System;
using System.Runtime.InteropServices;

namespace CleanSweep.Core.Cleaners
{
    public class RecycleBinCleaner
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        enum RecycleFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, RecycleFlags dwFlags);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        public long GetEstimatedSize()
        {
            try
            {
                var rbInfo = new SHQUERYRBINFO();
                rbInfo.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                int hr = SHQueryRecycleBin(null, ref rbInfo);
                if (hr == 0) // S_OK
                {
                    return rbInfo.i64Size;
                }
            }
            catch { }
            return 0;
        }

        public long GetItemCount()
        {
            try
            {
                var rbInfo = new SHQUERYRBINFO();
                rbInfo.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                int hr = SHQueryRecycleBin(null, ref rbInfo);
                if (hr == 0)
                {
                    return rbInfo.i64NumItems;
                }
            }
            catch { }
            return 0;
        }

        public bool Clean(bool noConfirmation = false)
        {
            try
            {
                RecycleFlags flags = RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND;
                if (noConfirmation)
                {
                    flags |= RecycleFlags.SHERB_NOCONFIRMATION;
                }

                uint result = SHEmptyRecycleBin(IntPtr.Zero, null, flags);
                // S_OK is 0
                return result == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
