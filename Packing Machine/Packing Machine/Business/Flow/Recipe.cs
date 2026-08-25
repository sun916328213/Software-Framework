using Communication;
using Extension.Core.ConvertEx;
using Extension.Core.SystemEx;
using Extension.Struct;
using Packing_Machine.Data;
using Packing_Machine.Business.Station;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Services;

namespace Packing_Machine.Business.Flow
{
    public class RecipeModel
    { 
        public string Name { get; set; }

        public  string NameRemark { get; set; }

        public string 晶圆盒扫码读取次数 {  get; set; }

        public string 晶圆盒RFID读取次数 {  get; set; }

        public string 检查相机拍照次数 {  get; set; }

        public string 进料贴标签个数 {  get; set; }

        public string 折袋贴标签个数 {  get; set; }

        public string SecsModel {  get; set; }

        public string 颜色传感器 {  get; set; }

        public string 湿度卡种类 {  get; set; }

        public string 左侧PE袋折袋比例阀开度设定值 {  get; set; }

        public string 右侧PE袋折袋比例阀开度设定值 { get; set; }

        public string TOSS_PE真空抽取时间设定值 { get; set; }

        public string TOSS_PE真空抽取流量设定值 { get; set; }

        public string TOSS_PE袋热封温度设定值 { get; set; }

        public string TOSS_PE袋热封加热时间设定值 { get; set; }

        public string TOSS_PE袋热封后保压时间设定值 { get; set; }

        public string PE抽真空压力设定值 { get; set; }

        public string PE抽真空流量设定值 { get; set; }
        
        public string PE袋折刀闭合延时 { get; set; }

        public string PE左折袋开始位比例阀压力设定值 { get; set; }
        public string PE左折袋闭合位比例阀压力设定值 { get; set; }
        public string ROPEX_PE袋热封温度设定值 { get; set; }



        public string ROPEX_PE袋热封加热时间设定值 { get; set; }
        public string ROPEX_PE袋热封后保压时间设定值 { get; set; }
        public string 上加热气缸比例阀设定值 { get; set; }
        public string 上密封气缸比例阀设定值 { get; set; }
        public string 左侧AL袋折袋比例阀开度设定值 { get; set; }
        public string 右侧AL袋折袋比例阀开度设定值 { get; set; }
        public string TOSS_AL真空抽取时间设定值 { get; set; }
        public string TOSS_AL真空抽取流量设定值 { get; set; }
        public string TOSS_AL袋热封温度设定值 { get; set; }
        public string TOSS_AL袋热封加热时间设定值 { get; set; }
        public string TOSS_AL袋热封后保压时间设定值 { get; set; }
        public string AL抽真空压力设定值kpa { get; set; }
        public string AL抽真空流量设定值kpa { get; set; }
        public string AL袋折刀闭合延时 { get; set; }
        public string 右折袋开始位比例阀压力设定值 { get; set; }
        public string 右折袋闭合位比例阀压力设定值 { get; set; }
        public string ROPEX_AL袋热封温度设定值 { get; set; }
        public string ROPEX_AL袋热封加热时间设定值 { get; set; }


        public string ROPEX_AL袋热封后保压时间设定值 { get; set; }
        public string 拆卸功能配置 { get; set; }
        public string 进料存档 { get; set; }
        public string 进料读码 { get; set; }
        public string 进料贴标签1 { get; set; }
        public string 进料检标签1 { get; set; }

        public string 进料放干燥剂1 { get; set; }
        public string 进料放干燥剂2 { get; set; }
        public string 进料贴标签2 { get; set; }
        public string 进料检标签2 { get; set; }
        public string 进料放干燥剂3 { get; set; }
        public string 进料放干燥剂4 { get; set; }
        public string 进料贴标签3 { get; set; }


        public string 进料检标签3 { get; set; }
        public string 折袋贴标签1 { get; set; }
        public string 折袋贴标签2 { get; set; }
        public string 出站存档 { get; set; }
    }

    public class Recipe : StationBase<int>
    {
         //CommunicationManager _CommunicationManager;

        internal string StationName = "Recipe";

        public Recipe(IContainerProvider container):base(container)
        {
            //_CommunicationManager= container.Resolve<CommunicationManager>();
        }

