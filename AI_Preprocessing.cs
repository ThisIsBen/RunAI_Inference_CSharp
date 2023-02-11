using HalconDotNet;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;


namespace アプリ
{
    class AI_Preprocessing
    {


        //目的：AIモデルの入力画像を作成する（AIモデルの前処理）。


        // declare HALCON program and procedures that we want to call
        private HDevProgram HalconProgram;
        private HDevProcedure InitProc;
        private HDevProcedure ProcessingProc;
       
        // instance of the engine
        private HDevEngine HalconEngine = new HDevEngine();
        // procedure calls
        private HDevProcedureCall InitProcCall;
        private HDevProcedureCall ProcessingProcCall;


        //エラー対策のため、エラー発生の原因とエラーメッセージを取得する。
        //エラーが発生していない場合、両方とも""である。
        private string HALCON_ERR = "";
        private string ErrorMsg = "";

        //HALCON_ERRの読み取り専用プロパティ
        public string Defined_HALCON_ERR
        {
            get
            {
                return HALCON_ERR;
            }
            
        }



        //HALCON HDevEngineの初期化＋HALCONプログラム内の初期化Procedureを実行する。
        public AI_Preprocessing()
        {
            try
            {
                // enable execution of JIT compiled procedures
                HalconEngine.SetEngineAttribute("execute_procedures_jit_compiled", "true");

                // load the HALCON program
                HalconProgram = new HDevProgram(GlobalConstants.AI_Preprocessing_HalconHDevPath);
                // specify which HALCON procedures to call and initialize them.
                InitProc = new HDevProcedure(HalconProgram, "InitProc");
                ProcessingProc = new HDevProcedure(HalconProgram, "ProcessingProc");
                

                // enable execution of JIT compiled procedures
                InitProc.CompileUsedProcedures();
                ProcessingProc.CompileUsedProcedures();
                

                //create HDevProcedureCall objects
                InitProcCall = new HDevProcedureCall(InitProc);
                ProcessingProcCall = new HDevProcedureCall(ProcessingProc);


                
                //Halconプログラムの初期化を実行する
                InitProcCall.Execute();

                



            }
            //HDevEngine を使って、画像処理プログラムをロードする時、エラーが発生したら、
            //MessageBoxでオペレーターに知らせ、再起動してもらう。
            //再起動してもこのエラーが続ける場合、PI炉出口監視システムを停止して管理者に連絡してもらう
            catch (Exception e)
            {

                //display this error message to inform the user
                string errorMessage = "画像処理プログラムをロードする時、エラーが発生した。\n\n停止ボタンを押して、\nPI炉出口監視システムを再開してください。\n\n再起動してもこのエラーが続ける場合、\nPI炉出口監視システムを停止して管理者に連絡してください。" + "\n\nエラーメッセージ：\n" + e.Message;

                // MessageBoxでオペレーターに知らせ、再起動してもらう。
                //show the error message box to inform the user if the same message box is not being shown 
                //ReportErrorMsg.showMsgBoxFromWS_IfMsgBoxIsNotShown(errorMessage, "PI炉出口監視システム " + "KanekaApp" + GlobalConstants.cameraNo + "_画像処理プログラムロードエラー");

                Console.WriteLine("画像処理プログラムをロードする時、エラーが発生した。\n\n" + "エラーメッセージ：\n" + e.Message);

                //Give up processing this picture and proceed to process the next one
                return;

            }

        }




        //対象画像をHALCONプログラム内の画像処理Procedureに入力し、
        //AIモデルの入力画像を作成して返す。
        //エラーが発生した場合、AIモデルの入力画像を作成できないため、nullを返す。
        public Bitmap preprocessImgForAI(string imageToBeChecked)
        {

            try
            {

                //[Halconプログラムの画像処理本体を実行する]
                //パラメーターを設定する。
                ProcessingProcCall.SetInputCtrlParamTuple("imageToBeChecked", imageToBeChecked);

                //AIモデルの入力画像の生成処理を実施する。
                ProcessingProcCall.Execute();


                //エラー対策のため、エラー発生の原因とエラーメッセージを取得する。
                //エラーが発生していない場合、両方とも""である。
                HALCON_ERR = ProcessingProcCall.GetOutputCtrlParamTuple("HALCON_ERR")[0];
                ErrorMsg = ProcessingProcCall.GetOutputCtrlParamTuple("ErrorMsg")[0];

                //エラーが発生した場合、AIモデルの入力画像が生成できないため、
                //nullをreturnして、この画像のAI画像処理を止める。
                if (HALCON_ERR != "")
                {
                    //エラーメッセージがあれば、メッセージを表示する。
                    if (ErrorMsg != "")
                    {
                        //Pop-upメッセーでエラーメッセージを表示する
                        //string title = "AI前処理HALCONエラー";
                        //ReportErrorMsg.showMsgBox_IfNotShown(ErrorMsg, title);

                        //console window にエラーメッセージを表示する
                        Console.WriteLine("AI前処理HALCONエラー");
                        Console.WriteLine(ErrorMsg);
                    }
                    //AIモデルの入力画像が生成できないため、Return null
                    return null;
                }


                //エラーがなく、AIモデルの入力画像が生成された場合、
                //Get the output HALCON image processing result Image
                HImage HALCON_ImgForAI = ProcessingProcCall.GetOutputIconicParamImage("ImgForAI");

                //Convert HImage to Bitmap
                if (HALCON_ImgForAI != null)
                {
                    //Convert HImage to Bitmap abd return it.
                    return HImage2Bitmap(HALCON_ImgForAI);
                }

                

            }
            catch (Exception e)
            {
                string title = "AIモデルの入力画像生成_連動ソフトエラー";
                ReportErrorMsg.showMsgBox_IfNotShown("AIモデルの入力画像を生成する時に、"+
                    GlobalConstants.PIInspectTarget+"カメラにエラーが発生した。\n\nメッセージ：\n" + e.Message, title);
               
            }

            //AIモデルの入力画像が生成できないため、Return null
            return null;
        }





        public static Bitmap HImage2Bitmap(HImage image)
        {
            // 读取图像
            //HImage image = new HImage(@"0.png");
            // 获取存放r，g，b值的指针
            image.GetImagePointer3(out IntPtr r, out IntPtr g, out IntPtr b, out string type, out int w, out int h);
            byte[] red = new byte[w * h];
            byte[] green = new byte[w * h];
            byte[] blue = new byte[w * h];
            // 将指针指向地址的值取出来放到byte数组中
            Marshal.Copy(r, red, 0, w * h);
            Marshal.Copy(g, green, 0, w * h);
            Marshal.Copy(b, blue, 0, w * h);



            Bitmap bitmap2 = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            Rectangle rect2 = new Rectangle(0, 0, w, h);
            BitmapData bitmapData2 = bitmap2.LockBits(rect2, ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);


            //*注意：ビルドやリビルドする前に、unsafe コードを下記のステップで許可する必要がある。
            //ソリューションエクスプローラー/プロジェクトを右クリック/プロパティをクリック/
            //左の選択肢でビルドを選択/全般/アンセーフ コードの許可ボックスにチェックを入れ/
            //キーボードctrl+sで保存
            unsafe
            {
                byte* bptr2 = (byte*)bitmapData2.Scan0;
                for (int i = 0; i < w * h; i++)
                {
                    bptr2[i * 4] = blue[i];
                    bptr2[i * 4 + 1] = green[i];
                    bptr2[i * 4 + 2] = red[i];
                    bptr2[i * 4 + 3] = 255;
                }
            }
            bitmap2.UnlockBits(bitmapData2);



            return bitmap2;
        }





    }
}
