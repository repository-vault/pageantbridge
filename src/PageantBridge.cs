using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.Security.Principal;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;

namespace PageantBridge
{
    class PageantBridge : IDisposable
    {
        const string className = "Pageant";

        delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        #region security

        [Flags()]
        private enum AccessRights : long
        {
            DELETE = 0x00010000L,
            READ_CONTROL = 0x00020000L,
            WRITE_DAC = 0x00040000L,
            WRITE_OWNER = 0x00080000L,
            SYNCHRONIZE = 0x00100000L,

            STANDARD_RIGHTS_REQUIRED = 0x000F0000L,

            STANDARD_RIGHTS_READ = READ_CONTROL,
            STANDARD_RIGHTS_WRITE = READ_CONTROL,
            STANDARD_RIGHTS_EXECUTE = READ_CONTROL,

            STANDARD_RIGHTS_ALL = 0x001F0000L,

            SPECIFIC_RIGHTS_ALL = 0x0000FFFFL,

            // AccessSystemAcl access type
            ACCESS_SYSTEM_SECURITY = 0x01000000L,

            // MaximumAllowed access type
            MAXIMUM_ALLOWED = 0x02000000L,

            // These are the generic rights.
            GENERIC_READ = 0x80000000L,
            GENERIC_WRITE = 0x40000000L,
            GENERIC_EXECUTE = 0x20000000L,
            GENERIC_ALL = 0x10000000L
        }

        private enum SE_OBJECT_TYPE
        {
            SE_UNKNOWN_OBJECT_TYPE = 0,
            SE_FILE_OBJECT,
            SE_SERVICE,
            SE_PRINTER,
            SE_REGISTRY_KEY,
            SE_LMSHARE,
            SE_KERNEL_OBJECT,
            SE_WINDOW_OBJECT,
            SE_DS_OBJECT,
            SE_DS_OBJECT_ALL,
            SE_PROVIDER_DEFINED_OBJECT,
            SE_WMIGUID_OBJECT,
            SE_REGISTRY_WOW64_32KEY
        }

        [Flags()]
        private enum SECURITY_INFORMATION : long
        {
            OWNER_SECURITY_INFORMATION = 0x00000001L,
            GROUP_SECURITY_INFORMATION = 0x00000002L,
            DACL_SECURITY_INFORMATION = 0x00000004L,
            SACL_SECURITY_INFORMATION = 0x00000008L,
            LABEL_SECURITY_INFORMATION = 0x00000010L,

            PROTECTED_DACL_SECURITY_INFORMATION = 0x80000000L,
            PROTECTED_SACL_SECURITY_INFORMATION = 0x40000000L,
            UNPROTECTED_DACL_SECURITY_INFORMATION = 0x20000000L,
            UNPROTECTED_SACL_SECURITY_INFORMATION = 0x10000000L
        }

        [DllImport("kernel32")]
        private static extern IntPtr OpenProcess(AccessRights dwDesiredAccess,
          bool bInheritHandle, long dwProcessId);

        [DllImport("Advapi32")]
        private static extern long GetSecurityInfo(IntPtr handle,
          SE_OBJECT_TYPE objectType, SECURITY_INFORMATION securityInfo,
          out IntPtr ppsidOwner, out IntPtr ppsidGroup, out IntPtr ppDacl,
          out IntPtr ppSacl, out IntPtr ppSecurityDescriptor);

        [DllImport("kernel32")]
        private static extern bool CloseHandle(IntPtr hObject);

