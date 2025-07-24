using System;
using System.IO;
using System.Runtime.InteropServices;

namespace scopy
{
    class Program
    {
        [DllImport("ntdll.dll")]
        static extern uint NtGetNlsSectionPtr(uint SectionType, uint SectionData, IntPtr ContextData, out IntPtr SectionPointer, out int SectionSize);

        [DllImport("ntdll.dll")]
        static extern uint NtMakeTemporaryObject(IntPtr Handle);

        [DllImport("ntdll.dll")]
        static extern uint NtOpenSection(out IntPtr SectionHandle, uint DesiredAccess, ref OBJECT_ATTRIBUTES ObjectAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        static int RandNlsName(out string name)
        {
            Random random = new Random();
            int num = random.Next(1000, 10000); // Generates a number between 1000 and 9999
            name = $"C:\\Windows\\System32\\C_{num}.NLS";
            return num;
        }

        static void SaveFile(string filePath, IntPtr ptr, int size)
        {
            byte[] data = new byte[size];
            Marshal.Copy(ptr, data, 0, size);
            File.WriteAllBytes(filePath, data);
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 2)
                    return;

                string src = args[0];
                string dest = args[1];

                int nlsNum = -1;
                string nlsName;

                do
                {
                    nlsNum = RandNlsName(out nlsName);
                } while (File.Exists(nlsName));

                bool ret = CreateSymbolicLink(nlsName, src, 0);
                if (!ret)
                {
                    Console.WriteLine("create error {0}", Marshal.GetLastWin32Error());
                    return;
                }

                int bufSize = 0;
                IntPtr buf = IntPtr.Zero;

                uint status = NtGetNlsSectionPtr(11, (uint)nlsNum, IntPtr.Zero, out buf, out bufSize);
                if (status != 0)
                {
                    Console.WriteLine("get error {0:x8}", status);
                    return;
                }

                if (File.Exists(dest))
                    File.Delete(dest);

                SaveFile(dest, buf, bufSize);

                File.Delete(nlsName);

                OBJECT_ATTRIBUTES attr = new OBJECT_ATTRIBUTES();
                attr.Length = Marshal.SizeOf(typeof(OBJECT_ATTRIBUTES));
                attr.RootDirectory = IntPtr.Zero;
                attr.Attributes = 0;
                attr.SecurityDescriptor = IntPtr.Zero;
                attr.SecurityQualityOfService = IntPtr.Zero;

                string linkName = string.Format("\\NLS\\NlsSectionCP{0}", nlsNum);

                UNICODE_STRING uLinkName = new UNICODE_STRING();
                uLinkName.Buffer = Marshal.StringToHGlobalUni(linkName);
                uLinkName.Length = (ushort)(linkName.Length * 2);
                uLinkName.MaximumLength = (ushort)((linkName.Length + 1) * 2);

                attr.ObjectName = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UNICODE_STRING)));
                Marshal.StructureToPtr(uLinkName, attr.ObjectName, false);

                uint DELETE = 0x00010000;

                IntPtr hSection = IntPtr.Zero;
                status = NtOpenSection(out hSection, DELETE, ref attr);
                if (status != 0)
                {
                    Console.WriteLine("open error {0:x8}", status);
                    return;
                }

                NtMakeTemporaryObject(hSection);
                CloseHandle(hSection);

                Console.WriteLine("copied.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}