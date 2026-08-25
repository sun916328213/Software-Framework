using Extension.Core.ControlEx;
using System;

namespace WPFCustomControls.Controls
{
    public class CustListViewService
    {
        public ListViewModel Model { get; set; } = new ListViewModel();

        public void Add(string data)
        {
            try
            {
                Action action = () => { Model.DataCollection.Add(data); };
                action.UIBeginInvoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ListViewService.Add 异常: {ex.Message}");
            }
        }

        public void Clear()
        {
            try
            {
                Action action = () => { Model.DataCollection.Clear(); };
                action.UIBeginInvoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ListViewService.Clear 异常: {ex.Message}");
            }
        }
    }
}
