using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectoryPackagesTools
{
    static class _VersionControlUtils
    {
        public static bool CommitToVersionControl(this DirectoryInfo dinfo)
        {
            if (dinfo.CommitGIT()) return true;
            if (dinfo.CommitSVN()) return true;
            return false;
        }

        public static bool CommitSVN(this DirectoryInfo dinfo)
        {
            var t = dinfo;

            while (t != null)
            {
                if (t.EnumerateDirectories(".svn").Any()) break;
                t = t.Parent;
            }

            if (t == null) return false;

            // https://tortoisesvn.net/docs/release/TortoiseSVN_en/tsvn-automation.html

            var exePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            exePath = System.IO.Path.Combine(exePath, "TortoiseSVN\\bin\\TortoiseProc.exe");

            var psi = new System.Diagnostics.ProcessStartInfo(exePath, $"/command:commit /path:\"{dinfo.FullName}\" /logmsg:\"nugets++\"");
            psi.UseShellExecute = true;
            psi.WorkingDirectory = dinfo.FullName;

            System.Diagnostics.Process.Start(psi);

            return true;
        }

        public static bool CommitGIT(this DirectoryInfo dinfo)
        {
            var t = dinfo;

            while (t != null)
            {
                if (t.EnumerateDirectories(".git").Any()) break;
                t = t.Parent;
            }

            if (t == null) return false;

            // https://stackoverflow.com/questions/40569041/list-of-tortoisegit-equivalents-of-command-line-commands
            // https://tortoisegit.org/docs/tortoisegit/git-command.html

            var exePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            exePath = System.IO.Path.Combine(exePath, "TortoiseGit\\bin\\TortoiseGitProc.exe");

            var psi = new System.Diagnostics.ProcessStartInfo(exePath, $"/command:commit /path:\"{dinfo.FullName}\" /logmsg:\"nugets++\"");
            psi.UseShellExecute = true;
            psi.WorkingDirectory = dinfo.FullName;

            System.Diagnostics.Process.Start(psi);

            return true;
        }
    }
}
