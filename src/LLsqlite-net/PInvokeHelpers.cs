using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace PInvokeHelpers
{
    /// <summary>
    /// Container for PInvoke definitions.
    /// </summary>
    static public class PInvokeDefs
    {
        public delegate bool EnumResTypeProc(IntPtr hModule, string lpszType, IntPtr lParam);
        public delegate bool EnumResLangDelegate(IntPtr hModule, string lpszType, string lpszName, ushort wIDLanguage, IntPtr lParam);
        public delegate bool EnumResNameProcDelegate(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

        public enum ResType
        {
            CURSOR = 1,
            BITMAP = 2,
            ICON = 3,
            MENU = 4,
            DIALOG = 5,
            STRING = 6,
            FONTDIR = 7,
            FONT = 8,
            ACCELERATOR = 9,
            RCDATA = 10,
            MESSAGETABLE = 11,
            GROUP_CURSOR = 12,
            GROUP_ICON = 14,
            VERSION = 16,
            DLGINCLUDE = 17,
            PLUGPLAY = 19,
            VXD = 20,
            ANICURSOR = 21,
            ANIICON = 22,
            HTML = 23,
            MANIFEST = 24
        }

        [DllImport("kernel32.dll")]
        public static extern bool EnumResourceTypes(IntPtr hModule, EnumResTypeProc lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern bool EnumResourceLanguages(IntPtr hModule, string lpszType, string lpName, EnumResLangDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern bool EnumResourceNames(IntPtr hModule, string lpszType, EnumResNameProcDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern bool EnumResourceNames(IntPtr hModule, int dwID, EnumResNameProcDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll")]
        public static extern IntPtr FindResource(IntPtr hModule, int lpName, int lpType);

        [DllImport("kernel32.dll")]
        public static extern IntPtr FindResource(IntPtr hModule, int lpName, string lpType);

        [DllImport("kernel32.dll")]
        public static extern IntPtr FindResource(IntPtr hModule, string lpName, int lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);

        [DllImport("kernel32.dll")]
        public static extern IntPtr FindResourceEx(IntPtr hModule, IntPtr lpType, IntPtr lpName, ushort wLanguage);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
        public static extern System.Boolean SetThreadPreferredUILanguages(
            System.UInt32 dwFlags,
            System.String pwszLanguagesBuffer,
            ref System.UInt32 pulNumLanguages
            );

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
        public static extern System.Boolean GetThreadPreferredUILanguages(
            System.UInt32 dwFlags,
            ref System.UInt32 pulNumLanguages,
            System.IntPtr pwszLanguagesBuffer,
            ref System.UInt32 pcchLanguagesBuffer
            );
    }
}
