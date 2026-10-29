using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;

static class ModernShape {
    public static GraphicsPath Round(RectangleF rect,float radius) {
        float d=Math.Min(radius*2,Math.Min(rect.Width,rect.Height));
        var path=new GraphicsPath();
        if(d<1) {path.AddRectangle(rect);return path;}
        path.AddArc(rect.X,rect.Y,d,d,180,90);path.AddArc(rect.Right-d,rect.Y,d,d,270,90);
        path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.X,rect.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
}

static class AccentPalette {
    public static readonly Color Default=Color.FromArgb(209,47,67);
    public static Color Mix(Color color,Color target,float amount) {return Color.FromArgb((int)(color.R+(target.R-color.R)*amount),(int)(color.G+(target.G-color.G)*amount),(int)(color.B+(target.B-color.B)*amount));}
    static double Linear(byte value) {double v=value/255.0;return v<=.04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4);}
    public static Color Ink(Color color) {double luminance=.2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);return luminance>.179 ? Color.FromArgb(18,18,20) : Color.White;}
}

class ModernButton : Button {
    public Color AccentColor=AccentPalette.Default;
    bool hover,pressed;
    public bool Dark {get;set;}
    public bool Minimal {get;set;}
    public void RefreshShape() {OnResize(EventArgs.Empty);Invalidate();}
    public ModernButton() {SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;}
    protected override void OnResize(EventArgs e) {base.OnResize(e);if(Width>0&&Height>0) using(var p=ModernShape.Round(new RectangleF(0,0,Width,Height),Minimal ? 5 : 10)) {var old=Region;Region=new Region(p);if(old!=null)old.Dispose();}}
    protected override void OnMouseEnter(EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e) {hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e) {pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e) {pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        bool accent=Convert.ToString(Tag)=="primary"||DialogResult==DialogResult.OK;
        Color top=accent ? AccentPalette.Mix(AccentColor,Color.White,.12f) : Dark ? Color.FromArgb(57,57,61) : Color.FromArgb(255,255,255);
        Color bottom=accent ? AccentPalette.Mix(AccentColor,Color.Black,.12f) : Dark ? Color.FromArgb(40,40,44) : Color.FromArgb(237,237,239);
        if(Minimal) {top=Dark ? Color.FromArgb(accent?52:37,accent?52:37,accent?55:40) : Color.FromArgb(accent?230:248,accent?230:248,accent?233:249);bottom=top;}
        if(hover) {top=ControlPaint.Light(top,.12f);bottom=ControlPaint.Light(bottom,.08f);}
        if(pressed) {top=ControlPaint.Dark(top,.1f);bottom=ControlPaint.Dark(bottom,.1f);}
        if(!Enabled) {top=Dark ? Color.FromArgb(39,39,42) : Color.FromArgb(236,236,239);bottom=top;}
        using(var path=ModernShape.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Minimal ? 5 : 10)) {
            using(var fill=new LinearGradientBrush(ClientRectangle,top,bottom,90f)) g.FillPath(fill,path);
            using(var border=new Pen(accent ? AccentPalette.Mix(AccentColor,Color.White,.3f) : Dark ? Color.FromArgb(79,79,85) : Color.FromArgb(212,212,217))) g.DrawPath(border,path);
        }
        var foreground=accent ? AccentPalette.Ink(AccentColor) : Dark ? Color.FromArgb(238,237,243) : Color.FromArgb(45,43,53);
        if(Minimal)foreground=Dark ? Color.FromArgb(238,238,242) : Color.FromArgb(36,36,40);
        if(!Enabled) foreground=Dark ? Color.FromArgb(119,119,132) : Color.FromArgb(153,153,165);
        TextRenderer.DrawText(g,Text,Font,ClientRectangle,foreground,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.PreserveGraphicsClipping);
        if(Focused&&ShowFocusCues) ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-5,-5),foreground,Color.Transparent);
    }
}

