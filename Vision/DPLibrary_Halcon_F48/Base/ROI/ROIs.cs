using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Vision;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;
using static HalconDotNet.HDrawingObject;

namespace DPLibrary_Halcon_F48
{
    public delegate void SelectedChanged(I_IsHit roi,bool value);

    public interface I_HROI : I_ROI, IDisposable
    {
        event SelectedChanged SelectedChanged;
        HRegion GetRegion();
        void Draw(HWindow win);
        void DeDraw(HWindow win);
    }

    // 判别符直接复用 ROIType 字段名：与旧格式 JSON 兼容，配合 JsonHelper 的 AllowOutOfOrderMetadataProperties 可读取旧文件（无需重存）。
    // ROIType 属性本身用 [JsonIgnore]（基类+各派生 override），避免与元数据属性名冲突。
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "ROIType")]
    [JsonDerivedType(typeof(HROI_Rectangle), "Rectangle")]
    [JsonDerivedType(typeof(HROI_Rectangle2), "Rectangle2")]
    [JsonDerivedType(typeof(HROI_Polyline), "Polygon")]
    [JsonDerivedType(typeof(HROI_Circle), "Circle")]
    [JsonDerivedType(typeof(HROI_Ellipsoid), "Ellipsoid")]
    public abstract class HROI : I_HROI, IJsonOnDeserialized
    {
        protected HRegion mRegion;
        protected HDrawingObject mDrawingROI;
        protected bool mIsROIDrawing = false;

        protected bool mIsHit = false;
        private bool _isActive = true;

        [JsonPropertyName("IsFull")]
        [ToolPropertyAttribute(DisplayName = "填充", Description = "显示时是否填充", EditorType = E_PropertyEditorType.Checkbox)]
        public bool IsFull { get; set; } = false;

        [JsonPropertyName("DispColor")]
        [ToolPropertyAttribute(DisplayName = "颜色", Description = "显示时是的颜色", EditorType = E_PropertyEditorType.SingleSelect)]
        public virtual E_VisionColor DispColor { get; set; } = E_VisionColor.Blue;

        [JsonPropertyName("DispSelectColor")]
        [ToolPropertyAttribute(DisplayName = "选中颜色", Description = "选中时显示的颜色", EditorType = E_PropertyEditorType.SingleSelect)]
        public virtual E_VisionColor DispSelectColor { get; set; } = E_VisionColor.Red;

        [Browsable(false)]
        public virtual bool IsHit
        {
            get { return mIsHit; }
            set
            {
                if (mIsHit != value)
                {
                    mIsHit = value;
                    OnPropertyChanged(nameof(IsHit));
                    if (mIsHit)//用于适配界面点击后的变更
                    {
                        OnROISelectedChanged();
                    }
                    // 延迟执行颜色更新到UI线程，防止回调重入
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        if (mDrawingROI != null)
                        {
                            HOperatorSet.SetDrawingObjectParams(mDrawingROI, "color",
                                IsHit ? DispSelectColor.ToString().ToLower() : DispColor.ToString().ToLower());
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        [JsonPropertyName("IsActive")]
        [ToolPropertyAttribute(DisplayName = "当前使用", Description = "运行时是否使用该ROI", EditorType = E_PropertyEditorType.Checkbox)]
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value)
                    return;

                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }

        public E_VisionType VisionType => E_VisionType.Halcon;

        public abstract string ROIName { get; set; }

        [JsonIgnore]
        public abstract E_ROIType ROIType { get; }

        public event SelectedChanged SelectedChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void Draw(HWindow win)
        {
            switch (ROIType)
            {
                case E_ROIType.Null:
                    if (mRegion != null)
                    {
                        HOperatorSet.SetColor(win, DispColor.ToHalconStr());
                        HOperatorSet.SetDraw(win, IsFull ? "fill" : "margin");
                        HOperatorSet.DispRegion(mRegion, win);
                    }
                    break;
                default:
                    if (mDrawingROI != null)
                    {
                        if (IsHit)
                        {
                            if (!mIsROIDrawing)
                            {
                                HOperatorSet.SetDrawingObjectParams(mDrawingROI, "color", DispSelectColor.ToHalconStr());
                                HOperatorSet.AttachDrawingObjectToWindow(win, mDrawingROI);
                                mIsROIDrawing = true;
                            }
                        }
                        else
                        {
                            DeDraw(win);
                            if (mRegion != null)
                            {
                                HOperatorSet.SetColor(win, DispColor.ToHalconStr());
                                HOperatorSet.SetDraw(win, IsFull ? "fill" : "margin");
                                HOperatorSet.DispRegion(mRegion, win);
                            }
                        }
                    }
                    break;
            }
        }

        public virtual void DeDraw(HWindow win)
        {
            if (ROIType != E_ROIType.Null)
            {
                if (mDrawingROI != null)
                {
                    if (mIsROIDrawing)
                    {
                        HOperatorSet.DetachDrawingObjectFromWindow(win, mDrawingROI);
                        mIsROIDrawing = false;
                    }
                }
            }
        }

        public void Dispose()
        {
            // 释放资源
            mRegion?.Dispose();
            mRegion = null;
            mDrawingROI?.Dispose();
            mDrawingROI = null;
        }

        public HRegion GetRegion()
        {
            return mRegion;
        }

        protected void RoiIsSelected(IntPtr drawID, IntPtr winHandle, string type)
        {
            IsHit = true;
            OnROISelectedChanged();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void OnROISelectedChanged()
        {
            SelectedChanged?.Invoke(this, IsHit);
        }

        /// <summary>STJ 在反序列化属性填充完成后调用，重建运行期对象（mRegion/mDrawingROI）。</summary>
        internal virtual void Rebuild()
        {
        }

        void IJsonOnDeserialized.OnDeserialized() => Rebuild();
    }

    public class HROI_Null : HROI
    {
        public HROI_Null(HObject region)
        {
            mRegion = new HRegion(region);
        }

        public override string ROIName { get; set; } = "ROI";

        [Browsable(false)]
        public override E_ROIType ROIType
        {
            get { return E_ROIType.Null; }
        }

    }

    public class HROI_Rectangle : HROI, I_ROI_RectangleData, INotifyPropertyChanged
    {
        public HROI_Rectangle() { }

        public HROI_Rectangle(Point startPoint, Point endPoint)
        {
            Config(startPoint.Y, startPoint.X, endPoint.Y, endPoint.X);
        }

        public HROI_Rectangle(double sy, double sx, double ey, double ex)
        {
            Config(sy, sx, ey, ex);
        }

        protected Point mLUPoint;
        protected Point mRDPoint;
        protected Point mRUPoint;
        protected Point mLDPoint;

        [Browsable(false)]
        [JsonPropertyName("ROIName")]
        public override string ROIName { get; set; } = "RecROI";

        [JsonPropertyName("LUPoint")]
        [ToolPropertyAttribute(DisplayName = "左上", Description = "左上的点坐标", EditorType = E_PropertyEditorType.Text, Order = 2)]
        public Point LUPoint
        {
            get { return mLUPoint; }
            set
            {
                double x = Math.Round(value.X, 3);
                double y = Math.Round(value.Y, 3);
                if ((x != mLUPoint.X) || (y != mLUPoint.Y))
                {
                    mLUPoint.X = x;
                    mLUPoint.Y = y;
                    OnPropertyChanged(nameof(LUPoint));
                }
            }
        }

        [JsonPropertyName("RDPoint")]
        [ToolPropertyAttribute(DisplayName = "右下", Description = "右下的点坐标", EditorType = E_PropertyEditorType.Text, Order = 2)]
        public Point RDPoint
        {
            get { return mRDPoint; }
            set
            {
                double x = Math.Round(value.X, 3);
                double y = Math.Round(value.Y, 3);
                if ((x != mRDPoint.X) || (y != mRDPoint.Y))
                {
                    mRDPoint.X = x;
                    mRDPoint.Y = y;
                    OnPropertyChanged(nameof(RDPoint));
                }
            }
        }

        [JsonPropertyName("RUPoint")]
        [ToolPropertyAttribute(DisplayName = "右上", Description = "右上的点坐标", EditorType = E_PropertyEditorType.Text, Order = 2)]
        public Point RUPoint
        {
            get { return mRUPoint; }
            set
            {
                double x = Math.Round(value.X, 3);
                double y = Math.Round(value.Y, 3);
                if ((x != mRUPoint.X) || (y != mRUPoint.Y))
                {
                    mRUPoint.X = x;
                    mRUPoint.Y = y;
                    OnPropertyChanged(nameof(RUPoint));
                }
            }
        }

        [JsonPropertyName("LDPoint")]
        [ToolPropertyAttribute(DisplayName = "左下", Description = "左下的点坐标", EditorType = E_PropertyEditorType.Text, Order = 2)]
        public Point LDPoint
        {
            get { return mLDPoint; }
            set
            {
                double x = Math.Round(value.X, 3);
                double y = Math.Round(value.Y, 3);
                if ((x != mLDPoint.X) || (y != mLDPoint.Y))
                {
                    mLDPoint.X = x;
                    mLDPoint.Y = y;
                    OnPropertyChanged(nameof(LDPoint));
                }
            }
        }

        [Browsable(false)]
        [JsonIgnore]
        public override E_ROIType ROIType
        {
            get { return E_ROIType.Rectangle; }
        }


        public void Config(double sy, double sx, double ey, double ex)
        {
            if (mRegion != null)
            {
                mRegion.Dispose();
                mRegion = null;
            }
            mRegion = new HRegion();

            HOperatorSet.SetSystem("clip_region", "false");
            HTuple clip;
            HOperatorSet.GetSystem("clip_region", out clip);
            Console.WriteLine("clip_region = " + clip.S);

            double row1 = Math.Min(sy, ey);
            double row2 = Math.Max(sy, ey);
            double col1 = Math.Min(sx, ex);
            double col2 = Math.Max(sx, ex);
            mRegion.GenRectangle1(row1, col1, row2, col2);

            mDrawingROI = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE1, row1, col1, row2, col2);

            // 定义委托并保存到字段
            mRoiUpPointCallback = new HDrawingObjectCallback(RoiResized);
            mRoiIsSelectedCallback = new HDrawingObjectCallback(RoiIsSelected);
            // 注册回调
            mDrawingROI.OnResize(mRoiUpPointCallback);
            mDrawingROI.OnDrag(mRoiUpPointCallback);
            mDrawingROI.OnSelect(mRoiIsSelectedCallback);

            LUPoint = new Point(row1, col1);
            RDPoint = new Point(row2, col2);

            RUPoint = new Point(row1, col2);
            LDPoint = new Point(row2, col1);
        }

        private HDrawingObjectCallback mRoiUpPointCallback;

        private void RoiResized(IntPtr drawID, IntPtr winHandle, string type)
        {
            HTuple row1, col1, row2, col2;
            row1 = mDrawingROI.GetDrawingObjectParams("row1");
            col1 = mDrawingROI.GetDrawingObjectParams("column1");
            row2 = mDrawingROI.GetDrawingObjectParams("row2");
            col2 = mDrawingROI.GetDrawingObjectParams("column2");

            LUPoint = new Point(row1, col1);
            RUPoint = new Point(row1, col2);
            RDPoint = new Point(row2, col2);
            LDPoint = new Point(row2, col1);

            mRegion.Dispose();
            mRegion = null;
            mRegion = new HRegion(mDrawingROI.GetDrawingObjectIconic());  // 转为区域对象

            row1.Dispose(); row1 = null;
            col1.Dispose(); col1 = null;
            row2.Dispose(); row2 = null;
            col2.Dispose(); col2 = null;
        }

        private HDrawingObjectCallback mRoiIsSelectedCallback;


        internal override void Rebuild()
        {
            // 反序列化完后，用参数重建运行期对象
            Config(LUPoint.X, LUPoint.Y, RDPoint.X, RDPoint.Y);
        }

    }

    public class HROI_Rectangle2 : HROI, I_ROI_Rectangle2Data, INotifyPropertyChanged
    {
        public HROI_Rectangle2() { }

        public HROI_Rectangle2(Point centerPoint, double angle, double l1, double l2)
        {
            Config(centerPoint.X, centerPoint.Y, angle, l1, l2);
        }

        public HROI_Rectangle2(double centerPointx, double centerPointy, double angle, double l1, double l2)
        {
            Config(centerPointx, centerPointy, angle, l1, l2);
        }

        [Browsable(false)]
        [JsonPropertyName("ROIName")]
        public override string ROIName { get; set; } = "Rec2ROI";


        Point mCenterPoint;
        double mAngle;
        double mLen1;
        double mLen2;


        [JsonPropertyName("CenterPoint")]
        [ToolPropertyAttribute(DisplayName = "中心", Description = "中心的点坐标", EditorType = E_PropertyEditorType.Text, Order = 2)]
        public Point CenterPoint
        {
            get { return mCenterPoint; }
            set
            {
                double x = Math.Round(value.X, 3);
                double y = Math.Round(value.Y, 3);
                if ((x != mCenterPoint.X) || (y != mCenterPoint.Y))
                {
                    mCenterPoint.X = x;
                    mCenterPoint.Y = y;
                    OnPropertyChanged(nameof(CenterPoint));
                }
            }
        }

        [JsonPropertyName("Angle")]
        [ToolPropertyAttribute(DisplayName = "旋转角度", Description = "旋转角度 ,单位°", EditorType = E_PropertyEditorType.Double, Max = 360, Min = 0, Order = 2)]
        public double Angle
        {
            get { return mAngle; }
            set
            {
                double a = Math.Round(value, 3);
                if (a != mAngle)
                {
                    mAngle = a;
                    OnPropertyChanged(nameof(Angle));
                }
            }
        }

        [JsonPropertyName("Len1")]
        [ToolPropertyAttribute(DisplayName = "半边1", Description = "平行与旋转箭头的边的半边长", EditorType = E_PropertyEditorType.Double, Min = 0, Order = 2)]
        public double Len1
        {
            get { return Math.Round(mLen1, 3); }
            set
            {
                double le = Math.Round(value, 3);
                if (value != mLen1)
                {
                    mLen1 = le;
                    OnPropertyChanged(nameof(Len1));
                }
            }
        }

        [JsonPropertyName("Len2")]
        [ToolPropertyAttribute(DisplayName = "半边2", Description = "垂直与旋转箭头的边的半边长", EditorType = E_PropertyEditorType.Double, Min = 0, Order = 2)]
        public double Len2
        {
            get { return Math.Round(mLen2, 3); }
            set
            {
                double se = Math.Round(value, 3);
                if (value != mLen2)
                {
                    mLen2 = se;
                    OnPropertyChanged(nameof(Len2));
                }
            }
        }

        [Browsable(false)]
        [JsonIgnore]
        public override E_ROIType ROIType
        {
            get { return E_ROIType.Rectangle2; }
        }

        public void Config(double centerPointx, double centerPointy, double angle, double l1, double l2)
        {
            if (mRegion != null)
            {
                mRegion.Dispose();
                mRegion = null;
            }
            mRegion = new HRegion();

            double longEdge = Math.Round(l1, 3);
            double shortEdge = Math.Round(l2, 3);
            double px = Math.Round(centerPointx, 3);
            double py = Math.Round(centerPointy, 3);
            double phi = Math.Round(Math.PI * (angle / 180), 3);

            mRegion.GenRectangle2(py, px, phi, longEdge, shortEdge);

            mDrawingROI = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.RECTANGLE2, py, px, phi, longEdge, shortEdge);

            // 定义委托并保存到字段
            mRoiUpPointCallback = new HDrawingObjectCallback(RoiResized);
            mRoiIsSelectedCallback = new HDrawingObjectCallback(RoiIsSelected);
            // 注册回调
            mDrawingROI.OnResize(mRoiUpPointCallback);
            mDrawingROI.OnDrag(mRoiUpPointCallback);
            mDrawingROI.OnSelect(mRoiIsSelectedCallback);

            CenterPoint = new Point(px, py);
            Angle = Math.Round(angle, 3);
            Len1 = longEdge;
            Len2 = shortEdge;
        }

        private HDrawingObjectCallback mRoiUpPointCallback;

        private void RoiResized(IntPtr drawID, IntPtr winHandle, string type)
        {
            HTuple centerX, centerY, phi, longEdge, shortEdge;
            centerY = mDrawingROI.GetDrawingObjectParams("row");
            centerX = mDrawingROI.GetDrawingObjectParams("column");
            phi = mDrawingROI.GetDrawingObjectParams("phi");
            longEdge = mDrawingROI.GetDrawingObjectParams("length1");
            shortEdge = mDrawingROI.GetDrawingObjectParams("length2");

            CenterPoint = new Point(centerX, centerY);
            Angle = (phi / Math.PI) * 180;
            Len1 = longEdge;
            Len2 = shortEdge;

            mRegion.Dispose();
            mRegion = null;
            mRegion = new HRegion(mDrawingROI.GetDrawingObjectIconic());  // 转为区域对象

            centerX.Dispose(); centerX = null;
            centerY.Dispose(); centerY = null;
            phi.Dispose(); phi = null;
            longEdge.Dispose(); longEdge = null;
            shortEdge.Dispose(); shortEdge = null;
        }

        private HDrawingObjectCallback mRoiIsSelectedCallback;

        internal override void Rebuild()
        {
            // 反序列化完后，用参数重建运行期对象
            Config(CenterPoint.X, CenterPoint.Y, Angle, Len1, Len2);
        }

    }

    public class HROI_Polyline : HROI, I_ROI_PolylineData, INotifyPropertyChanged
    {
        [JsonPropertyName("Points")]
        public List<Point> Points { get; set; }

        public int PointsCount => throw new NotImplementedException();

        public override string ROIName { get; set; } = "PolygonROI";

        [Browsable(false)]
        [JsonIgnore]
        public override E_ROIType ROIType { get { return E_ROIType.Polygon; } }

        public HROI_Polyline() { Points = new List<Point>(); }

        public HROI_Polyline(List<Point> points)
        {
            Config(points);
        }

        public void Config(List<Point> points)
        {
            if (mRegion != null)
            {
                mRegion.Dispose();
                mRegion = null;
            }
            mRegion = new HRegion();

            //点集合获取
            List<double> rs = new List<double>();
            List<double> cs = new List<double>();

            foreach (var item in points)
            {
                rs.Add(item.Y);
                cs.Add(item.X);
            }

            HTuple rows = new HTuple(rs.ToArray());
            HTuple cols = new HTuple(cs.ToArray());

            HObject p_xld = null;
            HObject region;
            HOperatorSet.GenContourPolygonXld(out p_xld, rows, cols);
            HOperatorSet.GenRegionContourXld(p_xld, out region, "filled");
            mRegion = new HRegion(region); 
            p_xld?.Dispose();

            mDrawingROI = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.XLD_CONTOUR, rows, cols);

            // 定义委托并保存到字段
            mRoiUpPointCallback = new HDrawingObjectCallback(RoiResized);
            mRoiIsSelectedCallback = new HDrawingObjectCallback(RoiIsSelected);
            // 注册回调
            mDrawingROI.OnResize(mRoiUpPointCallback);
            mDrawingROI.OnDrag(mRoiUpPointCallback);
            mDrawingROI.OnSelect(mRoiIsSelectedCallback);

            Points = points;
        }
        private HDrawingObjectCallback mRoiUpPointCallback;

        private void RoiResized(IntPtr drawID, IntPtr winHandle, string type)
        {
            HTuple rows, cols;
            rows = mDrawingROI.GetDrawingObjectParams("row");
            cols = mDrawingROI.GetDrawingObjectParams("column");

            Points.Clear();
            for (int i = 0; i < rows.Length; i++)
            {
                Points.Add(new Point(cols[i].D, rows[i].D));
            }

            mRegion.Dispose();
            mRegion = null;
            HObject p_xld = null;
            HObject region;
            HOperatorSet.GenContourPolygonXld(out p_xld, rows, cols);
            HOperatorSet.GenRegionContourXld(p_xld, out region, "filled");
            mRegion = new HRegion(region); // 转为区域对象

            rows.Dispose();
            cols.Dispose();
            p_xld?.Dispose();

        }

        private HDrawingObjectCallback mRoiIsSelectedCallback;

        /// <summary>
        /// 是否是第一次绘制
        /// </summary>
        bool isFirst = true;

        public override void Draw(HWindow win)
        {
            if (win == null) return;

            if (mDrawingROI != null && (IsHit || isFirst))
            {
                if (!mIsROIDrawing)
                {
                    HOperatorSet.SetDrawingObjectParams(mDrawingROI, "color", IsHit ? DispSelectColor.ToHalconStr() : DispColor.ToHalconStr());
                    HOperatorSet.AttachDrawingObjectToWindow(win, mDrawingROI);
                    mIsROIDrawing = true;
                    isFirst = false;
                }
            }
            else
            {
                if (mIsROIDrawing)
                {
                    DeDraw(win);
                }
                if (mRegion != null)
                {
                    HOperatorSet.SetColor(win, DispColor.ToHalconStr());
                    HOperatorSet.SetDraw(win, IsFull ? "fill" : "margin");
                    HOperatorSet.DispRegion(mRegion, win);
                }
            }
        }

        public override void DeDraw(HWindow win)
        {
            if (win == null) return;

            if (mDrawingROI != null/* && !IsHit*/)
            {
                if (mIsROIDrawing)
                {
                    mIsROIDrawing = false; 
                    HOperatorSet.DetachDrawingObjectFromWindow(win, mDrawingROI);
                }
            }
        }

        internal override void Rebuild()
        {
            // 反序列化完后，用参数重建运行期对象
            Config(Points);
        }

    }

    public class HROI_Circle : HROI, INotifyPropertyChanged
    {
        public HROI_Circle() { }

        public HROI_Circle(double centerX, double centerY, double radius)
        {
            Config(centerX, centerY, radius);
        }

        public override string ROIName { get; set; } = "CircleROI";

        [Browsable(false)]
        [JsonIgnore]
        public override E_ROIType ROIType => E_ROIType.Circle;

        [JsonPropertyName("CenterPoint")]
        [ToolPropertyAttribute(DisplayName = "中心", Description = "圆心坐标", EditorType = E_PropertyEditorType.Text, Order = 1)]
        public Point CenterPoint { get; set; }

        [JsonPropertyName("Radius")]
        [ToolPropertyAttribute(DisplayName = "半径", Description = "圆半径", EditorType = E_PropertyEditorType.Double, Min = 0, Order = 2)]
        public double Radius { get; set; }

        public void Config(double centerX, double centerY, double radius)
        {
            mRegion?.Dispose();
            mRegion = new HRegion();

            var x = Math.Round(centerX, 3);
            var y = Math.Round(centerY, 3);
            var r = Math.Round(Math.Max(1, radius), 3);
            mRegion.GenCircle(y, x, r);
            mDrawingROI = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.CIRCLE, y, x, r);

            mRoiUpPointCallback = new HDrawingObjectCallback(RoiResized);
            mRoiIsSelectedCallback = new HDrawingObjectCallback(RoiIsSelected);
            mDrawingROI.OnResize(mRoiUpPointCallback);
            mDrawingROI.OnDrag(mRoiUpPointCallback);
            mDrawingROI.OnSelect(mRoiIsSelectedCallback);

            CenterPoint = new Point(x, y);
            Radius = r;
        }

        private HDrawingObjectCallback mRoiUpPointCallback;
        private HDrawingObjectCallback mRoiIsSelectedCallback;

        private void RoiResized(IntPtr drawID, IntPtr winHandle, string type)
        {
            HTuple row = null, col = null, radius = null;
            try
            {
                row = mDrawingROI.GetDrawingObjectParams("row");
                col = mDrawingROI.GetDrawingObjectParams("column");
                radius = mDrawingROI.GetDrawingObjectParams("radius");

                CenterPoint = new Point(Math.Round(col.D, 3), Math.Round(row.D, 3));
                Radius = Math.Round(radius.D, 3);
                OnPropertyChanged(nameof(CenterPoint));
                OnPropertyChanged(nameof(Radius));

                mRegion?.Dispose();
                mRegion = new HRegion(mDrawingROI.GetDrawingObjectIconic());
            }
            finally
            {
                row?.Dispose();
                col?.Dispose();
                radius?.Dispose();
            }
        }

        internal override void Rebuild()
        {
            Config(CenterPoint.X, CenterPoint.Y, Radius);
        }
    }

    public class HROI_Ellipsoid : HROI, INotifyPropertyChanged
    {
        public HROI_Ellipsoid() { }

        public HROI_Ellipsoid(double centerX, double centerY, double angle, double radius1, double radius2)
        {
            Config(centerX, centerY, angle, radius1, radius2);
        }

        public override string ROIName { get; set; } = "EllipseROI";

        [Browsable(false)]
        [JsonIgnore]
        public override E_ROIType ROIType => E_ROIType.Ellipsoid;

        [JsonPropertyName("CenterPoint")]
        [ToolPropertyAttribute(DisplayName = "中心", Description = "椭圆中心坐标", EditorType = E_PropertyEditorType.Text, Order = 1)]
        public Point CenterPoint { get; set; }

        [JsonPropertyName("Angle")]
        [ToolPropertyAttribute(DisplayName = "角度", Description = "椭圆角度", EditorType = E_PropertyEditorType.Double, Order = 2)]
        public double Angle { get; set; }

        [JsonPropertyName("Radius1")]
        [ToolPropertyAttribute(DisplayName = "半轴1", Description = "椭圆长半轴", EditorType = E_PropertyEditorType.Double, Min = 0, Order = 3)]
        public double Radius1 { get; set; }

        [JsonPropertyName("Radius2")]
        [ToolPropertyAttribute(DisplayName = "半轴2", Description = "椭圆短半轴", EditorType = E_PropertyEditorType.Double, Min = 0, Order = 4)]
        public double Radius2 { get; set; }

        public void Config(double centerX, double centerY, double angle, double radius1, double radius2)
        {
            mRegion?.Dispose();
            mRegion = new HRegion();

            var x = Math.Round(centerX, 3);
            var y = Math.Round(centerY, 3);
            var phi = Math.Round(Math.PI * angle / 180.0, 3);
            var r1 = Math.Round(Math.Max(1, radius1), 3);
            var r2 = Math.Round(Math.Max(1, radius2), 3);
            mRegion.GenEllipse(y, x, phi, r1, r2);
            mDrawingROI = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.ELLIPSE, y, x, phi, r1, r2);

            mRoiUpPointCallback = new HDrawingObjectCallback(RoiResized);
            mRoiIsSelectedCallback = new HDrawingObjectCallback(RoiIsSelected);
            mDrawingROI.OnResize(mRoiUpPointCallback);
            mDrawingROI.OnDrag(mRoiUpPointCallback);
            mDrawingROI.OnSelect(mRoiIsSelectedCallback);

            CenterPoint = new Point(x, y);
            Angle = Math.Round(angle, 3);
            Radius1 = r1;
            Radius2 = r2;
        }

        private HDrawingObjectCallback mRoiUpPointCallback;
        private HDrawingObjectCallback mRoiIsSelectedCallback;

        private void RoiResized(IntPtr drawID, IntPtr winHandle, string type)
        {
            HTuple row = null, col = null, phi = null, radius1 = null, radius2 = null;
            try
            {
                row = mDrawingROI.GetDrawingObjectParams("row");
                col = mDrawingROI.GetDrawingObjectParams("column");
                phi = mDrawingROI.GetDrawingObjectParams("phi");
                radius1 = mDrawingROI.GetDrawingObjectParams("radius1");
                radius2 = mDrawingROI.GetDrawingObjectParams("radius2");

                CenterPoint = new Point(Math.Round(col.D, 3), Math.Round(row.D, 3));
                Angle = Math.Round(phi.D / Math.PI * 180.0, 3);
                Radius1 = Math.Round(radius1.D, 3);
                Radius2 = Math.Round(radius2.D, 3);
                OnPropertyChanged(nameof(CenterPoint));
                OnPropertyChanged(nameof(Angle));
                OnPropertyChanged(nameof(Radius1));
                OnPropertyChanged(nameof(Radius2));

                mRegion?.Dispose();
                mRegion = new HRegion(mDrawingROI.GetDrawingObjectIconic());
            }
            finally
            {
                row?.Dispose();
                col?.Dispose();
                phi?.Dispose();
                radius1?.Dispose();
                radius2?.Dispose();
            }
        }

        internal override void Rebuild()
        {
            Config(CenterPoint.X, CenterPoint.Y, Angle, Radius1, Radius2);
        }
    }
}
