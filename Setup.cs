using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;
class Setup {
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TextSnip");
        if(args.Length>0 && args[0]=="--extract") { Directory.CreateDirectory(args[1]); Extract("TextSnip.exe",args[1]); Extract("Ocr.ps1",args[1]); return; }
        using(var form=new Form { Text="Install TextSnip",Width=490,Height=350,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false }) {
            form.Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            form.Font=new System.Drawing.Font("Segoe UI",10);
            form.Controls.Add(new Label {Text="TextSnip\nSelect screen text. Copy it. Paste anywhere.",Left=24,Top=20,Width=430,Height=55});
            form.Controls.Add(new Label {Text="Installs for your account. No administrator access needed.",Left=24,Top=80,Width=430,Height=30});
            var desktop=new CheckBox {Text="Add a shortcut to the desktop",Left=24,Top=115,Width=420,Checked=true}; form.Controls.Add(desktop);
            var taskbar=new CheckBox {Text="Help me pin TextSnip to the taskbar",Left=24,Top=147,Width=420,Checked=false}; form.Controls.Add(taskbar);
            form.Controls.Add(new Label {Text="You complete pinning from the running app's taskbar icon.",Left=44,Top=175,Width=410,Height=26,Font=new System.Drawing.Font("Segoe UI",9)});
            var startup=new CheckBox {Text="Start TextSnip when I sign in",Left=24,Top=209,Width=400,Checked=true}; form.Controls.Add(startup);
            var install=new Button {Text="Install and launch",Left=260,Top=260,Width=185,Height=34}; form.Controls.Add(install);
            install.Click+=delegate {
                try {
                    foreach(var p in Process.GetProcessesByName("TextSnip")) { try { if(string.Equals(p.MainModule.FileName,Path.Combine(folder,"TextSnip.exe"),StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Exit TextSnip using its tray menu, then install again."); return; } } catch {} }
                    Directory.CreateDirectory(folder); Extract("TextSnip.exe",folder); Extract("Ocr.ps1",folder); Extract("Uninstall.ps1",folder);
                    if(desktop.Checked) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"TextSnip.lnk"),folder);
                    Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"TextSnip.lnk"),folder);
                    using(var run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                        if(startup.Checked) run.SetValue("TextSnip","\""+Path.Combine(folder,"TextSnip.exe")+"\""); else run.DeleteValue("TextSnip",false);
                    }
                    using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TextSnip")) {
                        key.SetValue("DisplayName","TextSnip"); key.SetValue("DisplayVersion","2.4.0"); key.SetValue("Publisher","TextSnip");
                        key.SetValue("InstallLocation",folder); key.SetValue("DisplayIcon",Path.Combine(folder,"TextSnip.exe"));
                        key.SetValue("UninstallString","powershell.exe -NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(folder,"Uninstall.ps1")+"\"");
                        key.SetValue("NoModify",1); key.SetValue("NoRepair",1);
                    }
                    Process.Start(Path.Combine(folder,"TextSnip.exe"));
                    if(taskbar.Checked) MessageBox.Show(form,"TextSnip is now running in the system tray.\n\n1. Right-click its tray icon and choose Open.\n2. Right-click the opened app's taskbar icon.\n3. Choose 'Pin to taskbar'.\n\nThis final step is completed by you in Windows.","Pin TextSnip to the taskbar",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    form.Close();
                } catch(Exception ex) { MessageBox.Show(ex.Message,"Installation failed"); }
            };
            Application.Run(form);
        }
    }
    static void Extract(string name,string folder) { using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) using(var output=File.Create(Path.Combine(folder,name))) input.CopyTo(output); }
    static void Shortcut(string path,string folder) {
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); dynamic link=shell.CreateShortcut(path);
        link.TargetPath=Path.Combine(folder,"TextSnip.exe"); link.WorkingDirectory=folder; link.Description="Copy text from any screen area"; link.Save();
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }
}
