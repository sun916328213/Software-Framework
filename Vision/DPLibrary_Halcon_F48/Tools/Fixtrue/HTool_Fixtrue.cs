using Vision.Core;
using Vision.Core.Tools;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Tools.TemplateMatch;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DPLibrary_Halcon_F48.Tools
{
    public class HTool_Fixtrue : HVisionTool,IMatrixSource
    {
        public HTool_Fixtrue() : base()
        {
            Inputs = new ToolInputPorts(new List<IToolInputPort>()
            {
                new InputPort<Point>(ID.ToString(), "TransforPoint")
                {
                    BindingData = new PortBindingData("6f7ff1e8-6a3c-4572-9612-e652fd48d15b","HTemplateResult",".Center")
                }
            });
        }

        Point mBasePoint = new Point(0,0);

        Point mTransforPoint = new Point(0, 0);
        double mTransforR = 0;
        double mTransforScale = 1;

        public override E_ToolType ToolType { get { return E_ToolType.HTool_Fixtrue; } }   

        [ToolPropertyAttribute(Group = "2.对标参数", DisplayName = "基础中心点", Description = "做为转换前的基础中心点",
                EditorType = E_PropertyEditorType.Text)]
        public Point BasePoint
        {
            get { return mBasePoint; }
            set
            {
                if (mBasePoint != value)
                {
                    mBasePoint = value;
                    OnPropertyChanged(nameof(BasePoint));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.对标参数", DisplayName = "转换中心点", Description = "做为转换的基础中心点",
        EditorType = E_PropertyEditorType.Text)]
        public Point TransforPoint
        {
            get { return mTransforPoint; }
            set
            {
                if (mTransforPoint != value)
                {
                    mTransforPoint = value;
                    OnPropertyChanged(nameof(TransforPoint));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.对标参数", DisplayName = "转换旋转", Description = "做为转换的基础旋转角度,角度制",
        EditorType = E_PropertyEditorType.Double)]
        public double TransforR
        {
            get { return mTransforR; }
            set
            {
                if (mTransforR != value)
                {
                    mTransforR = value;
                    OnPropertyChanged(nameof(TransforR));
                }
            }
        }

        [ToolPropertyAttribute(Group = "2.对标参数", DisplayName = "转换缩放", Description = "做为转换的基础缩放",
        EditorType = E_PropertyEditorType.Double)]
        public double TransforScale
        {
            get { return mTransforScale; }
            set
            {
                if (mTransforScale != value)
                {
                    mTransforScale = value;
                    OnPropertyChanged(nameof(TransforScale));
                }
            }
        }

        // ⚠️ setter 是 protected，System.Text.Json 默认**不给非公有 setter 赋值且静默跳过**。
        // Tool.Save()（ITool.cs:242）会整实例 JsonHelper.SerializeToFile(this, …) 存盘、
        // ToolMge.cs:275 读回 ⇒ 不加这一行的话，存盘能写出真实标定矩阵、读回却回退成单位矩阵
        // （下游 HTool_Blob.cs:170 / HTool_FindLine.cs:223 的 GetMatrix() 会拿到错值）。
        // 用 [JsonInclude] 而不是把 setter 改 public：外部仍不可写，只有 STJ 能用它。
        [JsonInclude]
        public string Matrix { get; protected set; } = "1, 0, 0, 0, 1, 0, 0, 0, 1";

        public override void Config()
        {
            throw new NotImplementedException();
        }

        public override void Pause()
        {
            throw new NotImplementedException();
        }

        public override void Reset()
        {
            throw new NotImplementedException();
        }

        public override void Run()
        {
            Results.Clear();
            HTuple homMat2D,homRt,homT;

            //坐标系转换构建
            HOperatorSet.HomMat2dIdentity(out homMat2D);
            //先缩放
            HOperatorSet.HomMat2dScale(homMat2D, mTransforScale, mTransforScale ,0, 0, out homMat2D);
            //旋转平移
            HOperatorSet.VectorAngleToRigid(mBasePoint.Y, mBasePoint.X, 0,
                                            mTransforPoint.Y, mTransforPoint.X,(mTransforR / 180.0) * Math.PI, out homRt);
            HOperatorSet.HomMat2dCompose(homRt, homMat2D, out homT);

            Matrix = homT.ToMixtrueStr();

            Results.Add(new HMixtrueResult()
            {
                Mixtrue = homT
            });
        }

        public string GetMatrix()
        {
            return Matrix;
        }
    }
}
