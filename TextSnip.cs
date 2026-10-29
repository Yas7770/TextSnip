using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

static class Program {
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static void Main() {
        bool first;
        using (var mutex = new Mutex(true, "Local\\TextSnip.Application", out first)) {
            if (!first) { MessageBox.Show("TextSnip is already running. Press Ctrl + Shift + 2 to capture text, or use its tray icon.", "TextSnip"); return; }
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext());
        }
    }
}

class TrayContext : ApplicationContext {
    readonly MainWindow window;
    public TrayContext() {
        window=new MainWindow();
        window.FormClosed+=delegate { ExitThread(); };
        var handle=window.Handle;
    }
    protected override void Dispose(bool disposing) { if(disposing) window.Dispose(); base.Dispose(disposing); }
}

class MainWindow : Form {
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    readonly NotifyIcon tray;
    readonly TextBox result;
    readonly Label status;
    readonly Button capture;
    readonly ModernCheckBox enhance;
    readonly ComboBox captureDelay;
    readonly WorkspaceTabs previews;
    readonly ScreenshotPreview screenshot;
    bool darkMode, minimalStyle;
    bool busy, quitting, hideBackgroundNotice;
    public MainWindow() {
        try { hideBackgroundNotice=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\TextSnip", "HideBackgroundNotice", 0))==1; } catch { hideBackgroundNotice=false; }
        darkMode=AppTheme.ReadDarkMode();minimalStyle=AppTheme.ReadMinimalStyle();
        ShowInTaskbar=false;
        Icon = Icon.ExtractAssociatedIcon(typeof(MainWindow).Assembly.Location);
        Text = "TextSnip"; MinimumSize = new Size(960, 600); StartPosition = FormStartPosition.CenterScreen;
        var area = Screen.PrimaryScreen.WorkingArea;
        Size = new Size(Math.Min(1180, area.Width), Math.Min(840, area.Height));
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(244,246,250);
        var layout = new TableLayoutPanel { Name="MainLayout",Dock=DockStyle.Fill, Padding=new Padding(28,22,28,14), RowCount=4, ColumnCount=1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        var header = new TableLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,310));
        var brand=new TableLayoutPanel {Name="BrandLayout",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var logo=new PictureBox {Name="BrandLogo",Width=48,Height=48,Anchor=AnchorStyles.Left,Margin=Padding.Empty,SizeMode=PictureBoxSizeMode.Zoom,Image=new Icon(Icon,48,48).ToBitmap()};
        brand.Controls.Add(logo,0,0);
        var brandText=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
        brandText.Controls.Add(new Label {Name="BrandTitle",Text="TextSnip",Font=new Font("Segoe UI",22,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,0,0,3)});
        brandText.Controls.Add(new Label {Name="BrandSubtitle",Tag="muted",Text="Capture, review, then copy what you need.",AutoSize=true,Margin=new Padding(0,0,0,3)});
        brand.Controls.Add(brandText,1,0);
        header.Controls.Add(brand,0,0);
        var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,8,0,0),Margin=Padding.Empty };
        capture=MakeButton("New capture",true,160); capture.Click+=delegate { CaptureArea(previews.SelectedIndex==1); };
        var copy=MakeButton("Copy all",false,120); copy.Click+=delegate { CopyText(); };
        actions.Controls.Add(capture); actions.Controls.Add(copy); header.Controls.Add(actions,1,0); var headerGlass=new GlassPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(18,12,18,12),Margin=new Padding(0,0,0,10)}; headerGlass.Controls.Add(header); layout.Controls.Add(headerGlass,0,0);
        var toolbar=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent,100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var timing=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,6,0,0)};
        timing.Controls.Add(new Label {Tag="muted",Text="Delay",AutoSize=true,Margin=new Padding(0,5,8,0),ForeColor=Color.FromArgb(100,112,133)});
        captureDelay=new ModernComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=115,AccessibleName="Capture delay"};
        captureDelay.Items.AddRange(new object[] {"No delay","3 seconds","5 seconds"}); captureDelay.SelectedIndex=0;
        timing.Controls.Add(captureDelay); toolbar.Controls.Add(timing,0,0);
        var reading=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(0,2,8,0),Margin=Padding.Empty };
        var larger=MakeButton("A+",false,46); larger.Height=30;
        var smaller=MakeButton("A-",false,46); smaller.Height=30;
        var wrap=new ModernCheckBox {Text="Word wrap",Checked=true,AutoSize=true,Margin=new Padding(8,5,8,0),ForeColor=Color.FromArgb(65,78,99)};
        enhance=new ModernCheckBox {Text="Enhance image",Checked=false,AutoSize=true,Margin=new Padding(8,5,8,0),ForeColor=Color.FromArgb(65,78,99)};
        reading.Controls.Add(enhance); reading.Controls.Add(wrap); reading.Controls.Add(smaller); reading.Controls.Add(larger); toolbar.Controls.Add(reading,1,0); var toolbarGlass=new GlassPanel {Dock=DockStyle.Fill,Padding=new Padding(14,4,14,4),Margin=new Padding(0,0,0,8)}; toolbarGlass.Controls.Add(toolbar); layout.Controls.Add(toolbarGlass,0,1);
        var editor=new GlassPanel {Tag="canvas",Dock=DockStyle.Fill,Padding=new Padding(24),Margin=Padding.Empty};
        result=new TextBox {Multiline=true,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,ScrollBars=ScrollBars.Vertical,WordWrap=true,AcceptsReturn=true,AcceptsTab=true,HideSelection=false,Font=new Font("Segoe UI",13),BackColor=Color.White,ForeColor=Color.FromArgb(30,41,59)};
        editor.Controls.Add(result);
        previews=new WorkspaceTabs {Dock=DockStyle.Fill,Margin=Padding.Empty};
        previews.SelectedIndexChanged+=delegate {copy.Visible=previews.SelectedIndex==0;};
        previews.AddPage("Text",editor);
        var imagePage=new GlassPanel {Padding=new Padding(14)};
        var imageTools=new TableLayoutPanel {Dock=DockStyle.Top,Height=52,Padding=new Padding(4,6,4,8),ColumnCount=2,RowCount=1};
        imageTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));imageTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));imageTools.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var imageActions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
        var cropActions=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Margin=Padding.Empty,FlowDirection=FlowDirection.RightToLeft};
        var saveImage=MakeButton("Save PNG",false,120); saveImage.Height=32; saveImage.Click+=delegate { SaveScreenshot(); };
        var copyImage=MakeButton("Copy image",false,120); copyImage.Height=32; copyImage.Click+=delegate { CopyScreenshot(); };
        var cropImage=MakeButton("Crop",false,100);cropImage.Height=32;cropImage.Enabled=false;
        var applyCrop=MakeButton("Apply crop",true,120);applyCrop.Height=32;applyCrop.Visible=false;
        var cancelCrop=MakeButton("Cancel",false,90);cancelCrop.Height=32;cancelCrop.Visible=false;
        imageActions.Controls.Add(saveImage);imageActions.Controls.Add(copyImage);
        cropActions.Controls.Add(cropImage);cropActions.Controls.Add(applyCrop);cropActions.Controls.Add(cancelCrop);
        imageTools.Controls.Add(imageActions,0,0);imageTools.Controls.Add(cropActions,1,0);
        screenshot=new ScreenshotPreview {Dock=DockStyle.Fill};
        Action<bool> setCropMode=delegate(bool active) {
            imageTools.SuspendLayout();
            cropImage.Visible=!active;applyCrop.Visible=cancelCrop.Visible=active;
            saveImage.Enabled=copyImage.Enabled=!active && screenshot.Image!=null;
            imageTools.ResumeLayout(true);
            if(active)screenshot.BeginCrop();else screenshot.CancelCrop();
            applyCrop.Enabled=!screenshot.SelectedPixels.IsEmpty;
            status.Text=active ? "Drag over the image, then Apply crop. Cancel keeps the original." : "Screenshot ready.";
        };
        cropImage.Click+=delegate {setCropMode(true);};
        cancelCrop.Click+=delegate {setCropMode(false);};
        applyCrop.Click+=delegate {
            try {
                var cropped=screenshot.CreateCroppedImage();
                if(cropped==null) {status.Text="Drag over the image to select an area first.";return;}
                var previous=screenshot.Image;screenshot.Image=cropped;if(previous!=null)previous.Dispose();
                setCropMode(false);status.Text="Screenshot cropped. Save PNG or Copy image.";
            } catch(Exception ex) {setCropMode(false);MessageBox.Show(this,ex.Message,"Crop screenshot");}
        };
        screenshot.SelectionChanged+=delegate {applyCrop.Enabled=screenshot.Cropping && !screenshot.SelectedPixels.IsEmpty;};
        screenshot.ImageChanged+=delegate {setCropMode(false);cropImage.Enabled=screenshot.Image!=null;};
        imagePage.Controls.Add(screenshot); imagePage.Controls.Add(imageTools); previews.AddPage("Screenshot",imagePage);
        layout.Controls.Add(previews,0,2);
        wrap.CheckedChanged+=delegate { result.WordWrap=wrap.Checked; result.ScrollBars=wrap.Checked ? ScrollBars.Vertical : ScrollBars.Both; };
        larger.Click+=delegate { ChangeTextSize(1); }; smaller.Click+=delegate { ChangeTextSize(-1); };
        var footer=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));
        status=new Label {Tag="muted",Text="Ready  |  Ctrl + Shift + 2 to capture",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(100,112,133),Font=new Font("Segoe UI",9)};
        var count=new Label {Tag="muted",Text="0 characters",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.FromArgb(100,112,133),Font=new Font("Segoe UI",9)};
        var selectionTimer=new System.Windows.Forms.Timer {Interval=150};
        selectionTimer.Tick+=delegate { copy.Text=result.SelectionLength>0 ? "Copy selection" : "Copy all"; }; selectionTimer.Start();
        Disposed+=delegate { selectionTimer.Dispose(); };
        result.TextChanged+=delegate { count.Text=result.TextLength.ToString("N0")+" characters"; copy.Enabled=result.TextLength>0; }; copy.Enabled=false;
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,96));
        var settings=MakeButton("Settings",false,88); settings.Height=28; settings.Font=new Font("Segoe UI",9); settings.Click+=delegate { OpenSettings(); };
        footer.Controls.Add(status,0,0); footer.Controls.Add(count,1,0); footer.Controls.Add(settings,2,0); layout.Controls.Add(footer,0,3); Controls.Add(layout);
        var menu=new ContextMenuStrip();
        var open=menu.Items.Add("Open",null,delegate { OpenWindow(); });
        open.Font=new Font(menu.Font,FontStyle.Bold);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Capture text (Ctrl+Shift+2)",null,delegate { CaptureArea(false); });
        menu.Items.Add("Take screenshot (Ctrl+Shift+3)",null,delegate { CaptureArea(true); });
        menu.Items.Add("Settings",null,delegate { OpenSettings(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",null,delegate { quitting=true; Close(); });
        tray=new NotifyIcon { Text="TextSnip - Ctrl+Shift+2",Icon=Icon,Visible=true,ContextMenuStrip=menu };
        tray.DoubleClick+=delegate { OpenWindow(); };
        ApplyInterfaceStyle(); AppTheme.ApplyStyle(menu,darkMode,minimalStyle);
        Disposed+=delegate { if(screenshot.Image!=null) screenshot.Image.Dispose(); };
        FormClosing+=delegate(object sender, FormClosingEventArgs e) {
            if(!quitting && e.CloseReason==CloseReason.UserClosing) {
                e.Cancel=true;
                if(!hideBackgroundNotice) {
                    using(var notice=new BackgroundNotice(Icon)) {
                        AppTheme.ApplyStyle(notice,darkMode,minimalStyle);
                        if(notice.ShowDialog(this)==DialogResult.OK && notice.DontShowAgain) {
                            try {
                                using(var key=Registry.CurrentUser.CreateSubKey(@"Software\TextSnip")) key.SetValue("HideBackgroundNotice",1,RegistryValueKind.DWord);
                                hideBackgroundNotice=true;
                            } catch(Exception ex) { MessageBox.Show(this,"Your preference could not be saved.\n"+ex.Message,"TextSnip"); }
                        }
                    }
                }
                Hide(); ShowInTaskbar=false;
            }
        };
    }
    protected override void OnPaintBackground(PaintEventArgs e) {
        if(ClientSize.Width<1||ClientSize.Height<1)return;
        if(minimalStyle) {e.Graphics.Clear(darkMode ? Color.FromArgb(24,24,26) : Color.FromArgb(239,239,241));return;}
        Color top=darkMode ? Color.FromArgb(32,32,34) : Color.FromArgb(239,239,241);
        Color bottom=darkMode ? Color.FromArgb(22,22,24) : Color.FromArgb(229,229,232);
        using(var fill=new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,top,bottom,65f)) e.Graphics.FillRectangle(fill,ClientRectangle);
    }
    void OpenWindow() {
        ShowInTaskbar=true; Show();
        if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal;
        BringToFront(); Activate();
    }
    void ApplyInterfaceStyle() {
        SuspendLayout();
        var layout=Controls.Find("MainLayout",true)[0] as TableLayoutPanel; layout.RowStyles[0].SizeType=minimalStyle ? SizeType.Absolute : SizeType.AutoSize;layout.RowStyles[0].Height=minimalStyle ? 88 : 0;
        var brand=Controls.Find("BrandLayout",true)[0] as TableLayoutPanel;
        brand.ColumnStyles[0].Width=minimalStyle ? 0 : 64;
        Controls.Find("BrandLogo",true)[0].Visible=!minimalStyle;
        Controls.Find("BrandSubtitle",true)[0].Visible=!minimalStyle;
        var title=Controls.Find("BrandTitle",true)[0];
        float size=minimalStyle ? 18 : 22;
        if(title.Font.Size!=size) {var previous=title.Font;title.Font=new Font("Segoe UI",size,FontStyle.Bold);previous.Dispose();}
        AppTheme.ApplyStyle(this,darkMode,minimalStyle);
        ResumeLayout(true);
    }
    void OpenSettings() {
        using(var dialog=new SettingsWindow(darkMode,minimalStyle,Icon)) {
            if(Visible) dialog.StartPosition=FormStartPosition.CenterParent;

            if(dialog.ShowDialog(Visible ? this : null)==DialogResult.OK) {
                try {
                    using(var key=Registry.CurrentUser.CreateSubKey(@"Software\TextSnip")) {key.SetValue("DarkMode",dialog.DarkMode ? 1 : 0,RegistryValueKind.DWord);key.SetValue("MinimalStyle",dialog.Minimal ? 1 : 0,RegistryValueKind.DWord);key.SetValue("AccentColor",dialog.AccentColor.ToArgb(),RegistryValueKind.DWord);}
                    darkMode=dialog.DarkMode; minimalStyle=dialog.Minimal; ApplyInterfaceStyle(); AppTheme.ApplyStyle(tray.ContextMenuStrip,darkMode,minimalStyle); Invalidate(true);
                } catch(Exception ex) { MessageBox.Show("Settings could not be saved.\n"+ex.Message,"TextSnip"); }
            }
        }
    }
    void SaveScreenshot() {
        if(screenshot.Image==null) {status.Text="Take a screenshot first."; return;}
        using(var dialog=new SaveFileDialog {Filter="PNG image|*.png",DefaultExt="png",AddExtension=true,FileName="TextSnip-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"}) {
            if(dialog.ShowDialog(this)==DialogResult.OK) try {screenshot.Image.Save(dialog.FileName,ImageFormat.Png); status.Text="Screenshot saved.";} catch(Exception ex) {MessageBox.Show(this,ex.Message,"Save screenshot");}
        }
    }
    void CopyScreenshot() {
        if(screenshot.Image==null) return;
        try {Clipboard.SetImage(screenshot.Image);status.Text="Screenshot copied.";} catch(Exception ex) {MessageBox.Show(this,ex.Message,"Copy screenshot");}
    }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        WindowChrome.Apply(this,darkMode);
        bool text=RegisterHotKey(Handle,1,0x4006,0x32), image=RegisterHotKey(Handle,2,0x4006,0x33);
        if(status!=null && (!text || !image)) status.Text="A shortcut is unavailable. Use the tray menu.";
    }
    protected override void OnHandleDestroyed(EventArgs e) {UnregisterHotKey(Handle,1);UnregisterHotKey(Handle,2);base.OnHandleDestroyed(e);}
    static Button MakeButton(string text,bool primary,int width) {
        var button=new ModernButton {Tag=primary ? "primary" : "secondary",Text=text,Width=width,Height=40,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,BackColor=primary ? Color.FromArgb(209,47,67) : Color.White,ForeColor=primary ? Color.White : Color.FromArgb(51,65,85),Margin=new Padding(8,0,0,0),Font=new Font("Segoe UI",10,FontStyle.Bold)};
        button.FlatAppearance.BorderColor=primary ? Color.FromArgb(209,47,67) : Color.FromArgb(218,225,235);
        button.FlatAppearance.MouseOverBackColor=primary ? Color.FromArgb(180,33,52) : Color.FromArgb(235,241,250);
        return button;
    }
    void ChangeTextSize(float delta) {
        float size=Math.Max(10,Math.Min(24,result.Font.Size+delta));
        var old=result.Font; result.Font=new Font("Segoe UI",size); old.Dispose();
    }
    protected override void WndProc(ref Message m) { if(m.Msg==0x312) CaptureArea(m.WParam.ToInt32()==2); base.WndProc(ref m); }
    protected override void Dispose(bool disposing) { if(disposing) { tray.Dispose(); } base.Dispose(disposing); }
    void CopyText() { if(string.IsNullOrWhiteSpace(result.Text)) return; try { bool selected=result.SelectionLength>0; Clipboard.SetText(selected ? result.SelectedText : result.Text); status.Text=selected ? "Selected text copied." : "All text copied."; } catch(Exception ex) { status.Text="Clipboard busy. Try Copy text again."; MessageBox.Show(ex.Message,"Clipboard"); } }
    async void CaptureArea(bool imageOnly=false) {
        if(busy) return; busy=true; capture.Enabled=false;
        bool wasVisible=Visible; Hide(); ShowInTaskbar=false;
        string path=Path.Combine(Path.GetTempPath(),"TextSnip-"+Guid.NewGuid().ToString("N")+".png");
        string enhancedPath=path+".enhanced.png";
        try {
            int seconds=captureDelay.SelectedIndex==1 ? 3 : captureDelay.SelectedIndex==2 ? 5 : 0;
            if(seconds>0) {
                using(var countdown=new CaptureCountdown(Icon)) {
                    AppTheme.ApplyStyle(countdown,darkMode,minimalStyle); countdown.Show();
                    for(int remaining=seconds;remaining>0;remaining--) {
                        if(countdown.Cancelled || countdown.IsDisposed) break;
                        countdown.SetRemaining(remaining);
                        await Task.Delay(1000);
                    }
                    bool cancelled=countdown.Cancelled || countdown.IsDisposed;
                    countdown.Hide();
                    if(cancelled) { status.Text="Capture cancelled."; if(wasVisible) OpenWindow(); return; }
                }
            }
            await Task.Delay(180);
            Rectangle desktop=SystemInformation.VirtualScreen;
            using(var shot=new Bitmap(desktop.Width,desktop.Height,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(shot)) g.CopyFromScreen(desktop.Location,Point.Empty,desktop.Size);
                using(var overlay=new SelectionWindow(shot,desktop)) {
                    if(overlay.ShowDialog()!=DialogResult.OK) { if(wasVisible) OpenWindow(); return; }
                    Rectangle r=overlay.Selection;
                    using(var crop=shot.Clone(r,PixelFormat.Format32bppArgb)) {
                        if(imageOnly) {
                            var previous=screenshot.Image; screenshot.Image=new Bitmap(crop); if(previous!=null) previous.Dispose();
                            previews.SelectedIndex=1; status.Text="Screenshot ready. Save PNG or Copy image."; OpenWindow(); return;
                        }
                        double scale=Math.Min(2.0,4000.0/Math.Max(crop.Width,crop.Height));
                        using(var original=new Bitmap(crop,Math.Max(1,(int)(crop.Width*scale)),Math.Max(1,(int)(crop.Height*scale)))) original.Save(path,ImageFormat.Png);
                        using(var prepared=OcrImage.Prepare(crop)) prepared.Save(enhancedPath,ImageFormat.Png);
                    }
                }
            }
            status.Text="Reading text...";
            bool useEnhanced=enhance.Checked;
            string text=await Task.Run(()=>Recognize(useEnhanced ? enhancedPath : path));
            if(string.IsNullOrWhiteSpace(text)) {
                status.Text="Trying another image treatment...";
                text=await Task.Run(()=>Recognize(useEnhanced ? path : enhancedPath));
            }
            if(string.IsNullOrWhiteSpace(text)) { status.Text="No text found. Try a clearer or larger area."; OpenWindow(); }
            else { result.Text=text; result.Select(0,0); status.Text="Ready. Select text to copy a portion, or use Copy all."; previews.SelectedIndex=0; OpenWindow(); result.Focus(); }
        } catch(Exception ex) { OpenWindow(); status.Text="Capture failed."; MessageBox.Show(this,ex.Message,"TextSnip",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        finally { try { File.Delete(path); File.Delete(enhancedPath); } catch {} busy=false; capture.Enabled=true; }
    }
    static string Recognize(string path) {
        string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe");
        var info=new ProcessStartInfo(ps,"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Ocr.ps1")+"\" -ImagePath \""+path+"\"") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8 };
        using(var p=Process.Start(info)) {
            var output=p.StandardOutput.ReadToEndAsync(); var error=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(60000)) { p.Kill(); throw new Exception("Text recognition timed out. Try a smaller area."); }
            Task.WaitAll(output,error); if(p.ExitCode!=0) throw new Exception(error.Result); return output.Result;
        }
    }
}

