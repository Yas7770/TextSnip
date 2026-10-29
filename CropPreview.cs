using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

class ScreenshotPreview : PictureBox {
    public Color AccentColor=AccentPalette.Default;
    Point start,current;
    bool dragging,cropping;
    public event EventHandler SelectionChanged;
    public event EventHandler ImageChanged;
    public bool Cropping {get {return cropping;}}
    public new Image Image {
        get {return base.Image;}
        set {CancelCrop();base.Image=value;if(ImageChanged!=null)ImageChanged(this,EventArgs.Empty);}
    }
    public ScreenshotPreview() {SizeMode=PictureBoxSizeMode.Zoom;DoubleBuffered=true;TabStop=true;}
    public RectangleF ImageBounds {
        get {
            if(Image==null || ClientSize.Width<1 || ClientSize.Height<1)return RectangleF.Empty;
            float scale=Math.Min((float)ClientSize.Width/Image.Width,(float)ClientSize.Height/Image.Height);
            float width=Image.Width*scale,height=Image.Height*scale;
            return new RectangleF((ClientSize.Width-width)/2,(ClientSize.Height-height)/2,width,height);
        }
    }
    RectangleF ViewSelection {
        get {
            if(Image==null)return RectangleF.Empty;
            Rectangle pixels=SelectedPixels;RectangleF bounds=ImageBounds;
            return new RectangleF(bounds.Left+pixels.Left*bounds.Width/Image.Width,bounds.Top+pixels.Top*bounds.Height/Image.Height,pixels.Width*bounds.Width/Image.Width,pixels.Height*bounds.Height/Image.Height);
        }
    }
    public Rectangle SelectedPixels {
        get {
            if(Image==null)return Rectangle.Empty;
            int left=Math.Max(0,Math.Min(start.X,current.X)),top=Math.Max(0,Math.Min(start.Y,current.Y));
            int right=Math.Min(Image.Width,Math.Max(start.X,current.X)),bottom=Math.Min(Image.Height,Math.Max(start.Y,current.Y));
            if(right<=left||bottom<=top)return Rectangle.Empty;
            return Rectangle.FromLTRB(left,top,right,bottom);
        }
    }
    public void BeginCrop() {if(Image==null)return;cropping=true;start=current=Point.Empty;Cursor=Cursors.Cross;Focus();Changed();}
    public void CancelCrop() {cropping=false;dragging=false;Capture=false;Cursor=Cursors.Default;start=current=Point.Empty;Changed();}
    public Bitmap CreateCroppedImage() {
        Rectangle pixels=SelectedPixels;
        if(Image==null || pixels.Width<1 || pixels.Height<1)return null;
        var output=new Bitmap(pixels.Width,pixels.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(output)) {graphics.CompositingMode=CompositingMode.SourceCopy;graphics.DrawImage(Image,new Rectangle(0,0,pixels.Width,pixels.Height),pixels,GraphicsUnit.Pixel);}
        return output;
    }
    Point Clamp(Point point) {
        var bounds=ImageBounds;if(Image==null||bounds.Width<=0||bounds.Height<=0)return Point.Empty;
        return new Point(Math.Max(0,Math.Min(Image.Width,(int)Math.Round((point.X-bounds.Left)*Image.Width/bounds.Width))),Math.Max(0,Math.Min(Image.Height,(int)Math.Round((point.Y-bounds.Top)*Image.Height/bounds.Height))));
    }
    protected override void OnMouseDown(MouseEventArgs e) {base.OnMouseDown(e);if(cropping && e.Button==MouseButtons.Left && ImageBounds.Contains(e.Location)) {start=current=Clamp(e.Location);dragging=true;Capture=true;Changed();}}
    protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e);if(dragging) {current=Clamp(e.Location);Changed();}}
    protected override void OnMouseUp(MouseEventArgs e) {base.OnMouseUp(e);if(dragging&&e.Button==MouseButtons.Left) {current=Clamp(e.Location);dragging=false;Capture=false;Changed();}}
    protected override void OnResize(EventArgs e) {base.OnResize(e);if(cropping)Changed();}
    void Changed() {Invalidate();if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);}
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(!cropping||Image==null)return;
        RectangleF bounds=ImageBounds,selected=RectangleF.Intersect(bounds,ViewSelection);
        using(var region=new Region(bounds)) {
            if(selected.Width>0&&selected.Height>0)region.Exclude(selected);
            using(var dim=new SolidBrush(Color.FromArgb(130,0,0,0)))e.Graphics.FillRegion(dim,region);
        }
        if(selected.Width>0&&selected.Height>0) {
            using(var pen=new Pen(AccentColor,2))e.Graphics.DrawRectangle(pen,selected.X,selected.Y,selected.Width,selected.Height);
        }
    }
}

