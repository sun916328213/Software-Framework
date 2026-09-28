using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vision.Core;
using DPLibrary_Halcon_F48;
using DPLibrary_Halcon_F48.Tools;
using DPLibrary_Halcon_F48.Tools.AI;
using DPLibrary_Halcon_F48.Tools.Blob;
using DPLibrary_Halcon_F48.Tools.FindLine;
using DPLibrary_Halcon_F48.Tools.Geometry;
using DPLibrary_Halcon_F48.Tools.TemplateMatch;
using HalconDotNet;
using System.Windows.Input;


namespace Vision.Workbench
{
    public class ViewModelT : ObservableObject
    {

        public HTool_BarCode BarcodeReader { get; set; } = new HTool_BarCode();
        public HTool_BarCode_AutoRegionZoomFind BarcodeReader2 { get; set; } = new HTool_BarCode_AutoRegionZoomFind();
        public HTool_2DCode BarcodeReader3 { get; set; } = new HTool_2DCode();

        public HTool_TemplateMatch template { get; set; } = new HTool_TemplateMatch();
        public HTool_TemplateMatch template2 { get; set; } = new HTool_TemplateMatch();

        public HTool_NPoints nPoints { get; set; } = new HTool_NPoints();
        public HTool_TransforPoint tp { get; set; } = new HTool_TransforPoint();
        public HTool_FindGroup findLineCanny { get; set; } = new HTool_FindGroup();
        public HTool_Blob Bb { get; set; }=new HTool_Blob();
        public HTool_Geometry GS { get; set; } = new HTool_Geometry();

        public HTool_Fixtrue fixtrue;

        public AITool aITool { get; set; }= new AITool();

        public ViewModelT()
        {
            //ToolMge.Instance.Config();

            ToolMge.Instance.Add(BarcodeReader);
            ToolMge.Instance.Add(BarcodeReader2);
            ToolMge.Instance.Add(BarcodeReader3);
            ToolMge.Instance.Add(template);
            ToolMge.Instance.Add(nPoints);
            ToolMge.Instance.Add(findLineCanny);
            ToolMge.Instance.Add(Bb);
            ToolMge.Instance.Add(GS);

            template2 = new HTool_TemplateMatch();
            fixtrue = new HTool_Fixtrue();
            ToolMge.Instance.Add(template2);
            ToolMge.Instance.Add(fixtrue);

            //nPoints.CalibPoints_XY.Add(new CalibPoint() { Cx = 0, Cy = 0, Rx = 2, Ry = 2 });
            //nPoints.CalibPoints_XY.Add(new CalibPoint() { Cx = 1, Cy = 1, Rx = 3, Ry = 3 });
            //nPoints.CalibPoints_XY.Add(new CalibPoint() { Cx = 2, Cy = 0, Rx = 4, Ry = 2 });


            //nPoints.CalibPoints_R.Add(new CalibPoint() { Cx = 1, Cy = 2 });
            //nPoints.CalibPoints_R.Add(new CalibPoint() { Cx = 1, Cy = 0 });
            //nPoints.CalibPoints_R.Add(new CalibPoint() { Cx = 2, Cy = 1 });

            ToolMge.Instance.Add(tp);
            ToolMge.Instance.Add(aITool);


            AddImageCommand = new RelayCommand(AddImage);
            AddMessageCommand = new RelayCommand(AddMessage);
            RemoveMessageCommand = new RelayCommand(RemoveMessage);
            ClearMessageCommand = new RelayCommand(ClearMessage);
            //AddMinCommand = new RelayCommand(AddMin);
            ReadCodeCommand = new RelayCommand(ReadCode);
            //MoveRCommand = new RelayCommand(MoveR);
            IsRoiCommand = new RelayCommand(AddROI);
            SaveCommand = new RelayCommand(SaveReader);
            //JsonHelper.SetJsonSetting(new System.Type[] { typeof(I_HROI) }, new string[] { "DPLibrary_Halcon_F48" });

            //BarcodeReader = (HTool_BarCode)ToolMge.Instance[0];
            //BarcodeReader2 = (HTool_BarCode_AutoRegionZoomFind)ToolMge.Instance[1];
            //BarcodeReader3 = (HTool_2DCode)ToolMge.Instance[2];
            //template = (HTool_TemplateMatch)ToolMge.Instance[3];
            //nPoints = (HTool_NPoints)ToolMge.Instance[4];
            //tp = (HTool_TransforPoint)ToolMge.Instance[5];
            //findLineCanny = (HTool_FindGroup)ToolMge.Instance[7];

            //fixtrue = (HTool_Fixtrue)ToolMge.Instance[11];

            AddNCommand = new RelayCommand(AddNodes);
            testCommand = new RelayCommand(test);
            //ToolMge.Instance.SaveManager();
            //ToolMge.Instance.Save();
        }