static class AppTheme {
    public static bool ReadDarkMode() {
        try {return Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\TextSnip","DarkMode",0))==1;} catch {return false;}
    }
    public static bool ReadMinimalStyle() {
        try {return Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\TextSnip","MinimalStyle",0))==1;} catch {return false;}
    }
    public static Color ReadAccentColor() {
        try {int argb=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\TextSnip","AccentColor",AccentPalette.Default.ToArgb()));Color color=Color.FromArgb(argb);return Color.FromArgb(color.R,color.G,color.B);} catch {return AccentPalette.Default;}
    }
    public static void Apply(Control root,bool dark) {ApplyStyle(root,dark,false);}
    public static void ApplyStyle(Control root,bool dark,bool minimal) {ApplyAccent(root,dark,minimal,ReadAccentColor());}
    public static void ApplyAccent(Control root,bool dark,bool minimal,Color chosen) {
        Color accent=minimal ? Color.FromArgb(126,126,134) : chosen;
        Color background=dark ? Color.FromArgb(24,24,26) : Color.FromArgb(238,238,240);
        Color surface=dark ? Color.FromArgb(33,33,36) : Color.White;
        Color foreground=dark ? Color.FromArgb(242,239,246) : Color.FromArgb(39,35,46);
        root.ForeColor=Convert.ToString(root.Tag)=="muted" ? (dark ? Color.FromArgb(160,155,173) : Color.FromArgb(111,104,124)) : foreground;
        root.BackColor=background;
        if(root is Panel || root is Label || root is ModernCheckBox || root is PictureBox) root.BackColor=Color.Transparent;
        if(root is TextBox || root is ComboBox) root.BackColor=surface;
        var glass=root as GlassPanel;
        if(glass!=null) {glass.Dark=dark;glass.Minimal=minimal;glass.RefreshShape();}
        var button=root as ModernButton;
        if(button!=null) {button.Dark=dark;button.Minimal=minimal;button.AccentColor=accent;button.RefreshShape();}
        var check=root as ModernCheckBox;if(check!=null) {check.AccentColor=accent;check.Invalidate();}
        var combo=root as ModernComboBox;if(combo!=null) {combo.AccentColor=accent;combo.Invalidate();}
        var preview=root as ScreenshotPreview;if(preview!=null) {preview.AccentColor=accent;preview.Invalidate();}
        var menu=root as ContextMenuStrip;
        if(menu!=null) {
            menu.Renderer=new ToolStripProfessionalRenderer(new MenuColors(dark,accent));
            foreach(ToolStripItem item in menu.Items) {item.BackColor=background;item.ForeColor=foreground;}
        }
        foreach(Control child in root.Controls) ApplyAccent(child,dark,minimal,chosen);
        var tabs=root as WorkspaceTabs;if(tabs!=null)tabs.SetTheme(dark);
        var form=root as Form;if(form!=null) {WindowChrome.Apply(form,dark);form.Invalidate(true);}
    }
}
class MenuColors : ProfessionalColorTable {
    readonly bool dark;readonly Color accent;
    public MenuColors(bool enabled,Color color) {dark=enabled;accent=color;UseSystemColors=false;}
    public override Color ToolStripDropDownBackground {get {return dark ? Color.FromArgb(33,33,36) : Color.FromArgb(248,248,249);}}
    public override Color ImageMarginGradientBegin {get {return ToolStripDropDownBackground;}}
    public override Color ImageMarginGradientMiddle {get {return ToolStripDropDownBackground;}}
    public override Color ImageMarginGradientEnd {get {return ToolStripDropDownBackground;}}
    public override Color MenuItemSelected {get {return AccentPalette.Mix(accent,dark ? Color.Black : Color.White,dark ? .7f : .85f);}}
    public override Color MenuItemBorder {get {return accent;}}
    public override Color MenuBorder {get {return dark ? Color.FromArgb(66,66,71) : Color.FromArgb(215,215,219);}}
}

class SettingsWindow : Form {
    readonly ModernCheckBox dark;
    readonly ComboBox style;
    Color accentColor;
    public Color AccentColor {get {return accentColor;}}
    public bool DarkMode {get {return dark.Checked;}}
    public bool Minimal {get {return style.SelectedIndex==1;}}
    public SettingsWindow(bool enabled,Icon icon) : this(enabled,false,icon) {}
    public SettingsWindow(bool enabled,bool minimal,Icon icon) {
        Text="TextSnip settings";Icon=icon;ClientSize=new Size(480,430);accentColor=AppTheme.ReadAccentColor();
        StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
        Font=new Font("Segoe UI",10);
        Controls.Add(new Label {Text="Appearance",Left=24,Top=20,AutoSize=true,Font=new Font("Segoe UI",16,FontStyle.Bold)});
        Controls.Add(new Label {Text="Interface style",Left=24,Top=71,AutoSize=true});
        style=new ModernComboBox {Name="InterfaceStyle",AccessibleName="Interface style",DropDownStyle=ComboBoxStyle.DropDownList,Left=24,Top=100,Width=432};
        style.Items.AddRange(new object[] {"Playful","Minimal"});style.SelectedIndex=minimal ? 1 : 0;Controls.Add(style);
        var description=new Label {Left=24,Top=143,Width=432,Height=42,Tag="muted"};Controls.Add(description);
        dark=new ModernCheckBox {Text="Dark mode",Checked=enabled,Left=24,Top=274,Width=350,Height=28,AccessibleName="Dark mode"};Controls.Add(dark);
        Controls.Add(new Label {Text="Text: Ctrl + Shift + 2    Screenshot: Ctrl + Shift + 3",Left=24,Top=323,Width=432,Height=25,Tag="muted"});
        var cancel=new ModernButton {Text="Cancel",DialogResult=DialogResult.Cancel,Left=244,Top=374,Width=95,Height=32,FlatStyle=FlatStyle.Flat};Controls.Add(cancel);
        var save=new ModernButton {Text="Save",DialogResult=DialogResult.OK,Left=351,Top=374,Width=105,Height=32,FlatStyle=FlatStyle.Flat};Controls.Add(save);
        Controls.Add(new Label {Text="Accent colour (Playful only)",Left=24,Top=194,AutoSize=true});
        var chooseColor=new ModernButton {Name="ChooseAccentColor",Text="Choose colour...",Left=24,Top=222,Width=210,Height=34,AccessibleName="Choose accent colour"};Controls.Add(chooseColor);
        var swatch=new Panel {Name="AccentSwatch",Left=250,Top=224,Width=32,Height=30};Controls.Add(swatch);
        var colorValue=new Label {Left=294,Top=229,Width=162,Height=24};Controls.Add(colorValue);
        AcceptButton=save;CancelButton=cancel;
        Action preview=delegate {
            description.Text=Minimal ? "Flat surfaces, a quieter header and restrained accents for focused work." : "Glass-style surfaces, rounded controls and your chosen accent colour.";
            chooseColor.Enabled=!Minimal;swatch.Visible=!Minimal;
            colorValue.Text=Minimal ? "Fixed neutral palette" : "#"+accentColor.R.ToString("X2")+accentColor.G.ToString("X2")+accentColor.B.ToString("X2");
            AppTheme.ApplyAccent(this,dark.Checked,Minimal,accentColor);swatch.BackColor=accentColor;
        };
        chooseColor.Click+=delegate {
            if(Minimal)return;
            using(var picker=new ColorDialog {Color=accentColor,FullOpen=true,AnyColor=true}) {
                if(picker.ShowDialog(this)==DialogResult.OK) {accentColor=Color.FromArgb(picker.Color.R,picker.Color.G,picker.Color.B);preview();}
            }
        };
        dark.CheckedChanged+=delegate {preview();};style.SelectedIndexChanged+=delegate {preview();};preview();
    }
}

class CaptureCountdown : Form {
    readonly Label message;
    public bool Cancelled {get; private set;}
    public CaptureCountdown(Icon icon) {
        Text="TextSnip capture"; Icon=icon; ClientSize=new Size(300,100);
        FormBorderStyle=FormBorderStyle.FixedToolWindow; ShowInTaskbar=false; TopMost=true; StartPosition=FormStartPosition.Manual;
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;
        Location=new Point(area.Right-Width-24,area.Top+24);
        Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(244,246,250); KeyPreview=true;
        message=new Label {Left=16,Top=16,Width=268,Height=26,Font=new Font("Segoe UI",12,FontStyle.Bold)}; Controls.Add(message);
        var cancel=new ModernButton {Text="Cancel (Esc)",Left=16,Top=54,Width=125,Height=30};
        cancel.Click+=delegate { Cancelled=true; Hide(); }; Controls.Add(cancel);
        KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) {Cancelled=true; Hide();} };
        FormClosing+=delegate {Cancelled=true;};
    }
    public void SetRemaining(int seconds) {message.Text="Select an area in "+seconds+"...";}
}

static class OcrImage {
    public static Bitmap Prepare(Bitmap source) {
        // Normalize polarity and contrast before scaling, retaining antialiased edges.
        using(var gray=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb)) {
            using(var graphics=Graphics.FromImage(gray)) graphics.DrawImageUnscaled(source,0,0);
            var data=gray.LockBits(new Rectangle(0,0,gray.Width,gray.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try {
                byte[] pixels=new byte[data.Stride*data.Height]; Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                int[] histogram=new int[256]; long edgeTotal=0; int edgeCount=0;
                for(int y=0;y<gray.Height;y++) for(int x=0;x<gray.Width;x++) {
                    int i=y*data.Stride+x*4;
                    int luminance=(pixels[i+2]*77+pixels[i+1]*150+pixels[i]*29+128)>>8;
                    histogram[luminance]++;
                    if(x<3 || y<3 || x>=gray.Width-3 || y>=gray.Height-3) { edgeTotal+=luminance; edgeCount++; }
                }
                int total=gray.Width*gray.Height, cumulative=0, low=0, high=255;
                for(int i=0;i<256;i++) { cumulative+=histogram[i]; if(cumulative>total/2000) { low=i; break; } }
                cumulative=0;
                for(int i=255;i>=0;i--) { cumulative+=histogram[i]; if(cumulative>total/2000) { high=i; break; } }
                bool invert=edgeCount>0 && edgeTotal/edgeCount<128;
                bool stretch=high-low>=24;
                for(int y=0;y<gray.Height;y++) for(int x=0;x<gray.Width;x++) {
                    int i=y*data.Stride+x*4;
                    int value=(pixels[i+2]*77+pixels[i+1]*150+pixels[i]*29+128)>>8;
                    if(stretch) value=Math.Max(0,Math.Min(255,(value-low)*255/(high-low)));
                    if(invert) value=255-value;
                    pixels[i]=pixels[i+1]=pixels[i+2]=(byte)value; pixels[i+3]=255;
                }
                Marshal.Copy(pixels,0,data.Scan0,pixels.Length);
            } finally { gray.UnlockBits(data); }
            const int padding=16;
            double scale=Math.Min(3.0, (4000.0-padding*2)/Math.Max(gray.Width,gray.Height));
            int width=Math.Max(1,(int)(gray.Width*scale)),height=Math.Max(1,(int)(gray.Height*scale));
            var output=new Bitmap(width+padding*2,height+padding*2,PixelFormat.Format32bppArgb);
            using(var graphics=Graphics.FromImage(output)) {
                graphics.Clear(Color.White);
                graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                using(var attributes=new ImageAttributes()) {
                    attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(gray,new Rectangle(padding,padding,width,height),0,0,gray.Width,gray.Height,GraphicsUnit.Pixel,attributes);
                }
            }
            return output;
        }
    }
}

class BackgroundNotice : Form {
    readonly ModernCheckBox dontShowAgain;
    public bool DontShowAgain { get { return dontShowAgain.Checked; } }
    public BackgroundNotice(Icon icon) {
        Text="TextSnip is still running"; Icon=icon;
        ClientSize=new Size(460,225); StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=false;
        Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(244,246,250);
        Controls.Add(new Label {Text="Ready when you need it",Left=24,Top=20,AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold),ForeColor=Color.FromArgb(23,32,53)});
        Controls.Add(new Label {Text="TextSnip will keep running in the system tray.\nPress Ctrl + Shift + 2 to capture text.\nTo quit, right-click the tray icon and choose Exit.",Left=24,Top=62,Width=410,Height=70,ForeColor=Color.FromArgb(65,78,99)});
        dontShowAgain=new ModernCheckBox {Text="Don't show this again",Left=24,Top=145,Width=300,AutoSize=true}; Controls.Add(dontShowAgain);
        var okay=new ModernButton {Text="Got it",DialogResult=DialogResult.OK,Left=326,Top=179,Width=110,Height=32,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(209,47,67),ForeColor=Color.White};
        Controls.Add(okay); AcceptButton=okay;
    }
}

class SelectionWindow : Form {
    readonly Bitmap shot; Point start,current; bool dragging;
    public Rectangle Selection { get { return Rectangle.FromLTRB(Math.Min(start.X,current.X),Math.Min(start.Y,current.Y),Math.Max(start.X,current.X),Math.Max(start.Y,current.Y)); } }
    public SelectionWindow(Bitmap image,Rectangle bounds) {
        shot=image; FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual; Bounds=bounds;
        TopMost=true; ShowInTaskbar=false; DoubleBuffered=true; Cursor=Cursors.Cross; KeyPreview=true;
        KeyDown+=delegate(object s,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) { DialogResult=DialogResult.Cancel; Close(); } };
        MouseDown+=delegate(object s,MouseEventArgs e) { if(e.Button==MouseButtons.Right) { DialogResult=DialogResult.Cancel; Close(); return; } start=current=e.Location; dragging=true; Capture=true; Invalidate(); };
        MouseMove+=delegate(object s,MouseEventArgs e) { if(dragging) { current=e.Location; Invalidate(); } };
        MouseUp+=delegate(object s,MouseEventArgs e) { if(!dragging) return; current=new Point(Math.Max(0,Math.Min(ClientSize.Width,e.X)),Math.Max(0,Math.Min(ClientSize.Height,e.Y))); dragging=false; Capture=false; if(Selection.Width>=5 && Selection.Height>=5) { DialogResult=DialogResult.OK; Close(); } else Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.DrawImageUnscaled(shot,0,0);
        using(var shade=new SolidBrush(Color.FromArgb(105,0,0,0))) e.Graphics.FillRectangle(shade,ClientRectangle);
        if(dragging && Selection.Width>0 && Selection.Height>0) { e.Graphics.DrawImage(shot,Selection,Selection,GraphicsUnit.Pixel); using(var pen=new Pen(Color.FromArgb(244,99,116),2)) e.Graphics.DrawRectangle(pen,Selection); }
        e.Graphics.FillRectangle(Brushes.Black,20,20,450,42); e.Graphics.DrawString("Drag around text to capture  |  Esc to cancel",Font,Brushes.White,32,32);
    }
}







