using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;

// User-operated launcher. Never activates an existing RP8 window.
internal static class RP8LiveLauncher {
    [STAThread] static int Main(string[] args) {
        string root=AppDomain.CurrentDomain.BaseDirectory;
        string home=Path.Combine(root,"rp8-automation","live");
        var json=new JavaScriptSerializer();
        try {
            string sessionPath=Path.Combine(home,"session.json");
            if(File.Exists(sessionPath)) {
                try {
                    var session=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(sessionPath,Encoding.UTF8));
                    var process=Process.GetProcessById(Convert.ToInt32(session["processId"]));
                    string path=process.MainModule.FileName;
                    if(!process.HasExited&&(string.Equals(path,Path.Combine(root,"RP8Live.exe"),StringComparison.OrdinalIgnoreCase)||
                        string.Equals(path,Path.Combine(root,"RP8Live.next.exe"),StringComparison.OrdinalIgnoreCase))) return 0;
                } catch { }
            }
            string source=args.Length==1?Path.GetFullPath(args[0]):Path.Combine(home,"实时设计演示.rp");
            if(!File.Exists(source)||!source.EndsWith(".rp",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Pass an existing .rp project");
            string folder=Path.Combine(home,"projects",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            Directory.CreateDirectory(folder);
            string copy=Path.Combine(folder,"AI实时设计.rp");
            using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var output=new FileStream(copy,FileMode.CreateNew,FileAccess.Write))input.CopyTo(output);
            string executable=File.Exists(Path.Combine(root,"RP8Live.next.exe"))?Path.Combine(root,"RP8Live.next.exe"):Path.Combine(root,"RP8Live.exe");
            Process.Start(new ProcessStartInfo(executable,"\""+copy+"\"") {
                UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Normal,WorkingDirectory=root
            });
            return 0;
        } catch(Exception e) {
            Directory.CreateDirectory(home);
            File.WriteAllText(Path.Combine(home,"launcher-error.json"),json.Serialize(new{error=e.ToString()}),new UTF8Encoding(false));
            return 1;
        }
    }
}
