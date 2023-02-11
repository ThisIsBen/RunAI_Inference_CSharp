using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace アプリ
{
    //用途：ONNX RuntimeというAIライブラリを利用し、
    //.onnxフォーマットのAIモデルを実行する。
    class AI_OnnxRuntimeImageInspection
    {
        
        //Store the loaded AI model
        private InferenceSession session;

        //Store AI画像処理の分類ラベリング
        private string[] classificationLabel;

        //AIの前処理：切り出しROI画像作成用のHALCONプログラムのオブジェクト
        private static AI_Preprocessing AIPreprocessorObj;

        //Indicate whether the AI model is run on CPU or GPU
        private string CPUOrGPU;



       




        public AI_OnnxRuntimeImageInspection()
        {

            try
            {

                //Read in AI画像処理の分類ラベリング from a txt file.
                classificationLabel = File.ReadAllLines(GlobalConstants.AIClassLabelFilePath);

                //Initialize and load the AIモデルの入力画像作成HALCONプログラム
                AIPreprocessorObj = new AI_Preprocessing();


                //If the GPU of this PC can be used to run an AI model,
                //we use the GPU to run it.
                //If not,
                //we use the CPU to run it.
                try
                {

                    //If GPU Memory limit is specified in the 設定ファイル,
                    //apply GPU Memory limit (unit: Byte)
                    if (GlobalConstants.GPUMemoryLimit > 0)
                    {
                        //Create GPU settings for running an AI model. 
                        OrtCUDAProviderOptions cudaProviderOptions = new OrtCUDAProviderOptions();

                        Dictionary<string, string> providerOptionsDict = new Dictionary<string, string>(2);

                        //Set which GPU to use. 
                        providerOptionsDict["device_id"] = "0";

                        //Set GPU Memory limit (unit: Byte)
                        ulong GPUMemoryLimit_InBytes = (ulong)(GlobalConstants.GPUMemoryLimit * 1024 * 1024 * 1024);
                        providerOptionsDict["gpu_mem_limit"] = GPUMemoryLimit_InBytes.ToString();

                        //Save GPU settings
                        cudaProviderOptions.UpdateOptions(providerOptionsDict);

                        //Read in an AI model and apply GPU settings
                        session = new InferenceSession(GlobalConstants.AIModelFilePath, SessionOptions.MakeSessionOptionWithCudaProvider(cudaProviderOptions));
                        

                    }

                    //If GPU Memory limit is not specified,
                    //run the AI Model on GPU without GPU Memory limit 
                    else
                    {

                        //Set which GPU to use. 
                        int gpuDeviceID = 0;
                        //Read in an AI model and apply GPU settings(without GPU Memory limit)
                        session = new InferenceSession(GlobalConstants.AIModelFilePath, SessionOptions.MakeSessionOptionWithCudaProvider(gpuDeviceID));

                    }


                    //Indicate that the AI model is run on GPU.
                    CPUOrGPU = "GPU使用";

                }
                //If the GPU on this PC can not be used to run an AI model,
                //we use CPU to run it.
                catch (OnnxRuntimeException e)
                {

                    Console.WriteLine("\nこのPCは下記の問題があるため、GPUでAI画像処理を実行できない。\n代わりにCPUでAI画像処理を実行する。\n" +
                        "GPUで実行できない原因：\n"+ e.Message);
                   
                    //Run AI Model on CPU 
                    //Read in an AI model
                    session = new InferenceSession(GlobalConstants.AIModelFilePath);

                    //Indicate that the AI model is run on CPU.
                    CPUOrGPU = "CPU使用";
                }

                catch (Exception e)
                {
                    //output this error message to inform the user
                    string errorMessage = "OnnxRuntimeExceptionと同じ階層のCatch Exepction\n" + e.Message;

                    //ReportErrorMsg.showMsgBox_Anyway(errorMessage, " " + GlobalConstants.PIInspectTarget + "_AI画像処理の初期化エラー");

                    Console.WriteLine(errorMessage);

                    throw;
                }
            }
            //AI画像処理の初期化にエラーが発生した場合のエラー処理
            catch (Exception e)
            {

                //AI画像処理の初期化にエラーが発生したため、
                //この後のAI画像処理の実行を停止させる。
                GlobalConstants.useAIImgProcessing = false;



                //output this error message to inform the user
                string errorMessage = GlobalConstants.PIInspectTarget + " カメラのAI画像処理の初期化にエラーが発生した。\n" +
                "AI画像処理が自動的に停止した。\n"+
                "停止ボタンを押してを停止して、\n管理者に連絡してください。" +
                "\n\n管理者への解決手順：\nStep1 "+ GlobalConstants.PIInspectTarget + " カメラの連動ソフトの設定ファイル内の" +
                "[AI画像処理]のところの設定に問題があるかどうかを確認してください。" +
                 "\n\nこのエラーの原因：\n" + e.Message;

                ReportErrorMsg.showMsgBox_Anyway(errorMessage, " " + GlobalConstants.PIInspectTarget + "_AI画像処理の初期化エラー");

                Console.WriteLine(errorMessage);
            }
           
        }





        //Convert a bitmap to a float tensor
        private Tensor<float> ConvertImageToFloatTensorUnsafe(Bitmap image)
        {
            //There are 3 channels,RGB,in the AI model input image.
            int imageChannel = 3;
            // Create the Tensor with the appropiate dimensions  for the NN
            // NHWC
            //Tensor<float> data = new DenseTensor<float>(new[] { 1,image.Height, image.Width, imageChannel });
            // NCHW
            Tensor<float> data = new DenseTensor<float>(new[] { 1, imageChannel, image.Height, image.Width });

            BitmapData bmd = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, image.PixelFormat);
            

            //*注意：ビルドやリビルドする前に、unsafe コードを下記のステップで許可する必要がある。
            //ソリューションエクスプローラー/プロジェクトを右クリック/プロパティをクリック/
            //左の選択肢でビルドを選択/全般/アンセーフ コードの許可ボックスにチェックを入れ/
            //キーボードctrl+sで保存
            unsafe
            {
                for (int y = 0; y < bmd.Height; y++)
                {
                    // row is a pointer to a full row of data with each of its colors
                    byte* row = (byte*)bmd.Scan0 + (y * bmd.Stride);
                    for (int x = 0; x < bmd.Width; x++)
                    {
                        // Note the order of colors is BGR for Bitmap,
                        // so we need to reverse it to RGB when converting to a float Tensor.

                        // AIモデルに入力するデータの形式：NHWC　（TensorFlowのデフォルトはNHWC）
                        //data[0, y, x, 0] = row[x * imageChannel + 2];//Get R value of the current pixel.
                        //data[0, y, x, 1] = row[x * imageChannel + 1];//Get G value of the current pixel.
                        //data[0, y, x, 2] = row[x * imageChannel + 0];//Get B value of the current pixel.

                        // AIモデルに入力するデータの形式：NCHW　（ONNXのデフォルトはNCHW）
                        data[0, 0, y, x] = row[x * imageChannel + 2]; //Get R value of the current pixel.
                        data[0, 1, y, x] = row[x * imageChannel + 1]; //Get G value of the current pixel.
                        data[0, 2, y, x] = row[x * imageChannel + 0]; //Get B value of the current pixel.
                    }
                }

                image.UnlockBits(bmd);
            }
            return data;
        }


        //全体画像をAIが処理できるサイズに変更する処理はHALCONで実装することになったため、
        //本機能はもういらない。
        /*
        //AI model will use the original image to do the inference.
        public InspectionDetailModel doImageInspection_NoROI(string imageToBeChecked)
        {


            //Step1 Read in the image to be checked.
            using Bitmap originalImage = new Bitmap(imageToBeChecked);





            //Step2 画像をAIモデルに必要なサイズに変更する。例：1920ｘ1080➔224ｘ224

            //ImageオブジェクトのGraphicsオブジェクトを作成する
            using Bitmap resizedImage = new Bitmap(GlobalConstants.AIModelInputImageWidth, GlobalConstants.AIModelInputImageHeight, PixelFormat.Format24bppRgb);
            using Graphics graphEditor = Graphics.FromImage(resizedImage);
            //補間方法として最近傍補間を指定する
            graphEditor.InterpolationMode =
                System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            //画像を縮小して描画する
            graphEditor.DrawImage(originalImage, 0, 0, GlobalConstants.AIModelInputImageWidth, GlobalConstants.AIModelInputImageHeight);

            



            //Step3 Convert a bitmap to a float Tensor,which is acceptable for the Onnx AI model
            Tensor<float> inputValues = ConvertImageToFloatTensorUnsafe(resizedImage);




            //Step4 Set up inputs of the AI model.
            List<NamedOnnxValue> AIModel_Input = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(GlobalConstants.AIModelInputLayerName, inputValues)
            };

           




            //Step5 AI model performs the inference 
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> AIModel_Output = session.Run(AIModel_Input);

            //Get the output of the AI model 
            Tensor<float> AIModel_OutputTensor = AIModel_Output.First().AsTensor<float>();

            //Select the classification result by choosing the class that has the highest probability.
            int highestProbIndex = -1;
            float highestProb = 0;            
            foreach (float probOfClass in AIModel_OutputTensor)
            {
                if (probOfClass > highestProb)
                {
                    highestProb = probOfClass;
                    highestProbIndex += 1;

                }
            }



            //Step6 Output the AI inference Result
            //Use the class that has the highest probability as the inpection result and return it
            string imageInspectionResult = classificationLabel[highestProbIndex];
            //画像処理用固定名称で表示用事象名とカテゴリーデ番号を取得する。
            //カテゴリーデ番号が存在していない場合、取得したのは""である。
            InspectionDetailModel inspectionResult = InspectionDetailController.setUpInspectionResult(imageInspectionResult);
            Console.WriteLine($"AIの判断:{inspectionResult.displayJigoYocyoReasonName} ({highestProb * 100.0}%)");
            Console.WriteLine("AIモデルの実行：" + "ROI前処理なし" + "、"+ CPUOrGPU);
            return inspectionResult;

          
        }
        */

        //We run a HALCON program to perform the preprocessing,
        //and return the input image for our AI model. 
        //AI model will use that input image to do the inference.
        public InspectionDetailModel doImageInspection(string imageToBeChecked)
        {

            //To save the image processing result
            string imageInspectionResult;
            InspectionDetailModel inspectionResult;

            //Step1  Create the AIモデルの入力画像
            //AIモデルの入力画像の前処理HALCONプログラムを実行し、
            //AIモデルの入力画像を生成する。
            using Bitmap Bitmap_ImgForAI = AIPreprocessorObj.preprocessImgForAI(imageToBeChecked);





            //Step2 Perform the error handling of AIモデルの入力画像作成機能
            //AIモデルの入力画像の生成にエラーが発生した場合、
            //この画像のAI画像処理を止めて、
            //指定したエラー(ERR)をAI画像処理結果として返す。
            if (Bitmap_ImgForAI == null)
            {
                //画像処理用固定名称で表示用事象名とカテゴリーデ番号を取得する。
                //カテゴリーデ番号が存在していない場合、取得したのは""である。
                if(AIPreprocessorObj.Defined_HALCON_ERR != "")
                {
                    imageInspectionResult = "ERR,"+ AIPreprocessorObj.Defined_HALCON_ERR;
                    
                }
                else
                {
                    imageInspectionResult = "ERR,ERR_UndefinedError";
                }
                


                inspectionResult = InspectionDetailController.setUpInspectionResult(imageInspectionResult);
                Console.WriteLine($"AIの判断:{inspectionResult.displayJigoYocyoReasonName}のため、この画像を処理しない。");
                Console.WriteLine("AIモデルの実行：" + CPUOrGPU);
                return inspectionResult;
                
            }





            //Step3 Convert a bitmap to a float Tensor,which is acceptable for the Onnx AI model
            //既にHALCONによってAIモデルに必要なサイズに変更したため、
            //サイズの変更は要らない。例：1920ｘ1080➔224ｘ224

            //AIモデルの入力画像が生成された場合、
            //AIモデルが処理できるフォマードに変換し、AIモデルに変換してもらう。
            Tensor<float> inputValues = ConvertImageToFloatTensorUnsafe(Bitmap_ImgForAI);





            //Step4 Setup inputs of the AI model.
            List<NamedOnnxValue> AIModel_Input = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(GlobalConstants.AIModelInputLayerName, inputValues)
            };






            //Step5 AI model performs the inference 
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> AIModel_Output = session.Run(AIModel_Input);

            //Get the output of the AI model 
            Tensor<float> AIModel_OutputTensor = AIModel_Output.First().AsTensor<float>();

            //Select the classification result by choosing the class that has the highest probability.
            int highestProbIndex = -1;
            float highestProb = 0;           
            foreach (float probOfClass in AIModel_OutputTensor)
            {
                if (probOfClass > highestProb)
                {
                    highestProb = probOfClass;
                    highestProbIndex += 1;

                }
            }



            //Step6 Output the AI inference Result
            //Use the class that has the highest probability as the inpection result and return it
            imageInspectionResult = classificationLabel[highestProbIndex];
            //画像処理用固定名称で表示用事象名とカテゴリーデ番号を取得する。
            //カテゴリーデ番号が存在していない場合、取得したのは""である。
            inspectionResult = InspectionDetailController.setUpInspectionResult(imageInspectionResult);
            Console.WriteLine($"AIの判断:{inspectionResult.displayJigoYocyoReasonName} ({highestProb * 100.0}%)");
            Console.WriteLine("AIモデルの実行：" + CPUOrGPU);
            return inspectionResult;


        }


    }
}
