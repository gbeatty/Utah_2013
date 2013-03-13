using Shell32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace videoTimeReader
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            StreamWriter outStream = new StreamWriter("videoTimes.txt");

            string dir = System.Environment.CurrentDirectory;

            DirectoryInfo di = new DirectoryInfo(dir);
            foreach (var fileInfo in di.EnumerateFiles())
            {
                var details = GetDetails(fileInfo);

                if (fileInfo.Name.Contains(".MP4") && !fileInfo.Name.Contains(".mta"))
                {
                    DateTime time = fileInfo.CreationTime + new TimeSpan(12, 0, 0);
                    DateTime utc = fileInfo.CreationTimeUtc + new TimeSpan(14, 0, 0);
                    outStream.WriteLine(fileInfo.Name + "," + time + "," + utc + "," + details["Length"]);

                }

            }

            outStream.Close();
        }

        private static Dictionary<string, string> GetDetails(FileInfo fi)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            Shell shl = new Shell();
            Folder folder = shl.NameSpace(fi.DirectoryName);
            FolderItem item = folder.ParseName(fi.Name);

            for (int i = 0; i < 150; i++)
            {
                string dtlDesc = folder.GetDetailsOf(null, i);
                string dtlVal = folder.GetDetailsOf(item, i);

                if (dtlVal == null || dtlVal == "")
                    continue;

                ret.Add(dtlDesc, dtlVal);
            }
            return ret;
        }
    }
}