class ModernComboBox : ComboBox {
    public Color AccentColor=AccentPalette.Default;
    public ModernComboBox() {DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=24;FlatStyle=FlatStyle.Flat;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnResize(EventArgs e) {base.OnResize(e);if(Width>0&&Height>0)using(var p=ModernShape.Round(new RectangleF(0,0,Width,Height),7)) {var old=Region;Region=new Region(p);if(old!=null)old.Dispose();}}
    protected override void OnSelectedIndexChanged(EventArgs e) {base.OnSelectedIndexChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);
        bool dark=BackColor.GetBrightness()<.5f;
        using(var path=ModernShape.Round(new RectangleF(.5f,.5f,Width-1,Height-1),7)) using(var pen=new Pen(Focused ? AccentColor : dark ? Color.FromArgb(78,73,89) : Color.FromArgb(207,201,217))) g.DrawPath(pen,path);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(10,0,Math.Max(1,Width-34),Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        using(var pen=new Pen(ForeColor,1.6f)) g.DrawLines(pen,new Point[] {new Point(Width-20,Height/2-2),new Point(Width-16,Height/2+2),new Point(Width-12,Height/2-2)});
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0 ? AccentColor : BackColor)) e.Graphics.FillRectangle(brush,e.Bounds);
        if(e.Index>=0) TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,Rectangle.Inflate(e.Bounds,-8,0),(e.State&DrawItemState.Selected)!=0 ? AccentPalette.Ink(AccentColor) : ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);
    }
}

class ModernCheckBox : Control {
    public Color AccentColor=AccentPalette.Default;
    bool isChecked;
    public event EventHandler CheckedChanged;
    public bool Checked {
        get {return isChecked;}
        set {if(isChecked==value)return;isChecked=value;Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);}
    }
    public ModernCheckBox() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
        Size=new Size(150,26);Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.CheckButton;
    }
    protected override void OnClick(EventArgs e) {Checked=!Checked;Focus();base.OnClick(e);}
    protected override void OnKeyDown(KeyEventArgs e) {if(e.KeyCode==Keys.Space) {e.Handled=true;e.SuppressKeyPress=true;OnClick(EventArgs.Empty);}base.OnKeyDown(e);}
    protected override void OnTextChanged(EventArgs e) {base.OnTextChanged(e);if(Parent!=null)Parent.PerformLayout(this,"Text");Invalidate();}
    protected override void OnFontChanged(EventArgs e) {base.OnFontChanged(e);if(Parent!=null)Parent.PerformLayout(this,"Font");Invalidate();}
    protected override void OnGotFocus(EventArgs e) {base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e) {base.OnLostFocus(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        int glyph=Math.Max(16,Font.Height-1),y=(Height-glyph)/2;
        using(var path=ModernShape.Round(new RectangleF(.5f,y+.5f,glyph-1,glyph-1),4)) {
            if(Checked) using(var brush=new SolidBrush(AccentColor)) g.FillPath(brush,path);
            using(var pen=new Pen(Checked ? AccentPalette.Mix(AccentColor,Color.White,.2f) : Color.FromArgb(126,124,139))) g.DrawPath(pen,path);
        }
        if(Checked) using(var pen=new Pen(AccentPalette.Ink(AccentColor),Math.Max(2,glyph/8f))) {g.DrawLines(pen,new PointF[] {new PointF(glyph*.25f,y+glyph*.5f),new PointF(glyph*.44f,y+glyph*.7f),new PointF(glyph*.77f,y+glyph*.3f)});}
        int textX=glyph+8;
        TextRenderer.DrawText(g,Text,Font,new Rectangle(textX,0,Math.Max(1,Width-textX),Height),Enabled ? ForeColor : SystemColors.GrayText,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.PreserveGraphicsClipping);
        if(Focused&&ShowFocusCues) ControlPaint.DrawFocusRectangle(g,new Rectangle(textX-2,0,Math.Max(1,Width-textX+1),Height));
    }
    public override Size GetPreferredSize(Size proposedSize) {
        var size=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        return new Size(size.Width+Math.Max(16,Font.Height-1)+26,Math.Max(24,size.Height+6));
    }
    protected override AccessibleObject CreateAccessibilityInstance() {return new CheckAccessibility(this);}
    class CheckAccessibility : ControlAccessibleObject {
        readonly ModernCheckBox owner;
        public CheckAccessibility(ModernCheckBox control) : base(control) {owner=control;}
        public override AccessibleStates State {get {return base.State|(owner.Checked ? AccessibleStates.Checked : AccessibleStates.None);}}
        public override string DefaultAction {get {return "Toggle";}}
        public override void DoDefaultAction() {if(owner.Enabled)owner.Checked=!owner.Checked;}
    }
}