        //public int? MinContrast2
        //{
        //    get { return BarcodeReader.MinContrast; }
        //    set
        //    {
        //        if (BarcodeReader.MinContrast != value)
        //        {
        //            BarcodeReader.MinContrast = value;
        //            OnPropertyChanged(nameof(MinContrast2)); 
        //        }
        //    }
        //}
        public ICommand testCommand { get; }

        void test()
        {


            template.Run();


            fixtrue.BasePoint = template.mTpModel.BasePoint;

            //fixtrue.TransforPoint = (Point)((InputPort<Point>)fixtrue.Inputs["TransforPoint"]).Get(ToolMge.Instance.ToolBus);

            fixtrue.TransforPoint = ((HTemplateResult)template.Results[0]).Center;
            fixtrue.TransforR = ((HTemplateResult)template.Results[0]).R;
            fixtrue.TransforScale = ((HTemplateResult)template.Results[0]).Scale;
            fixtrue.Run();
            //fixtrue.Save();
            Bb.Image = template.Image.Clone();
            Bb.Run();
            //findLineCanny.Image = template.Image.Clone();
            //findLineCanny.Run();
        }

        public ICommand AddMinCommand { get; }

        //void AddMin()
        //{
        //    MinContrast2 += 1;
        //}
        public ICommand MoveRCommand { get; }
        public ICommand ReadCodeCommand { get; }

        public ICommand AddImageCommand { get; }
        public ICommand AddMessageCommand { get; }
        public ICommand RemoveMessageCommand { get; }
        public ICommand ClearMessageCommand { get; }
        public ICommand IsRoiCommand { get; }

        bool mIsRoi = false;
        public bool IsRoi 
        {
            get { return mIsRoi; }
            set
            {
                SetProperty(ref mIsRoi, value);
            }
        } 
        void AddROI() 
        {
            IsRoi = !mIsRoi;
        }

        public ICommand SaveCommand { get; }

        bool mSave = false;
        public bool Save
        {
            get { return mSave; }
            set
            {
                SetProperty(ref mSave, value);
            }
        }
        void SaveReader()
        {
            ////JsonHelper.SetJsonSetting(new System.Type[] { typeof(I_ROI) }, new string[] { "DPLibrary_Halcon_F48" });

            ////BarcodeReader = JsonHelper.DeserializeFromFile<HBarcodeReader>( $"C:\\Users\\ZDH-XMXT-257\\Desktop\\", "111");
            //Debug.WriteLine(BarcodeReader.SearchRegions.Count); // 应该 > 0
            //JsonHelper.SerializeToFile(BarcodeReader, $"C:\\Users\\ZDH-XMXT-257\\Desktop\\", "111");


            //JsonHelper.SerializeToFile(BarcodeReader2, $"C:\\Users\\ZDH-XMXT-257\\Desktop\\", "222");

            ToolMge.Instance.Save();
        }


        HObject himage;

        public HObject Himage
        {
            get { return himage; }
            set
            {
                SetProperty(ref himage, value);
            }
        }

        BulkObservableCollection<HROI> mListRegion = new BulkObservableCollection<HROI>();

        public BulkObservableCollection<HROI> ListRegion
        {
            get { return mListRegion; }
            set
            {
                SetProperty(ref mListRegion, value);
            }
        }

        BulkObservableCollection<I_HXLD> mListXLD = new BulkObservableCollection<I_HXLD>();

        public BulkObservableCollection<I_HXLD> ListXLD
        {
            get { return mListXLD; }
            set
            {
                SetProperty(ref mListXLD, value);
            }
        }

