using ResoniteModLoader;
using System.Runtime.InteropServices;
using System;

namespace Varjo4Reso
{
    class HookProc
    {
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private const int GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
        private const int GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT = 0x00000002;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetCurrentProcessIdDelegate();

        private static GetCurrentProcessIdDelegate originalGetCurrentProcessId;

        public static void HookGetCurrentProcessId()
        {
            IntPtr kernel32Handle = GetModuleHandle("kernel32.dll");
            IntPtr procAddress = GetProcAddress(kernel32Handle, "GetCurrentProcessId");

            originalGetCurrentProcessId = Marshal.GetDelegateForFunctionPointer<GetCurrentProcessIdDelegate>(procAddress);

            VirtualProtect(procAddress, (uint)IntPtr.Size, PAGE_EXECUTE_READWRITE, out uint oldProtect);
            
            Marshal.WriteIntPtr(procAddress, Marshal.GetFunctionPointerForDelegate(new GetCurrentProcessIdDelegate(GetCurrentProcessId_Hook)));
            //add back the memory protection.
            VirtualProtect(procAddress, (uint)IntPtr.Size, oldProtect, out _);
        }

        private static uint GetCurrentProcessId_Hook()
        {
            IntPtr returnAddr = GetReturnAddress();
            IntPtr callerModule;

            if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, returnAddr, out callerModule))
            {
                IntPtr libVarjo = GetModuleHandleA("VarjoLib.dll");
                IntPtr libVarjoRuntime = GetModuleHandleA("VarjoRuntime.dll");

                if (callerModule == libVarjo || callerModule == libVarjoRuntime)
                {
                    ResoniteMod.Warn("Hijacked Varjo's process ID call! All good");
                    return originalGetCurrentProcessId() + 42;
                }
            }

            return originalGetCurrentProcessId();
        }

        #region Internal helper methods and DllImports

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandleA(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetModuleHandleExA(int dwFlags, IntPtr ModuleName, out IntPtr phModule);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        private static IntPtr GetReturnAddress()
        {
            
            throw new NotImplementedException("Return address retrieval logic needed here.");
        }

        #endregion
    }
}