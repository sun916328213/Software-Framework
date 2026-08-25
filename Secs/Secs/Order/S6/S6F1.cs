using Extension.Core.ConvertEx;
using Prism.Ioc;
using Secs.Manager;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace Secs.Order.S6
{
    public struct SS6F1 
    {
        /// <summary>
        /// TRID
        /// </summary>
        public string TRID { get; set; }
        /// <summary>
        /// Number
        /// </summary>
        public string SMPLN { get; set; }
        /// <summary>
        /// 时间 yyyymmyyddhhmmsscc
        /// </summary>
        public string STIME { get; set; }
        /// <summary>
        /// VID ID集合
        /// </summary>
        public List<string> VID { get; set; }
    }

    [SecsOrder("S6F1")]
    class   S6F1:SecsOrderBase
    {
       
        public override Item GetSendItem(params object[] pData )
        {
            try
            {
                if (pData == null) return null;


                SS6F1 s2F23 = (SS6F1)pData[0];
                VIDManager vIDManager = (VIDManager)pData[1];

                List<Item> vid_vlaue = new List<Item>();

                foreach (var item in s2F23.VID)
                {
                    var vid = vIDManager.GetVIDByID(item);
                    Item viditem = AddData(vid.Value, vid.DataType);
                    vid_vlaue.Add(viditem);
                }

                Item TRID = AddData(s2F23.TRID, SecsFormat.U2);
                Item SMPLN = AddData(s2F23.SMPLN, SecsFormat.U2);
                Item time = AddData(DateTime.Now.ToYYYYMMDDHHMMSSFFF(), SecsFormat.ASCII);
                Item SecsItem = Item.L(TRID, SMPLN, time, Item.L(vid_vlaue.ToArray()));

                return SecsItem;
            }
            catch (Exception ex)
            {
                string message = ex.Message + ex.StackTrace;
                return null;  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