        BulkObservableCollection<I_DispMessage> mListMessages = new BulkObservableCollection<I_DispMessage>();

        public BulkObservableCollection<I_DispMessage> ListMessages
        {
            get { return mListMessages; }
            set
            {
                SetProperty(ref mListMessages, value);
            }
        }

        int x = 0;

        void AddMessage() 
        {
            //ListMessages.IsSuspend = true;
            //for (int i = 0; i < 1000; i++)
            //{
            //    ListMessages.Add(new HDispMessageData()
            //    {
            //        X = 0,
            //        Y = 0,
            //        Message = $"测试消息{x}HHH",
            //        FontSize = 30,
            //        Color = EDispColor.Red
            //    });
            //    x += 10;
            //}
            //ListMessages.IsSuspend = false;


            //ListMessages.Add(new HDispMessage()
            //{
            //    X = 0,
            //    Y = 0,
            //    Message = $"测试消息{x}HHH",
            //    FontSize = 30,
            //    Color = EHColor.Red
            //});
            //x += 10;

            //BarcodeReader.SearchRegions.Add(new HROI_Rectangle(300, 300, 800, 1200)
            //{
            //    CanChange = true
            //});

            //HObject region;
            //HOperatorSet.GenCircleContourXld(out region,100,100, 30, 0, 6.28, "positive", 1.0);
            //ListXLD.Add(new HDispXLDData()
            //{
            //    XLD = region,
            //    LineSize = 5
            //});
            //HObject region2;
            //HOperatorSet.GenCircleContourXld(out region2, 100, 100, 60, 0, 6.28, "positive", 1.0);
            //ListXLD.Add(new HDispXLDData()
            //{
            //    XLD = region2,
            //    LineSize = 1
            //});

        }

        
        void RemoveMessage()
        {
            //ListMessages.RemoveAt(0);
            ListRegion.RemoveAt(0);
        }
        void ClearMessage()
        {
            //ListMessages.Clear();
            ListRegion.Clear();
        }

        void AddImage()
        {
            HObject himage2;
            //HOperatorSet.ReadImage(out himage2, @"C:\Users\ZDH-XMXT-257\Pictures\2.jpg");
            HOperatorSet.ReadImage(out himage2, @"C:\Users\ZDH-XMXT-257\Desktop\奕斯伟\2.bmp");



            //Himage = himage2;
            
           // BarcodeReader.Image = himage2;
            //BarcodeReader.SearchRegions.Add(new HROI_Rectangle(300, 300, 800, 1200)
            //{
            //    DispColor = EHColor.Red
            //});
        }


        void ReadCode()
        {
            //foreach (var item in BarcodeReader.FindCodes())
            //{
            //    ListRegion.Add(new HROI_Null(item.Region));
            //} ;
        }


        #region CanvasTest

        //EditorCanvas _canvas = new EditorCanvas();

        //public EditorCanvas canvas
        //{
        //    get { return _canvas; }
        //    set
        //    {
        //        SetProperty(ref _canvas, value);
        //    }
        //}

        public ICommand AddNCommand { get; }

        void AddNodes()
        {
            //for (int i = 0; i < 2; i++)
            //{
            //    canvas.Nodes.Add(new EditorNode_Start(new System.Drawing.Point(0, 150 * i)));
            ////}
            //canvas.Nodes.Add(new EditorNode_Start(new System.Drawing.Point(0, 150 * 1))
            //{
            //    IsSelected = true
            //});

            //canvas.Nodes.Add(new EditorNode_End(new System.Drawing.Point(0, 150 * 0))
            //{
            //    NodeStates = E_NodeStates.Error
            //});

            //canvas.Nodes.Add(new EditorNode_MethodDo(new System.Drawing.Point(0, 150 * 2))
            //{
            //    NodeStates = E_NodeStates.Paused
            //});

            //IEditorNode id = new EditorNode_MethodDo(new System.Drawing.Point(0, 150 * 3))
            //{
            //    NodeStates = E_NodeStates.Paused
            //};

            //id.InputSocket.SocketOnLine = E_SocketLine.Top;
            //id.OutputSocket.SocketOnLine = E_SocketLine.Right;

            //canvas.Nodes.Add(id);
        }



        #endregion


    }
}
