using System.Runtime.InteropServices;
using System.Text;
using System;

namespace Varjo4Reso
{
   public  class VarjoNativeInterface
    {
        private IntPtr _session;
        protected GazeData gazeData;
        protected EyeMeasurements eyeMeasurements;

        #region Lifetime methods (Init, Update, Teardown)

        public bool Initialize()
        {
            HookProc.HookGetCurrentProcessId();

            _session = varjo_SessionInit();
            if (_session == IntPtr.Zero)
            {
                return false;
            }

            if (!varjo_IsGazeAllowed(_session))
            {
                return false;
            }

            varjo_GazeInit(_session);
            varjo_SyncProperties(_session);
            return true;
        }

        public void Teardown()
        {
            varjo_SessionShutDown(_session);
        }

        public bool Update()
        {
            if (_session == IntPtr.Zero)
                return false;

            bool hasData = varjo_GetGazeData(_session, out gazeData, out eyeMeasurements);
            return hasData;
        }

        public GazeData GetGazeData() => gazeData;
        public EyeMeasurements GetEyeMeasurements() => eyeMeasurements;

        public string GetHMDName()
        {
            int bufferSize = varjo_GetPropertyStringSize(_session, VarjoPropertyKey.HMDProductName);
            StringBuilder buffer = new StringBuilder(bufferSize);
            varjo_GetPropertyString(_session, VarjoPropertyKey.HMDProductName, buffer, bufferSize);

            return buffer.ToString();
        }

        #endregion

        #region DllImports
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
    }
}