        private SecurityIdentifier GetProcessOwnerSID(int pid)
        {
            var processHandle = OpenProcess(AccessRights.MAXIMUM_ALLOWED, false, pid);
            if (processHandle == IntPtr.Zero)
            {
                return null;
            }
            try
            {
                IntPtr sidOwner, sidGroup, dacl, sacl, securityDescriptor;

                if (GetSecurityInfo(processHandle, SE_OBJECT_TYPE.SE_KERNEL_OBJECT,
                    SECURITY_INFORMATION.OWNER_SECURITY_INFORMATION, out sidOwner,
                    out sidGroup, out dacl, out sacl, out securityDescriptor) != 0)
                {
                    return null;
                }
                return new SecurityIdentifier(sidOwner);
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        #endregion

#region stdin check
        public static bool IsInputRedirected { get { return FileType.Pipe == GetFileType(GetStdHandle(StdHandle.Stdin)); } }

        // P/Invoke:
        public enum FileType { Unknown, Disk, Char, Pipe };
        private enum StdHandle { Stdin = -10, Stdout = -11, Stderr = -12 };
        [DllImport("kernel32.dll")]
        private static extern FileType GetFileType(IntPtr hdl);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(StdHandle std);

#endregion

        public static bool CheckPageantRunning()
        {
            IntPtr hwnd = FindWindow(className, className);
            return (hwnd != IntPtr.Zero);
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        const long AGENT_COPYDATA_ID = 0x804e50ba;

        struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
            public string lpszClassName;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(String sClassName, String sAppName);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern System.UInt16 RegisterClassW(
            [System.Runtime.InteropServices.In] ref WNDCLASS lpWndClass
        );

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CreateWindowExW(
           UInt32 dwExStyle,
           [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
       string lpClassName,
           [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
       string lpWindowName,
           UInt32 dwStyle,
           Int32 x,
           Int32 y,
           Int32 nWidth,
           Int32 nHeight,
           IntPtr hWndParent,
           IntPtr hMenu,
           IntPtr hInstance,
           IntPtr lpParam
        );

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern System.IntPtr DefWindowProcW(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam
        );

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(
            IntPtr hWnd
        );

        private const int ERROR_CLASS_ALREADY_EXISTS = 1410;

        private bool m_disposed;
        private IntPtr m_hwnd;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }

                // Dispose unmanaged resources
                if (m_hwnd != IntPtr.Zero)
                {
                    DestroyWindow(m_hwnd);
                    m_hwnd = IntPtr.Zero;
                    m_disposed = true;
                }

            }
        }

        public void registerClassName(string class_name)
        {

            if (String.IsNullOrEmpty(class_name))
                throw new Exception("Invalid class_name");

            WndProc m_wnd_proc_delegate = CustomWndProc;

            // Create WNDCLASS
            WNDCLASS wind_class = new WNDCLASS();
            wind_class.lpszClassName = class_name;
            wind_class.lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(m_wnd_proc_delegate);

            UInt16 class_atom = RegisterClassW(ref wind_class);

            int last_error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();

            if (class_atom == 0 && last_error != ERROR_CLASS_ALREADY_EXISTS)
                throw new Exception("Could not register window class");
        }


        private byte[] byteData = new byte[1024];

        Stream stdin;
        Stream stdout;
        public PageantBridge(string class_name, string window_name)
        {

            if(!IsInputRedirected)
                throw new Exception("Stdin is not bound to anything");

            if (CheckPageantRunning())
                throw new PublicException(Errors.ALREADY_RUNNING);

            stdin = Console.OpenStandardInput();
            stdout = Console.OpenStandardOutput();

            registerClassName(class_name);

            // Create window
            m_hwnd = CreateWindowExW(
                0,
                class_name,
                window_name,
                0,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero
            );
        }

        const int WM_COPYDATA = 0x004A;
        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {

            // we only care about COPYDATA messages
            if (msg != WM_COPYDATA)
                return DefWindowProcW(hWnd, msg, wParam, lParam);

            Console.Error.WriteLine("Receive message", hWnd, msg);
            IntPtr result = IntPtr.Zero;

            // convert lParam to something usable
            COPYDATASTRUCT copyData = (COPYDATASTRUCT) Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));

            if (((IntPtr.Size == 4) &&
                (copyData.dwData.ToInt32() != (unchecked((int)AGENT_COPYDATA_ID)))) ||
                ((IntPtr.Size == 8) &&
                (copyData.dwData.ToInt64() != AGENT_COPYDATA_ID))) {
                return result; // failure
            }

          string mapname = Marshal.PtrToStringAnsi(copyData.lpData);
          if (mapname.Length != copyData.cbData - 1)
            return result; // failure

      try {
        MemoryMappedFile fileMap = MemoryMappedFile.OpenExisting(mapname, MemoryMappedFileRights.FullControl);
        if (fileMap.SafeMemoryMappedFileHandle.IsInvalid) 
            return result; // failure


#region security
        /* check to see if message sender is same user as this program's
           * user */

          SecurityIdentifier mapOwner =
            (SecurityIdentifier)fileMap.GetAccessControl()
            .GetOwner(typeof(System.Security.Principal.SecurityIdentifier));

          var user = WindowsIdentity.GetCurrent();
          var userSid = user.User;

          // see http://www.chiark.greenend.org.uk/~sgtatham/putty/wishlist/pageant-backwards-compatibility.html
          var procOwnerSid = GetProcessOwnerSID(Process.GetCurrentProcess().Id);

          if (!(userSid == mapOwner || procOwnerSid == mapOwner))
              return result; // failure
#endregion


        MemoryMappedViewStream stream = fileMap.CreateViewStream();
        stream.CopyTo(stdout);
          //we respond is the same mapfile (so response cannot exceed mapfile length)
        byteData = new byte[stream.Length];

        var red = stdin.Read(byteData, 0, byteData.Length);
        Console.Error.WriteLine("Got " + red + " bytes from agent");

        if (stream.CanSeek)
            stream.Position = 0;
        stream.Write(byteData, 0, byteData.Length);
        stream.Flush();

        result = new IntPtr(1);
        return result; // success
      } catch (Exception ex) {
        Debug.Fail(ex.ToString());
      }
      return result; // failure    
    }
    }





}
