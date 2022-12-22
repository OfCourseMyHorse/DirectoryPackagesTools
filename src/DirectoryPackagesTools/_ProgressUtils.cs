using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectoryPackagesTools
{
    class _ProgressCounter : IProgress<string>
    {
        public _ProgressCounter(IProgress<int> target, int count)
        {
            _Percent = target;
            _Total = count;

            _Elapsed = System.Diagnostics.Stopwatch.StartNew();
        }

        private readonly int _Total;
        private int _Count;
        private IProgress<int> _Percent;

        private System.Diagnostics.Stopwatch _Elapsed;

        public TimeSpan Elapsed => _Elapsed.Elapsed;

        public void Report(string value)
        {
            ++_Count;
            _Percent?.Report((_Count * 100) / _Total);
        }
    }
}
