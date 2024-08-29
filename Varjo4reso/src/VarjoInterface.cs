using Elements.Core;
using ResoniteModLoader;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Varjo4Reso
{
    public class VarjoNativeInterface
    {
        private IntPtr _session;
        protected GazeData gazeData;
        protected EyeMeasurements eyeMeasurements;

        private const int GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
        private const int GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT = 0x00000002;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        public bool Initialize()
        {
            UniLog.Log("Initializing Varjo Native Interface...");

            if (!VarjoAvailable())
            {
                UniLog.Log("Varjo headset detection failed.");
                return false;
            }

            UniLog.Log("Varjo headset detected successfully.");

            if (!LoadLibrary())
            {
                UniLog.Log("Varjo library loading failed.");
                return false;
            }

            UniLog.Log("Varjo library loaded successfully.");

            HookGetCurrentProcessId();

            UniLog.Log("Process ID hook installed successfully.");

            _session = varjo_SessionInit();
            if (_session == IntPtr.Zero)
            {
                UniLog.Log("Varjo session initialization failed.");
                return false;
            }

            UniLog.Log("Varjo session initialized successfully.");

            if (!varjo_IsGazeAllowed(_session))
            {
                UniLog.Log("Gaze tracking is not allowed by Varjo Base.");
                return false;
            }

            UniLog.Log("Gaze tracking allowed by Varjo Base.");

            varjo_GazeInit(_session);
            UniLog.Log("Gaze tracking initialized successfully.");

            varjo_SyncProperties(_session);
            UniLog.Log("Synchronized properties with Varjo SDK.");

            // Check for Varjo Aero or VR-1 (Hedy)
            string hmdName = GetHMDName();
            UniLog.Log($"HMD Name detected: {hmdName}");

            if (hmdName.Contains("Aero") || hmdName.Contains("hedy"))
            {
                UniLog.Log($"Supported Varjo device detected: {hmdName}");
            }
            else
            {
                UniLog.Log($"Unsupported Varjo device detected: {hmdName}");
            }

            UniLog.Log("Varjo Native Interface initialized successfully.");
            return true;
        }

        public void Teardown()
        {
            UniLog.Log("Tearing down Varjo Native Interface...");

            if (_session != IntPtr.Zero)
            {
                varjo_SessionShutDown(_session);
                UniLog.Log("Varjo session shut down successfully.");
            }
            else
            {
                UniLog.Log("Varjo session was not active, nothing to shut down.");
            }
        }

        public bool Update()
        {
            UniLog.Log("Updating Varjo Native Interface...");

            if (_session == IntPtr.Zero)
            {
                UniLog.Log("Varjo session is not initialized.");
                return false;
            }

            bool hasData = varjo_GetGazeData(_session, out gazeData, out eyeMeasurements);
            if (!hasData)
            {
                UniLog.Log("Failed to get Gaze Data from Varjo.");
            }
            else
            {
                UniLog.Log("Gaze Data successfully retrieved from Varjo.");
            }

            return hasData;
        }

        public GazeData GetGazeData()
        {
            UniLog.Log("Getting GazeData...");
            return gazeData;
        }

        public EyeMeasurements GetEyeMeasurements()
        {
            UniLog.Log("Getting EyeMeasurements...");
            return eyeMeasurements;
        }

        public string GetHMDName()
        {
            UniLog.Log("Retrieving HMD name...");
            int bufferSize = varjo_GetPropertyStringSize(_session, VarjoPropertyKey.HMDProductName);
            StringBuilder buffer = new StringBuilder(bufferSize);
            varjo_GetPropertyString(_session, VarjoPropertyKey.HMDProductName, buffer, bufferSize);
            UniLog.Log($"HMD name retrieved: {buffer}");
            return buffer.ToString();
        }

        private bool LoadLibrary()
        {
            UniLog.Log("Loading Varjo library...");
            string path = $"{System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}\\TrackingLibs\\VarjoLib.dll";
            UniLog.Log($"Library path: {path}");

            if (path == null || LoadLibrary(path) == IntPtr.Zero)
            {
                UniLog.Log($"Unable to load library: {path}");
                return false;
            }

            UniLog.Log($"Library loaded successfully: {path}");
            return true;
        }

        private static bool VarjoAvailable()
        {
            UniLog.Log("Checking if Varjo is available...");
            bool available = System.IO.File.Exists("\\\\.\\pipe\\Varjo\\InfoService");
            UniLog.Log($"Varjo available: {available}");
            return available;
        }

        private void HookGetCurrentProcessId()
        {
            UniLog.Log("Hooking GetCurrentProcessId...");

            IntPtr kernel32Handle = GetModuleHandleA("kernel32.dll");
            if (kernel32Handle == IntPtr.Zero)
            {
                UniLog.Log("Failed to get handle for kernel32.dll.");
                throw new Exception("Failed to get handle for kernel32.dll.");
            }
            UniLog.Log("Successfully obtained handle for kernel32.dll.");

            IntPtr procAddress = GetProcAddress(kernel32Handle, "GetCurrentProcessId");
            if (procAddress == IntPtr.Zero)
            {
                UniLog.Log("Failed to get address for GetCurrentProcessId.");
                throw new Exception("Failed to get address for GetCurrentProcessId.");
            }
            UniLog.Log("Successfully obtained address for GetCurrentProcessId.");

            VirtualProtect(procAddress, (uint)IntPtr.Size, PAGE_EXECUTE_READWRITE, out uint oldProtect);
            UniLog.Log("Memory protection changed to PAGE_EXECUTE_READWRITE.");

            Marshal.WriteIntPtr(procAddress, Marshal.GetFunctionPointerForDelegate(new GetCurrentProcessIdDelegate(GetCurrentProcessId_Hook)));
            UniLog.Log("GetCurrentProcessId hooked successfully.");

            VirtualProtect(procAddress, (uint)IntPtr.Size, oldProtect, out _);
            UniLog.Log("Memory protection restored.");
        }

        private uint GetCurrentProcessId_Hook()
        {
            UniLog.Log("GetCurrentProcessId_Hook called.");

            IntPtr returnAddr = GetReturnAddress();
            IntPtr callerModule;

            if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, returnAddr, out callerModule))
            {
                IntPtr libVarjo = GetModuleHandleA("VarjoLib.dll");
                IntPtr libVarjoRuntime = GetModuleHandleA("VarjoRuntime.dll");

                if (callerModule == libVarjo || callerModule == libVarjoRuntime)
                {
                    UniLog.Log("Hijacked Varjo's process ID call.");
                    return GetCurrentProcessId() + 42;
                }
            }

            UniLog.Log("Returning original process ID.");
            return GetCurrentProcessId();
        }

        #region DllImports

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandleA(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetModuleHandleExA(int dwFlags, IntPtr ModuleName, out IntPtr phModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetCurrentProcessIdDelegate();

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("VarjoLib", CharSet = CharSet.Auto)]
        private static extern IntPtr varjo_SessionInit();

        [DllImport("VarjoLib", CharSet = CharSet.Auto)]
        private static extern void varjo_SessionShutDown(IntPtr session);

        [DllImport("VarjoLib", CharSet = CharSet.Auto)]
        private static extern void varjo_GazeInit(IntPtr session);

        [DllImport("VarjoLib", CharSet = CharSet.Auto)]
        private static extern bool varjo_IsGazeAllowed(IntPtr session);

        [DllImport("VarjoLib", CharSet = CharSet.Auto)]
        private static extern bool varjo_GetGazeData(IntPtr session, out GazeData gaze, out EyeMeasurements eyeMeasurements);

        [DllImport("VarjoLib", CharSet = CharSet.Auto)]
        private static extern void varjo_SyncProperties(IntPtr session);

        [DllImport("VarjoLib", CharSet = CharSet.Auto)]
        private static extern int varjo_GetPropertyStringSize(IntPtr session, VarjoPropertyKey propertyKey);

        [DllImport("VarjoLib", CharSet = CharSet.Ansi)]
        private static extern void varjo_GetPropertyString(IntPtr session, VarjoPropertyKey propertyKey, StringBuilder buffer, int bufferSize);

        #endregion

        private static IntPtr GetReturnAddress()
        {
            StackFrame frame = new StackFrame(1, true);
            return frame.GetMethod().MethodHandle.GetFunctionPointer();
        }
    }
}
