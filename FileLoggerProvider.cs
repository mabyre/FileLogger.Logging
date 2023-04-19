using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;

using System.Collections.Concurrent;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;

namespace FileLogger.Logging
{
    /// <summary>
    /// A logger provider that writes log entries to a text file.
    /// <para>"File" is the provider alias of this provider and can be used in the Logging section of the appsettings.json.</para>
    /// </summary>
    [Microsoft.Extensions.Logging.ProviderAlias("File")]
    public class FileLoggerProvider : LoggerProvider
    {
        public static string PathFileName { get; set; }

        private bool Terminated;
        private int Counter = 0;
        private Dictionary<string, int> Lengths = new Dictionary<string, int>();
       
        private ConcurrentQueue<LogEntry> InfoQueue = new ConcurrentQueue<LogEntry>();

        /// <summary>
        /// Applies the log file retains policy according to options
        /// </summary>
        void ApplyRetainPolicy()
        {
            FileInfo FI;
            try
            {
                List<FileInfo> FileList = new DirectoryInfo(Settings.Folder)
                .GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .OrderBy(fi => fi.CreationTime)
                .ToList();

                while (FileList.Count >= Settings.RetainPolicyFileCount)
                {
                    FI = FileList.First();
                    FI.Delete();
                    FileList.Remove(FI);
                }
            }
            catch  
            { 
            }

        }
        /// <summary>
        /// Writes a line of text to the current file.
        /// If the file reaches the size limit, creates a new file and uses that new file.
        /// </summary>
        void WriteLine(string Text)
        {
            // check the file size after any 100 writes
            Counter++;
            if (Counter % 100 == 0)
            {
                FileInfo FI = new FileInfo(PathFileName);
                if (FI.Length > (1024 * 1024 * Settings.MaxFileSizeInMB))
                {                    
                    BeginFile(); // begin a new file
                }
            }

            File.AppendAllText(PathFileName, Text);
        }
        /// <summary>
        /// Pads a string with spaces to a max length. Truncates the string to max length if the string exceeds the limit.
        /// </summary>
        string Pad(string Text, int MaxLength)
        {
            if (string.IsNullOrWhiteSpace(Text))
                return "".PadRight(MaxLength);

            if (Text.Length > MaxLength)
                return Text.Substring(0, MaxLength);

            return Text.PadRight(MaxLength);
        }
        /// <summary>
        /// Prepares the lengths of the columns in the log file
        /// </summary>
        void PrepareLengths()
        {
            // prepare the lengs table
            Lengths["Time"] = 24;
            Lengths["Host"] = 16;
            Lengths["User"] = 16;
            Lengths["Level"] = 14;
            Lengths["EventId"] = 8;
            Lengths["Category"] = 32;
            Lengths["Text"] = 64;
            Lengths["Scope"] = 64;
        }

        /// <summary>
        /// Creates a new disk file and writes the column titles
        /// </summary>
        void BeginFile()
        {
            Directory.CreateDirectory(Settings.Folder);
            PathFileName = Path.Combine(Settings.Folder, LogEntry.StaticHostName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".log");

            // Log Titles
            StringBuilder sb = new StringBuilder();
            sb.Append(Pad("Time", Lengths["Time"]));
            sb.Append(Pad("Host", Lengths["Host"]));
            sb.Append(Pad("User", Lengths["User"]));
            sb.Append(Pad("Level", Lengths["Level"]));
            sb.Append(Pad("EventId", Lengths["EventId"]));
            sb.Append(Pad("Category", Lengths["Category"]));
            sb.Append(Pad("Text", Lengths["Text"]));
            sb.AppendLine(Pad("Scope", Lengths["Scope"]));

            File.WriteAllText(PathFileName, sb.ToString());

            ApplyRetainPolicy();
        }
        /// <summary>
        /// Pops a log info instance from the stack, prepares the text line, and writes the line to the text file.
        /// </summary>
        void WriteLogLine()
        {
            LogEntry Info = null;
            if (InfoQueue.TryDequeue(out Info))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Pad(Info.TimeStampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.ff"), Lengths["Time"]));
                sb.Append(Pad(Info.HostName, Lengths["Host"]));
                sb.Append(Pad(Info.UserName, Lengths["User"]));
                sb.Append(Pad(Info.Level.ToString(), Lengths["Level"]));
                sb.Append(Pad(Info.EventId != null ? Info.EventId.ToString() : "", Lengths["EventId"]));
                sb.Append(Pad(Info.Category, Lengths["Category"]));
                sb.Append(Info.Text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));

                if (Info.Scopes != null && Info.Scopes.Count > 0)
                {
                    string S = "";

                    LogScopeInfo infoScope = Info.Scopes.Last();
                    if (!string.IsNullOrWhiteSpace(infoScope.Text))
                    {
                        S = infoScope.Text;
                    }
                    else
                    {
                    }

                    sb.Append(Pad(S, Lengths["Scope"]));
                }


                /* Writing properties is too much for a text file logger
                 * while StateProperties is equivalent to Text
                if (Info.StateProperties != null && Info.StateProperties.Count > 0)
                {
                    Text = Text + " Properties = " + Newtonsoft.Json.JsonConvert.SerializeObject(Info.StateProperties);
                }                 
                 */

                sb.AppendLine();
                WriteLine(sb.ToString());
            }

        }
        void ThreadProc()
        {
            Task.Run(() => {

                while (!Terminated)
                {
                    try
                    {
                        WriteLogLine();
                        System.Threading.Thread.Sleep(100);
                    }
                    catch // (Exception ex)
                    {
                    }
                }

            });
        }

        /* overrides */
        /// <summary>
        /// Disposes the options change toker. IDisposable pattern implementation.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            Terminated = true;
            base.Dispose(disposing);
        }

        /* construction */
        /// <summary>
        /// Constructor.
        /// <para>The IOptionsMonitor provides the OnChange() method which is called when the user alters the settings of this provider in the appsettings.json file.</para>
        /// </summary>
        public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> Settings)
            : this(Settings.CurrentValue)
        {   
            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/change-tokens
            SettingsChangeToken = Settings.OnChange(settings => {       
                this.Settings = settings;                   
            }); 
        }
        /// <summary>
        /// Constructor
        /// </summary>
        public FileLoggerProvider(FileLoggerOptions Settings)
        {
            PrepareLengths();
            this.Settings = Settings;

            // Create the first log file
            BeginFile();

            ThreadProc();
        }

        /* public */
        /// <summary>
        /// Checks if the given logLevel is enabled. It is called by the Logger.
        /// </summary>
        public override bool IsEnabled(LogLevel logLevel)
        {
            bool Result = logLevel != LogLevel.None
               && this.Settings.LogLevel != LogLevel.None
               && Convert.ToInt32(logLevel) >= Convert.ToInt32(this.Settings.LogLevel);

            return Result;
        }
        /// <summary>
        /// Writes the specified log information to a log file.
        /// </summary>
        public override void WriteLog(LogEntry Info)
        {
            InfoQueue.Enqueue(Info);
        }

        /* properties */
        /// <summary>
        /// Returns the settings
        /// </summary>
        internal FileLoggerOptions Settings { get; private set; }


    }
}
