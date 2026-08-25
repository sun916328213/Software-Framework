using DPLibrary_Base_F48;
using DPLibrary_Base_F48.Vision;
using HalconDotNet;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using static HalconDotNet.HDrawingObject;

namespace DPLibrary_Halcon_F48
{
    public interface I_HXLD : I_XLD, IDisposable
    {
        event SelectedChanged SelectedChanged;
        HXLDCont GetXLD();
        void Draw(HWindow win);
        void DeDraw(HWindow win);
    }

    // 判别符直接复用 XLDType 字段名：与旧格式 JSON 兼容，配合 JsonHelper 的 AllowOutOfOrderMetadataProperties 可读取旧文件（无需重存）。
    // XLDType 属性本身用 [JsonIgnore]（基类+各派生 override），避免与元数据属性名冲突。
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "XLDType")]
    [JsonDerivedType(typeof(HXLD_Line), "Line")]
    [JsonDerivedType(typeof(HXLD_SectorCircle), "SectorCircle")]
    public abstract class HXLD2 : I_HXLD, IJsonOnDeserialized
    {
        protected HXLDCont mXLD;
        protected HDrawingObject mDrawingXLD;
        protected bool mIsDrawing = false;

        protected bool mIsHit = false;

        [JsonPropertyName("DispColor")]
        [ToolPropertyAttribute(DisplayName = "颜色", Description = "显示时是的颜色", EditorType = E_PropertyEditorType.SingleSelect)]
        public virtual E_VisionColor DispColor { get; set; } = E_VisionColor.Green;

        [JsonPropertyName("DispSelectColor")]
        [ToolPropertyAttribute(DisplayName = "选中颜色", Description = "选中时显示的颜色", EditorType = E_PropertyEditorType.SingleSelect)]
        public virtual E_VisionColor DispSelectColor { get; set; } = E_VisionColor.Red;

        [JsonPropertyName("LineSize")]
        [ToolPropertyAttribute(DisplayName = "线宽", Description = "绘制时的线宽", EditorType = E_PropertyEditorType.Double,Min = 1)]
        public double LineSize { get; set; } = 2;

        [Browsable(false)]
        public bool IsHit
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
                        IsSelected(IntPtr.Zero, IntPtr.Zero, null);
                    }
                    // 延迟执行颜色更新到UI线程，防止回调重入
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        if (mDrawingXLD != null)
                        {
                            HOperatorSet.SetDrawingObjectParams(mDrawingXLD, "color",
                                IsHit ? DispSelectColor.ToString().ToLower() : DispColor.ToString().ToLower());
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        public E_VisionType VisionType => E_VisionType.Halcon;

        protected string mXLDName = "XLD";
        [JsonPropertyName("XLDName")]
        public string XLDName 
        {
            get { return mXLDName; }
            set 
            {   
                if (value != mXLDName)
                {
                    mXLDName = value;
                    OnPropertyChanged(nameof(XLDName));
                }
            }
        } 

        [JsonIgnore]
        public abstract E_XLDType XLDType { get; }

        public event SelectedChanged SelectedChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void Draw(HWindow win)
        {
            if (win == null) return;

            switch (XLDType)
            {
                case E_XLDType.Null:
                    if (mXLD != null)
                    {
                        HOperatorSet.SetColor(win, DispColor.ToHalconStr());
                        HOperatorSet.SetLineWidth(win,LineSize);
                        HOperatorSet.DispXld(mXLD, win);
                    }
                    break;
                default:
                    if (mDrawingXLD != null)
                    {
                        if (!mIsDrawing)
                        {
                            HOperatorSet.SetDrawingObjectParams(mDrawingXLD, "color", IsHit ? DispSelectColor.ToHalconStr() : DispColor.ToHalconStr());
                            HOperatorSet.AttachDrawingObjectToWindow(win, mDrawingXLD);
                            mIsDrawing = true;
                        }
                    }
                    break;
            }
        }

        public virtual void DeDraw(HWindow win)
        {
            if (win == null) return;

            if (XLDType != E_XLDType.Null)
            {
                if (mDrawingXLD != null)
                {
                    if (mIsDrawing)
                    {
                        HOperatorSet.DetachDrawingObjectFromWindow(win, mDrawingXLD);
                        mIsDrawing = false;
                    }
                }
            }
        }

        public void Dispose()
        {
            // 释放资源
            mXLD?.Dispose();
            mXLD = null;
            mDrawingXLD?.Dispose();
            mDrawingXLD = null;
        }

        public HXLDCont GetXLD()
        {
            return mXLD;
        }

        protected void IsSelected(IntPtr drawID, IntPtr winHandle, string type)
        {
            IsHit = true;
            OnSelectedChanged();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void OnSelectedChanged()
        {
            SelectedChanged?.Invoke(this, IsHit);
        }

        /// <summary>STJ 在反序列化属性填充完成后调用，重建运行期对象（mXLD/mDrawingXLD）。</summary>
        internal virtual void Rebuild()
        {
        }

        void IJsonOnDeserialized.OnDeserialized() => Rebuild();
    }

    public class HXLD_Null : HXLD2
    {
        public HXLD_Null(HObject xld)
        {
            mXLD = new HXLDCont(xld);
        }


        [Browsable(false)]
        public override E_XLDType XLDType
        {
            get { return E_XLDType.Null; }
        }
    }

    public class HXLD_Line : HXLD2, I_XLD_LineData
    {
        public HXLD_Line() //必须否则反序列化找不到默认构造函数
        {
        }

        public HXLD_Line(Point startPoint, Point endPoint)
        {
            Config(startPoint.X, startPoint.Y, endPoint.X,  endPoint.Y);
            XLDName = "Line";
        }

        public HXLD_Line(double sx , double sy, double ex , double ey)
        {
            Config(sx, sy, ex,  ey);
            XLDName = "Line";
        }

        Point mStartPoint;
        Point mEndPoint;

        public void Config(double sx, double sy, double ex, double ey)
        {
            if (mXLD != null)
            {
                mXLD.Dispose();
                mXLD = null;
            }

            mXLD = new HXLDCont(new double[] { sy, ey }, new double[] { sx, ex });

            HOperatorSet.SetSystem("clip_region", "false");
            HTuple clip;
            HOperatorSet.GetSystem("clip_region", out clip);
            Console.WriteLine("clip_region = " + clip.S);

            mDrawingXLD = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.LINE, sy, sx, ey, ex);

            // 定义委托并保存到字段
            mXLDResizedCallback = new HDrawingObjectCallback(XLDResized);
            mSelectedCallback = new HDrawingObjectCallback(IsSelected);
            // 注册回调
            mDrawingXLD.OnResize(mXLDResizedCallback);
            mDrawingXLD.OnDrag(mXLDResizedCallback);
            mDrawingXLD.OnSelect(mSelectedCallback);

            StartPoint = new Point(sx,sy);
            EndPoint = new Point(ex,ey);
        }

        [Browsable(false)]
        [JsonIgnore]
        public override E_XLDType XLDType
        {
            get { return E_XLDType.Line; }
        }

        [JsonPropertyName("StartPoint")]
        [ToolPropertyAttribute(DisplayName = "起始点", Description = "线段起始点", EditorType = E_PropertyEditorType.Text, Order = 2)]
        public Point StartPoint
        {
            get { return mStartPoint; }
            set
            {
                double x = Math.Round(value.X, 3);
                double y = Math.Round(value.Y, 3);
                if ((x != mStartPoint.X) || (y != mStartPoint.Y))
                {
                    mStartPoint.X = x;
                    mStartPoint.Y = y;
                    OnPropertyChanged(nameof(StartPoint));
                }
            }
        }

        [JsonPropertyName("EndPoint")]
        [ToolPropertyAttribute(DisplayName = "结束点", Description = "线段结束点", EditorType = E_PropertyEditorType.Text, Order = 2)]
        public Point EndPoint
        {
            get { return mEndPoint; }
            set
            {
                double x = Math.Round(value.X, 3);
                double y = Math.Round(value.Y, 3);
                if ((x != mEndPoint.X) || (y != mEndPoint.Y))
                {
                    mEndPoint.X = x;
                    mEndPoint.Y = y;
                    OnPropertyChanged(nameof(EndPoint));
                }
            }
        }

        private HDrawingObjectCallback mXLDResizedCallback;

        private void XLDResized(IntPtr drawID, IntPtr winHandle, string type)
        {
            HTuple row1, col1, row2, col2;
            row1 = mDrawingXLD.GetDrawingObjectParams("row1");
            col1 = mDrawingXLD.GetDrawingObjectParams("column1");
            row2 = mDrawingXLD.GetDrawingObjectParams("row2");
            col2 = mDrawingXLD.GetDrawingObjectParams("column2");

            StartPoint = new Point(col1, row1);
            EndPoint = new Point(col2, row2);

            mXLD.Dispose();
            mXLD = null;
            mXLD = new HXLDCont(new double[] { row1, row2 }, new double[] { col1, col2 });

            row1.Dispose(); row1 = null;
            col1.Dispose(); col1 = null;
            row2.Dispose(); row2 = null;
            col2.Dispose(); col2 = null;
        }

        private HDrawingObjectCallback mSelectedCallback;

        internal override void Rebuild()
        {
            // 反序列化完后，用参数重建运行期对象
            Config(StartPoint.X, StartPoint.Y, EndPoint.X, EndPoint.Y);
        }
    }

    public class HXLD_SectorCircle : HXLD2, I_XLD_SectorCircleData
    {
        public HXLD_SectorCircle() //必须否则反序列化找不到默认构造函数
        {
        }

        public HXLD_SectorCircle(double sx, double sy, double r, double sp,double ep)
        {
            Config(sx, sy, r, sp,ep);
            XLDName = "Circle";
        }

        public void Config(double sx, double sy, double r, double sp, double ep)
        {
            if (mXLD != null)
            {
                mXLD.Dispose();
                mXLD = null;
            }

            HOperatorSet.GenCircleContourXld(
                out HObject hObject,
                sy, sx, r,
                sp, ep, "positive", 2.0);

            mXLD = new HXLDCont(hObject);

            mDrawingXLD = HDrawingObject.CreateDrawingObject(HDrawingObject.HDrawingObjectType.CIRCLE_SECTOR, sy, sx, r, sp,ep);

            // 定义委托并保存到字段
            mXLDResizedCallback = new HDrawingObjectCallback(XLDResized);
            mSelectedCallback = new HDrawingObjectCallback(IsSelected);
            // 注册回调
            mDrawingXLD.OnResize(mXLDResizedCallback);
            mDrawingXLD.OnDrag(mXLDResizedCallback);
            mDrawingXLD.OnSelect(mSelectedCallback);

            CenterPoint = new Point(sx, sy);
            Radius = r;
            StartAngle = Math.Round(sp / Math.PI * 180, 3);
            EndAngle = Math.Round(ep / Math.PI * 180, 3);
        }

        [Browsable(false)]
        [JsonIgnore]
        public override E_XLDType XLDType
        {
            get { return E_XLDType.SectorCircle; }
        }

        Point mCenterPoint;
        double mRadius;
        double mStartAngle;
        double mEndAngle;

        [JsonPropertyName("CenterPoint")]
        [ToolPropertyAttribute(DisplayName = "圆心", Description = "圆心", EditorType = E_PropertyEditorType.Text, Order = 2)]
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

        [JsonPropertyName("Radius")]
        [ToolPropertyAttribute(DisplayName = "半径", Description = "半径", EditorType = E_PropertyEditorType.Double, Order = 2)]
        public double Radius
        {
            get { return mRadius; }
            set
            {
                double r = Math.Round(value, 3);
                if (r != mRadius)
                {
                    mRadius = r;
                    OnPropertyChanged(nameof(Radius));
                }
            }
        }

        [JsonPropertyName("StartAngle")]
        [ToolPropertyAttribute(DisplayName = "起始角度", Description = "起始角度", EditorType = E_PropertyEditorType.Double, Order = 2)]
        public double StartAngle
        {
            get { return mStartAngle; }
            set
            {
                double r = Math.Round(value, 3);
                if (r != mStartAngle)
                {
                    mStartAngle = r;
                    OnPropertyChanged(nameof(StartAngle));
                }
            }
        }

        [JsonPropertyName("EndAngle")]
        [ToolPropertyAttribute(DisplayName = "结束角度", Description = "结束角度", EditorType = E_PropertyEditorType.Double, Order = 2)]
        public double EndAngle
        {
            get { return mEndAngle; }
            set
            {
                double r = Math.Round(value, 3);
                if (r != mEndAngle)
                {
                    mEndAngle = r;
                    OnPropertyChanged(nameof(EndAngle));
                }
            }
        }


        private HDrawingObjectCallback mXLDResizedCallback;

        private void XLDResized(IntPtr drawID, IntPtr winHandle, string type)
        {
            HTuple vals;
            HOperatorSet.GetDrawingObjectParams(
                mDrawingXLD,
                new HTuple(new[] { "row", "column", "radius", "start_angle", "end_angle" }),
                out vals);

            CenterPoint = new Point(vals[1], vals[0]);
            Radius = Math.Round(vals[2].D, 3);
            StartAngle = Math.Round(vals[3].D / Math.PI * 180, 3);
            EndAngle = Math.Round(vals[4].D / Math.PI * 180, 3);

            mXLD?.Dispose();
            mXLD = null;

            HOperatorSet.GenCircleContourXld(
                out HObject hObject,
                vals[0], vals[1], vals[2].D,
                vals[3].D, vals[4].D, "positive", 2.0);
            mXLD = new HXLDCont(hObject);

            vals?.Dispose(); vals = null;
        }

        private HDrawingObjectCallback mSelectedCallback;

        internal override void Rebuild()
        {
            // 反序列化完后，用参数重建运行期对象
            Config(CenterPoint.X, CenterPoint.Y,Radius, StartAngle / 180.0 * Math.PI, EndAngle / 180.0 * Math.PI);
        }
    }
}
