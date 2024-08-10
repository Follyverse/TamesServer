using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TamesServer
{
    public class Logs
    {
        public static Logs Log;
        List<string> log = new List<string>();
        int lastWritten = 0;
        string logPath;
        public Logs()
        {
            Log = this;
            string path = AppDomain.CurrentDomain.BaseDirectory;
            logPath = path + "log.txt";
        }
          public void Add(string msg)
        {
            string s = DateTime.Now.ToString("yyyy/MM/dd\tHH:mm:ss\t") + msg;
            lock (this)
            {
                log.Add(s);
                if (log.Count % 10 == 0)
                {
                    string[] lines = new string[log.Count - lastWritten - 1];
                    for (int i = 0; i < lines.Length; i++)
                        lines[i] = log[lastWritten + i + 1];

                    File.AppendAllLines(logPath, lines);
                    lastWritten = log.Count - 1;
                }
            }
        }
    }
}
