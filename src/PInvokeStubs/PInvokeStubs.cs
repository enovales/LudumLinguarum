using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PInvoke;

namespace PInvokeStubs
{
    /// <summary>
    /// Stubs for PInvoke wrappers that can't be invoked directly from F# due to
    /// use of void * (which cannot be replaced with nativeptr of unit).
    /// </summary>
    public class PInvokeStubs
    {
        public class EnumResourceLanguagesCallback
        {
            public List<Kernel32.LANGID> Languages = new List<Kernel32.LANGID>();

            unsafe public bool EnumResLangProc(IntPtr hModule, char* lpType, char* lpName, Kernel32.LANGID wLanguage, void* lParam)
            {
                Languages.Add(wLanguage);
                return true;
            }
        }
        /// <summary>
        /// Wrapper for PInvoke.Kernel32.EnumResourceLanguages.
        /// </summary>
        /// <param name="hModule">handle to the module containing the resources</param>
        /// <param name="lpType">resource type</param>
        /// <param name="lpName">resource name, or output of MAKEINTRESOURCE</param>
        /// <param name="lpEnumFunc">callback for enumeration</param>
        /// <param name="lParam">user-defined parameter for enumeration callback</param>
        /// <returns></returns>
        static unsafe public bool EnumResourceLanguages(Kernel32.SafeLibraryHandle hModule, char* lpType, char* lpName, EnumResourceLanguagesCallback lpEnumFunc)
        {
            return Kernel32.EnumResourceLanguages(hModule, lpType, lpName, new Kernel32.EnumResLangProc(lpEnumFunc.EnumResLangProc), null);
        }
    }
}
