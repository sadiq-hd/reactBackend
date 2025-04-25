using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace reactBackend.Services
{
    // فئة مساعدة لتحميل المكتبات غير المُدارة
    public class CustomAssemblyLoadContext : AssemblyLoadContext
    {
        public IntPtr LoadUnmanagedLibrary(string absolutePath)
        {
            return LoadUnmanagedDll(absolutePath);
        }
        
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            return NativeLibrary.Load(unmanagedDllName);
        }
    }
}