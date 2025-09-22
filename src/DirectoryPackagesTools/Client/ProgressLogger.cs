using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Common;

namespace DirectoryPackagesTools.Client
{
    

    internal class ProgressLogger : ILogger
    {
        #region lifecycle

        public static ProgressLogger Instance { get; } = new ProgressLogger(new _DebugProgress());        

        public ProgressLogger(IProgress<string> sink)
        {
            _Sink = sink;
        }

        private sealed class _DebugProgress : IProgress<string>
        {
            public void Report(string value)
            {
                System.Diagnostics.Debug.WriteLine(value);
            }
        }

        #endregion

        private readonly IProgress<string> _Sink;

        public void LogDebug(string data) { _Sink.Report(data); }

        public void LogVerbose(string data) { _Sink.Report(data); }

        public void LogInformation(string data) { _Sink.Report(data); }

        public void LogMinimal(string data) { _Sink.Report(data); }

        public void LogWarning(string data) { _Sink.Report(data); }

        public void LogError(string data) { _Sink.Report(data); }

        public void LogInformationSummary(string data) { _Sink.Report(data); }

        public void Log(LogLevel level, string data) { _Sink.Report(data); }        

        public async Task LogAsync(LogLevel level, string data)
        {
            await Task.Yield();
            _Sink.Report(data);            
        }
        public void Log(ILogMessage message)
        {
            _Sink.Report(message.Message);
        }

        public async Task LogAsync(ILogMessage message)
        {
            await Task.Yield();
            _Sink.Report(message.Message);            
        }
    }
}
