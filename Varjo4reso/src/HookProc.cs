using ResoniteModLoader;
using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
using Elements.Core;

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
            UniLog.Log("Starting the process of hooking GetCurrentProcessId...");

            try
            {
                IntPtr kernel32Handle = GetModuleHandle("kernel32.dll");
                if (kernel32Handle == IntPtr.Zero) throw new Exception("Failed to get handle for kernel32.dll.");
                UniLog.Log("Successfully obtained handle for kernel32.dll.");

                IntPtr procAddress = GetProcAddress(kernel32Handle, "GetCurrentProcessId");
                if (procAddress == IntPtr.Zero) throw new Exception("Failed to get address for GetCurrentProcessId.");
                UniLog.Log("Successfully obtained address for GetCurrentProcessId.");

                originalGetCurrentProcessId = Marshal.GetDelegateForFunctionPointer<GetCurrentProcessIdDelegate>(procAddress);
                UniLog.Log("Stored original GetCurrentProcessId function pointer.");

                VirtualProtect(procAddress, (uint)IntPtr.Size, PAGE_EXECUTE_READWRITE, out uint oldProtect);
                UniLog.Log("Changed memory protection to PAGE_EXECUTE_READWRITE.");

                Marshal.WriteIntPtr(procAddress, Marshal.GetFunctionPointerForDelegate(new GetCurrentProcessIdDelegate(GetCurrentProcessId_Hook)));
                UniLog.Log("Overwritten GetCurrentProcessId with custom hook.");

                // Restore the original memory protection
                VirtualProtect(procAddress, (uint)IntPtr.Size, oldProtect, out _);
                UniLog.Log("Restored original memory protection.");
            }
            catch (Exception ex)
            {
                ResoniteMod.Error($"Error during hooking: {ex.Message}");
                UniLog.Log($"Exception caught during hooking process: {ex.Message}");
            }

            UniLog.Log("Hooking process completed.");
        }

        private static uint GetCurrentProcessId_Hook()
        {
            UniLog.Log("GetCurrentProcessId_Hook called.");

            IntPtr returnAddr = GetReturnAddress();
            IntPtr callerModule;

            UniLog.Log("Determining the caller module of GetCurrentProcessId...");
            if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, returnAddr, out callerModule))
            {
                IntPtr libVarjo = GetModuleHandleA("VarjoLib.dll");
                IntPtr libVarjoRuntime = GetModuleHandleA("VarjoRuntime.dll");

                UniLog.Log($"Caller module identified. Checking if it matches Varjo libraries...");
                if (callerModule == libVarjo || callerModule == libVarjoRuntime)
                {
                    UniLog.Log("Caller module matches Varjo libraries. Hijacking process ID...");
                    ResoniteMod.Warn("Hijacked Varjo's process ID");
                    return originalGetCurrentProcessId() + 42;
                }
                else
                {
                    UniLog.Log("Caller module does not match Varjo libraries. Returning original process ID.");
                }
            }
            else
            {
                UniLog.Log("Failed to determine the caller module. Returning original process ID.");
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
            UniLog.Log("Retrieving return address of the current method...");
            StackFrame frame = new StackFrame(1, true);
            IntPtr returnAddress = frame.GetMethod().MethodHandle.GetFunctionPointer();
            UniLog.Log($"Return address retrieved: {returnAddress}");
            return returnAddress;
        }

        #endregion
    }
}