        string RecipeID ;
        /// <summary>
        /// 配方切换
        /// </summary>
        public async void RecipeSwitch(string id)
        {
            try
            {
                _SecsMethod.Update(VIDOrder.FOSBID, id);  //更新VID数据
                _LogInfoService.AddRunInfo( Message.配方切换请求.GetMessage());
                await _SecsMethod.SendS6F11(EventOrder.Recipe_Request);
                //等待S2F41指令
                  RecipeID = await _SecsMethod.GetS2F41_Recipe(id, S2F42Order.RecipeInfo);
                if (RecipeID == null)
                {
                    _LogInfoService.AddRunInfo(StationName + Message.配方切换失败.GetMessage(), ELogLevel.Error);
                    await _SecsMethod.SendS6F11(EventOrder.Recipe_Select_Fail);
                }
                else
                {
                    //告知PLC进行配方切换
                    var d1 = await _PLCMethod.TriggerAsync(StationName, PLCProcess.RecipeProcess_Trigger, RecipeID);
                    if (d1 != EPLCResult.OK)
                    {
                        _LogInfoService.AddRunInfo(StationName + Message.配方切换失败.GetMessage(), ELogLevel.Error);
                        await _SecsMethod.SendS6F11(EventOrder.Recipe_Select_Fail);
                    }
                }
            }

            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + ":" + message, ELogLevel.SysError);
            }
        }

        /// <summary>
        /// 配方信息修改上报
        /// </summary>
        public async Task RecipeReport()
        {
            var data = ReadRecipe();
            Item[] items = new Item[1] { Item.A(data.ToJson()) };


            Item SecsItem = Item.L(
                    Item.U2(1),//DataID
                    Item.U2(1),//EventID
                    Item.L(items)
                   );
            await _SecsMethod.SendS6F11(SecsItem);
        }

        /// <summary>
        /// 配方信息确认上报
        /// </summary>
        public async Task RecipeConfirm(string fosbid, string pLCOrder)
        {
            EPLCResult pLCResult = EPLCResult.NG;
            try
            {
                _PLCMethod.TriggerReply(StationName, pLCOrder);
                var data = ReadRecipe();
                Item[] items = new Item[1] { Item.A(data.ToJson()) };

                Item SecsItem = Item.L(
                        Item.U2(1),//DataID
                        Item.U2(1),//EventID
                        Item.L(items)
                       );
                await _SecsMethod.SendS6F11(SecsItem);


                //等待S2F41指令 确认配方信息
                var d3 = await _SecsMethod.GetS2F41_Recipe(fosbid, S2F42Order.RecipeInfo);

                if (d3 == "1")
                {
                    _SecsMethod.Update(VIDOrder.RecipeID, RecipeID);  //更新VID数据
                    _SecsMethod.Update(VIDOrder.RecipeName, RecipeID); //更新VID中配方的名称
                    _SecsMethod.Update(VIDOrder.FOSBType, ""); //更新VID中FOSB类型
                    await _SecsMethod.SendS6F11(EventOrder.Recipe_Select_Success);
                    _LogInfoService.AddRunInfo(StationName + Message.配方切换成功.GetMessage());
                    pLCResult = EPLCResult.OK;
                }
                else
                {
                    _SecsMethod.Update(VIDOrder.RecipeID, "");  //更新VID数据
                    _SecsMethod.Update(VIDOrder.RecipeName, ""); //更新VID中配方的名称
                    _SecsMethod.Update(VIDOrder.FOSBType, ""); //更新VID中FOSB类型
                    await _SecsMethod.SendS6F11(EventOrder.Recipe_Select_Success);
                    _LogInfoService.AddRunInfo(StationName + Message.配方切换失败.GetMessage(), ELogLevel.Error);
                    pLCResult = EPLCResult.NG;
                }

                //流程开始
                await _SecsMethod.SendS6F11(EventOrder.Process_Start);
            }
            catch (Exception ex)
            {
                pLCResult = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + ":" + message, ELogLevel.SysError);
            }
            finally
            {
                //通知PLC结果
                _PLCMethod.SendResult(StationName, pLCOrder, EPLCResult.OK);
            }
        }

        /// <summary>
        /// 读取recipe数据
        /// </summary>
        /// <returns></returns>
        private RecipeModel ReadRecipe()
        {
            //var data = _CommunicationManager.ReadContinuous("D20000", 200);

            //RecipeModel recipeModel = new RecipeModel();
            //#region
            //var D1 = data[0].ToString();
            //var D2 = data[1].ToString();
            //var D3 = data[2].ToString();
            //var D4 = data[3].ToString();


            //recipeModel.Name = data.Skip(0).Take(10).ToArray().ToASC();

            //recipeModel.NameRemark = data.Skip(10).Take(10).ToArray().ToASC();

            //recipeModel.晶圆盒扫码读取次数 = data[20].ToString();

            //recipeModel.晶圆盒RFID读取次数 = data[21].ToString();

            //recipeModel.检查相机拍照次数 = data[22].ToString();

            //recipeModel.进料贴标签个数 = data[23].ToString();

            //recipeModel.折袋贴标签个数 = data[24].ToString();

            //recipeModel.SecsModel = data[25].ToString();

            //recipeModel.颜色传感器 = data[26].ToString();

            //recipeModel.湿度卡种类 = data[27].ToString();

            //recipeModel.左侧PE袋折袋比例阀开度设定值 = data[30].ToString();

            //recipeModel.右侧PE袋折袋比例阀开度设定值 = data[32].ToString();

            //recipeModel.TOSS_PE真空抽取时间设定值 = data[34].ToString();

            //recipeModel.TOSS_PE真空抽取流量设定值 = data[36].ToString();

            //recipeModel.TOSS_PE袋热封温度设定值 = data[38].ToString();

            //recipeModel.TOSS_PE袋热封加热时间设定值 = data[40].ToString();

            //recipeModel.TOSS_PE袋热封后保压时间设定值 = data[42].ToString();

            //recipeModel.PE抽真空压力设定值 = data[44].ToString();

            //recipeModel.PE抽真空流量设定值 = data[46].ToString();

            //recipeModel.PE袋折刀闭合延时 = data[48].ToString();

            //recipeModel.PE左折袋开始位比例阀压力设定值 = data[50].ToString();
            //recipeModel.PE左折袋闭合位比例阀压力设定值 = data[52].ToString();
            //recipeModel.ROPEX_PE袋热封温度设定值 = data[54].ToString();


            //recipeModel.ROPEX_PE袋热封加热时间设定值 = data[56].ToString();
            //recipeModel.ROPEX_PE袋热封后保压时间设定值 = data[58].ToString();
            //recipeModel.上加热气缸比例阀设定值 = data[60].ToString();
            //recipeModel.上密封气缸比例阀设定值 = data[62].ToString();
            //recipeModel.左侧AL袋折袋比例阀开度设定值 = data[70].ToString();
            //recipeModel.右侧AL袋折袋比例阀开度设定值 = data[72].ToString();
            //recipeModel.TOSS_AL真空抽取时间设定值 = data[74].ToString();
            //recipeModel.TOSS_AL真空抽取流量设定值 = data[76].ToString();
            //recipeModel.TOSS_AL袋热封温度设定值 = data[78].ToString();
            //recipeModel.TOSS_AL袋热封加热时间设定值 = data[80].ToString();
            //recipeModel.TOSS_AL袋热封后保压时间设定值 = data[82].ToString();
            //recipeModel.AL抽真空压力设定值kpa = data[84].ToString();
            //recipeModel.AL抽真空流量设定值kpa = data[86].ToString();
            //recipeModel.AL袋折刀闭合延时 = data[88].ToString();
            //recipeModel.右折袋开始位比例阀压力设定值 = data[90].ToString();
            //recipeModel.右折袋闭合位比例阀压力设定值 = data[92].ToString();
            //recipeModel.ROPEX_AL袋热封温度设定值 = data[94].ToString();
            //recipeModel.ROPEX_AL袋热封加热时间设定值 = data[96].ToString();


            //recipeModel.ROPEX_AL袋热封后保压时间设定值 = data[98].ToString();
            //recipeModel.拆卸功能配置 = data[100].ToString();
            //recipeModel.进料存档 = data[101].ToString();
            //recipeModel.进料读码 = data[102].ToString();
            //recipeModel.进料贴标签1 = data[103].ToString();
            //recipeModel.进料检标签1 = data[104].ToString();

            //recipeModel.进料放干燥剂1 = data[105].ToString();
            //recipeModel.进料放干燥剂2 = data[106].ToString();
            //recipeModel.进料贴标签2 = data[107].ToString();
            //recipeModel.进料检标签2 = data[108].ToString();
            //recipeModel.进料放干燥剂3 = data[109].ToString();
            //recipeModel.进料放干燥剂4 = data[110].ToString();
            //recipeModel.进料贴标签3 = data[111].ToString();


            //recipeModel.进料检标签3 = data[112].ToString();
            //recipeModel.折袋贴标签1 = data[113].ToString();
            //recipeModel.折袋贴标签2 = data[114].ToString();
            //recipeModel.出站存档 = data[115].ToString();
            //#endregion
            //return recipeModel;
            return null;
        }
    }
}
