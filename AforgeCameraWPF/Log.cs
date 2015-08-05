using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AforgeCameraWPF
{
    public enum LogEventLevel
    {
        Verbose = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5,
    }

    public static class Log
    {
        private static string LINEFMT = "{0:yyyy-MMM-dd HH:mm:ss.fff}\t{1}\t{2}";
        private static string EXCPFMT = "{0:yyyy-MMM-dd HH:mm:ss.fff}\t{1}\t{2}\n{3}";
        private static string EXCPFMT_NOMSG = "{0:yyyy-MMM-dd HH:mm:ss.fff}\t{1}\t{2} failed.\n{3}";

        public static LogEventLevel EffectiveLevel
        {
            get;
            set;
        }

        public static void Verbose(string message)
        {
            if (EffectiveLevel > LogEventLevel.Verbose)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(LINEFMT, DateTime.Now, LogEventLevel.Verbose, message));
        }

        public static void Debug(string format, object p)
        {
            if (EffectiveLevel > LogEventLevel.Debug)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(format, p));
        }

        public static void Debug(string format, object p1, object p2)
        {
            if (EffectiveLevel > LogEventLevel.Debug)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(format, p1, p2));
        }

        public static void Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine(string.Format(LINEFMT, DateTime.Now, LogEventLevel.Debug, message));
        }

        public static void Information(string message)
        {
            if (EffectiveLevel > LogEventLevel.Information)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(LINEFMT, DateTime.Now, LogEventLevel.Information, message));
        }

        public static void Warning(string message)
        {
            if (EffectiveLevel > LogEventLevel.Warning)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(LINEFMT, DateTime.Now, LogEventLevel.Warning, message));
        }

        public static void Warning(Exception ex, string message)
        {
            if (EffectiveLevel > LogEventLevel.Warning)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(EXCPFMT, DateTime.Now, LogEventLevel.Warning, message, ex));
        }

        public static void Error(Exception ex, [CallerMemberName] string callingMethod = "")
        {
            if (EffectiveLevel > LogEventLevel.Error)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(EXCPFMT_NOMSG, DateTime.Now, LogEventLevel.Error, callingMethod, ex));
        }

        public static void Error(string message, Exception ex)
        {
            if (EffectiveLevel > LogEventLevel.Error)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(EXCPFMT, DateTime.Now, LogEventLevel.Error, message, ex));
        }

        public static void Fatal(Exception ex, [CallerMemberName] string callingMethod = "")
        {
            if (EffectiveLevel > LogEventLevel.Fatal)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(EXCPFMT_NOMSG, DateTime.Now, LogEventLevel.Fatal, callingMethod, ex));
        }

        public static void Fatal(string message, Exception ex)
        {
            if (EffectiveLevel > LogEventLevel.Fatal)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(string.Format(EXCPFMT, DateTime.Now, LogEventLevel.Fatal, message, ex));
        }

        public static void Write(LogEventLevel level, string message)
        {
            System.Diagnostics.Debug.WriteLine(string.Format(LINEFMT, DateTime.Now, level, message));
        }
    }
}
