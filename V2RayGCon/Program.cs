﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static V2RayGCon.Lib.StringResource;

#region Support CallerMemberName on .net 4.0
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class CallerMemberNameAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class CallerFilePathAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class CallerLineNumberAttribute : Attribute
    {
    }
}
#endregion

namespace V2RayGCon
{

    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>

        #region DPI awareness
        // PROCESS_DPI_AWARENESS = 0/1/2 None/SystemAware/PerMonitorAware
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SetProcessDpiAwareness(int PROCESS_DPI_AWARENESS);
        #endregion

        #region single instance
        // https://stackoverflow.com/questions/19147/what-is-the-correct-way-to-create-a-single-instance-application
#if DEBUG
        static Mutex mutex = new Mutex(true, "{a4333801-a206-4061-9e20-1f03e2deaf7f}");
#else
        static Mutex mutex = new Mutex(true, "{84d287ae-c0b0-4c1a-9ecc-d98c26577c02}");
#endif
        #endregion

        static Service.Notifier noty = null;

        [STAThread]
        static void Main()
        {
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            IntPtr pShcoreDll = HiResSupport();


            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                // Service.Notifier noty = null;
                noty = Service.Notifier.Instance;
                Application.Run();
                mutex.ReleaseMutex();
            }
            else
            {
                MessageBox.Show(I18N("ExitOtherVGCFirst"));
            }

            Lib.DllLoader.FreeLibrary(pShcoreDll);
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            SaveException(e.Exception.ToString());
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            SaveException((e.ExceptionObject as Exception).ToString());
        }

        private static IntPtr HiResSupport()
        {
            // load Shcore.dll and get high resolution support
            IntPtr pDll = Lib.DllLoader.LoadLibrary(@"Shcore.DLL");
            Lib.DllLoader.CallMethod(
                pDll,
                @"SetProcessDpiAwareness",
                typeof(SetProcessDpiAwareness),
                (method) => ((SetProcessDpiAwareness)method).Invoke(2));
            return pDll;
        }

        private static void ShowMessage()
        {
            System.Diagnostics.Process.Start(GetBugLogFileName());
#if DEBUG
#else
            MessageBox.Show(I18N("LooksLikeABug")
                + System.Environment.NewLine
                + GetBugLogFileName());
#endif
        }

        static void SaveException(string msg)
        {
            var log = msg;
            try
            {
                if (noty != null)
                {
                    log += Environment.NewLine
                        + Environment.NewLine
                        + noty.GetLogCache();
                }
            }
            catch { }
            SaveBugLog(log);
            ShowMessage();
#if DEBUG
#else
            Application.Exit();
#endif
        }

        static string GetBugLogFileName()
        {
            var appData = Lib.Utils.GetAppDataFolder();
            return Path.Combine(appData, StrConst("BugFileName"));
        }

        static void SaveBugLog(string content)
        {
            try
            {
                var bugFileName = GetBugLogFileName();
                Lib.Utils.CreateAppDataFolder();
                File.WriteAllText(bugFileName, content);
            }
            catch { }
        }
    }
}