class GlassPanel : Panel {
    public bool Dark {get;set;}
    public bool Minimal {get;set;}
    public void RefreshShape() {OnResize(EventArgs.Empty);Invalidate();}
    public GlassPanel() {DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);Padding=new Padding(18);}
    protected override void OnResize(EventArgs e) {base.OnResize(e);if(Width>0&&Height>0) using(var p=ModernShape.Round(new RectangleF(0,0,Width,Height),Minimal ? 6 : 18)) {var old=Region;Region=new Region(p);if(old!=null)old.Dispose();}}
    protected override void OnPaintBackground(PaintEventArgs e) {
        if(Width<1||Height<1)return;
        var top=Dark ? Color.FromArgb(45,45,49) : Color.FromArgb(255,255,255);
        var bottom=Dark ? Color.FromArgb(33,33,36) : Color.FromArgb(241,241,244);
        if(Minimal) {top=Dark ? Color.FromArgb(30,30,33) : Color.FromArgb(250,250,251);bottom=top;}
        using(var fill=new LinearGradientBrush(ClientRectangle,top,bottom,80f)) e.Graphics.FillRectangle(fill,ClientRectangle);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var p=ModernShape.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Minimal ? 6 : 18)) using(var pen=new Pen(Dark ? Color.FromArgb(76,76,82) : Color.FromArgb(215,215,221))) e.Graphics.DrawPath(pen,p);
        if(!Minimal) using(var glow=new LinearGradientBrush(new Rectangle(0,0,Math.Max(1,Width),2),Color.FromArgb(120,235,235,240),Color.Transparent,0f)) e.Graphics.FillRectangle(glow,24,0,Math.Max(1,Width-48),1);
    }
}

class WorkspaceTabs : Panel {
    readonly FlowLayoutPanel strip;
    readonly Panel body;
    readonly List<Control> pages=new List<Control>();
    readonly List<ModernButton> buttons=new List<ModernButton>();
    int selected;
    public bool Dark {get;private set;}
    public event EventHandler SelectedIndexChanged;
    public int SelectedIndex {get {return selected;} set {if(value<0||value>=pages.Count)return;selected=value;UpdatePages();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
    public WorkspaceTabs() {
        DoubleBuffered=true;BackColor=Color.Transparent;
        strip=new FlowLayoutPanel {Dock=DockStyle.Top,Height=50,Padding=new Padding(0,5,0,5),BackColor=Color.Transparent,WrapContents=false};
        body=new Panel {Dock=DockStyle.Fill,BackColor=Color.Transparent};Controls.Add(body);Controls.Add(strip);
    }
    public void AddPage(string name,Control page) {
        int index=pages.Count;pages.Add(page);page.Dock=DockStyle.Fill;body.Controls.Add(page);
        var button=new ModernButton {Text=name,Width=130,Height=36,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(0,0,10,0),Cursor=Cursors.Hand};
        button.Click+=delegate {SelectedIndex=index;};buttons.Add(button);strip.Controls.Add(button);UpdatePages();
    }
    public void SetTheme(bool dark) {Dark=dark;foreach(var button in buttons)button.Dark=dark;UpdatePages();}
    void UpdatePages() {for(int i=0;i<pages.Count;i++) {pages[i].Visible=i==selected;buttons[i].Tag=i==selected ? "primary" : "secondary";buttons[i].Invalidate();}}
}

static class WindowChrome {
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    public static void Apply(Form form,bool dark) {
        if(!form.IsHandleCreated)return;
        try {int value=dark?1:0;DwmSetWindowAttribute(form.Handle,20,ref value,4);int rounded=2;DwmSetWindowAttribute(form.Handle,33,ref rounded,4);} catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
    }
}

