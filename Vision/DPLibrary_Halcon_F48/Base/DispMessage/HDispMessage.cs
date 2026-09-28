using Vision.Core.Vision;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Base.DispMessage
{
    public class HDispMessage : I_DispMessage
    {

        public DispAnchor Anchor { get; set; }
        public bool IsOnImage { get; set; } = true; // 是否在图像上显示
        public Point UL { get; set; } = new Point(0, 0);
        public int FontSize { get ; set ; } = 15;
        public string Message { get; set ; } = "";
        public bool IsAutoLineBreak { get; set ; } = false; // 是否自动换行
        public E_VisionColor Color { get ; set ; } = E_VisionColor.Red;
        public bool IsBackGround { get; set; } = true; // 是否有背景

        #pragma warning disable CS0067 // 事件由 I_DispMessage:INotifyPropertyChanged 接口契约要求，但本类未触发通知
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        public void Dispose()
        {
            
        }

        public void Draw(HWindow win)
        {
            string s = win.QueryFont().S;

            win.SetFont(s + $"-{FontSize}");

            //HOperatorSet.SetColor(win, Color.ToString().ToLower());
            HTuple lab, val;
            if (IsBackGround)
            {
                lab = new HTuple(new[] { "box", "shadow" });
                val = new HTuple(new[] { "true", "true" });
            }
            else
            {
                lab = new HTuple(new[] { "box", "shadow" });
                val = new HTuple(new[] { "false", "false" });
            }

            double row = 0, col = 0;

            switch (Anchor)
            {
                case DispAnchor.Center:
                    row = UL.Y - FontSize / 2;
                    col = UL.X - FontSize * Message.Length / 2; ;
                    break;
                case DispAnchor.Left:
                    row = UL.Y - FontSize / 2;
                    col = UL.X - FontSize * Message.Length;
                    break;
                case DispAnchor.Right:
                    break;
                case DispAnchor.Top:
                    break;
                case DispAnchor.Bottom:
                    break;
                case DispAnchor.TopLeft:
                    break;
                case DispAnchor.TopRight:
                    break;
                case DispAnchor.BottomLeft:
                    break;
                case DispAnchor.BottomRight:
                    break;
                default:
                    break;
            }

            win.DispText(Message,IsOnImage? "image" : "window", row, col, Color.ToString().ToLower(),lab,val);
            
        }
    }

    public enum DispAnchor
    {
        Center,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
