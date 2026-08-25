using DPLibrary_Base_F48.Vision;
using DPLibrary_Halcon_F48.Base;
using DPLibrary_Halcon_F48.Base.DispMessage;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48
{
    public interface I_DispMessage : IDisposable, INotifyPropertyChanged
    {
        DispAnchor Anchor { get; set; }
        bool IsOnImage { get; set; }
        Point UL { get; set; }
        int FontSize { get; set; }
        string Message { get; set; }
        bool IsAutoLineBreak { get; set; }
        E_VisionColor Color { get; set; }
        bool IsBackGround { get; set; }
        void Draw(HWindow win);
    }
}
