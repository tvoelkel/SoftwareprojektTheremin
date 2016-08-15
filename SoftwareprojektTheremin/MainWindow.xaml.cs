using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using NAudio.Dsp;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Drawing.Imaging;

namespace SoftwareprojektTheremin
{

    public class SineWaveProvider32 : WaveProvider32
    {
        int sample;

        public SineWaveProvider32()
        {
            Frequency = 1000;
            Amplitude = 0.25f;
            freq = Frequency;
            amp = Amplitude;
            ActualPositionSineWavePositive = false;
            PreviousPositionSineWavePositive = false;
        }

        public float Frequency;
        public float Amplitude;
        private float freq;
        private float amp;
        private bool ActualPositionSineWavePositive, PreviousPositionSineWavePositive;

		//private BiQuadFilter AudioFilter = BiQuadFilter.SetHighPassFilter(16000,100,1);

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            float freq = Frequency;
            float amp = Amplitude;
            for (int n = 0; n < sampleCount; n++)
            {
                buffer[n + offset] = (float)(amp * Math.Sin((2 * Math.PI * sample * freq) / sampleRate));
                PreviousPositionSineWavePositive = ActualPositionSineWavePositive;
                ActualPositionSineWavePositive = buffer[n + offset] >= 0;
                sample++;
                if (sample >= sampleRate) sample = 0;
                if (PreviousPositionSineWavePositive == false && ActualPositionSineWavePositive == true)
                {
                    freq = Frequency;
                    amp = Amplitude;
                }
                //@Note Wenn das nicht funktioniert, NumberOfBuffers und DesiredLatency von Waveout anpassen
            }
            return sampleCount;
        }
    }


    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        enum hand : Int32
        {
            LEFT = 0, RIGHT = 1
        }
        enum cord : Int32
        {
            X = 0, Y = 1
        }
        private PXCMSession session;
        private float[,] blobCoordinates = new float[2, 2] { { 0, 0 }, { 0, 0 }};
        private PXCMSenseManager senseManager;
        private Thread update;
        private PXCMBlobModule blobModule;
        private PXCMBlobConfiguration blobConfig;
        private PXCMBlobData blobData;
        private PXCMBlobData.IBlob[] blobList = new PXCMBlobData.IBlob[2];
        private int trackingDistance = 1000;
        private int frequency = 35;
        private float volume = 0.5F;
        Bitmap colorBitmap, checkBitmap;
        DateTime startTime;
        bool templatesSet = false;
        Mat hand1 = null;
        Mat hand2 = null;




        //  private Thread ton;
        private SineWaveProvider32 sineWaveProvider = new SineWaveProvider32();


        public MainWindow()
        {
            InitializeComponent();

            // Configure RealSense session and SenseManager interface
            session = PXCMSession.CreateInstance();
            senseManager = session.CreateSenseManager();
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 320, 240, 30);
            senseManager.EnableBlob();
            senseManager.Init();

            //Create blobModule from SenseManager
            blobModule = senseManager.QueryBlob();
            blobConfig = blobModule.CreateActiveConfiguration();
            blobConfig.SetMaxBlobs(2);
            blobConfig.SetMaxDistance(trackingDistance);
            blobConfig.SetMaxObjectDepth(100);
            blobConfig.SetMinPixelCount(400);
            blobConfig.EnableColorMapping(true);
            blobConfig.ApplyChanges();
            blobData = blobModule.CreateOutput();

            //Audio
            WaveOut waveOut;
            sineWaveProvider.SetWaveFormat(16000, 1);
            sineWaveProvider.Frequency = 1000;
            sineWaveProvider.Amplitude = 0;
            waveOut = new WaveOut();
            waveOut.Init(sineWaveProvider);
            waveOut.Play();

            startTime = DateTime.Now;
            // Start Update thread
            update = new Thread(new ThreadStart(Update));
            update.Start();
        }



        private void Update()
        {
            // Start AcquireFrame-ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                PXCMCapture.Sample sample = senseManager.QuerySample();

                PXCMImage.ImageData colorData;
                blobData.Update();

                /*while (blobData.QueryNumberOfBlobs() < 2)
                {
                    trackingDistance += 100;
                    blobConfig.SetMaxDistance(trackingDistance);
                    blobConfig.ApplyChanges();
                    blobData.Update();
                    
                    senseManager.ReleaseFrame();

                   if(trackingDistance > 3000)
                       trackingDistance = 600;
                   }
                   {
                }
                */
                /*
                for (int i = 0; i < 2; i++)
                {
                    blobData.QueryBlobByAccessOrder(i, PXCMBlobData.AccessOrderType.ACCESS_ORDER_NEAR_TO_FAR, out blobList[i]);
                }
                if (blobData.QueryNumberOfBlobs() == 2)
                {
                    if (blobCoordinates[3, (int)cord.Y] == -1)
                    {
                        //smoothing
                    }
                    if (blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x > blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x)
                    {
                        blobCoordinates[(int)hand.LEFT, (int)cord.Y] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.RIGHT, (int)cord.Y] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.LEFT, (int)cord.X] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                        blobCoordinates[(int)hand.RIGHT, (int)cord.X] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                    }
                    else
                    {
                        blobCoordinates[(int)hand.RIGHT, (int)cord.Y] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.LEFT, (int)cord.Y] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.RIGHT, (int)cord.X] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                        blobCoordinates[(int)hand.LEFT, (int)cord.X] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                    }
                }
                */
                // Tonausgabe: aktuelle Tonausgabe beenden und neue beginnen
                frequency = (int)(blobCoordinates[(int)hand.LEFT, (int)cord.Y] * 1.8);
                volume = (500 - blobCoordinates[(int)hand.RIGHT, (int)cord.Y]) / 500;

                sineWaveProvider.Frequency = frequency;
                sineWaveProvider.Amplitude = volume;

                // Get color image data
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);
                checkBitmap = new Bitmap(colorBitmap);

                if ((DateTime.Now.Ticks - startTime.Ticks) < 50000000)
                {
                    using (var graphics = Graphics.FromImage(colorBitmap))
                    {
                        System.Drawing.Pen fancyPen = new System.Drawing.Pen(System.Drawing.Color.Cyan, 5);
                        graphics.DrawRectangle(fancyPen, colorBitmap.Width / 5, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                        graphics.DrawRectangle(fancyPen, (colorBitmap.Width / 5) * 3, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                    }
                }
                else if (!templatesSet)
                {
                    Bitmap template1 = new Bitmap(colorBitmap.Width / 5, colorBitmap.Height / 3);
                    Bitmap template2 = new Bitmap(colorBitmap.Width / 5, colorBitmap.Height / 3);
                    Rectangle rect1 = new Rectangle(colorBitmap.Width / 5, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                    Rectangle rect2 = new Rectangle((colorBitmap.Width / 5) * 3, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                    Rectangle dest = new Rectangle(0, 0, template1.Width, template1.Height);
                    using (var graphics = Graphics.FromImage(template1))
                    {
                        graphics.DrawImage(colorBitmap, dest, rect1, GraphicsUnit.Pixel);
                    }
                    using (var graphics = Graphics.FromImage(template2))
                    {
                        graphics.DrawImage(colorBitmap, dest, rect2, GraphicsUnit.Pixel);
                    }
                    template1.Save("hand1.bmp");
                    hand1 = CvInvoke.Imread("hand1.bmp", Emgu.CV.CvEnum.LoadImageType.Color);
                    template2.Save("hand2.bmp");
                    hand2 = CvInvoke.Imread("hand2.bmp", Emgu.CV.CvEnum.LoadImageType.Color);
                    templatesSet = true;
                }
                else {
                    // Update UI
                    colorBitmap.Save("bitmap.bmp", ImageFormat.Bmp);
                    //Mat img = new Mat("bitmap.bmp", Emgu.CV.CvEnum.LoadImageType.AnyColor);
                    Mat img = CvInvoke.Imread("bitmap.bmp", Emgu.CV.CvEnum.LoadImageType.Color);
                    templateMatch(img, hand1, false);
                    Mat img2 = CvInvoke.Imread("checkBitmap.bmp", Emgu.CV.CvEnum.LoadImageType.Color);
                    templateMatch(img2, hand2, true);
                }
                Render(colorBitmap);

                // Release frame
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();
            }
        }

        private void Render(Bitmap bitmap)
        {
            BitmapImage bitmapImage = ConvertBitmap(bitmap);

            if (bitmapImage != null)
            {
                // Update the WPF Image control
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    imgStream.Source = bitmapImage;
                    labelR.Content = blobCoordinates[(int)hand.RIGHT, (int)cord.Y];
                    labelL.Content = blobCoordinates[(int)hand.LEFT, (int)cord.Y];
                }));
            }
        }

        private BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            BitmapImage bitmapImage = null;
            System.Drawing.Pen fancyPen = new System.Drawing.Pen(System.Drawing.Color.Cyan, 5);

            if (bitmap != null)
            {
                if (blobData.QueryNumberOfBlobs() == 2)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            //graphics.DrawRectangle(fancyPen, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y, 5, 5);
                            //graphics.DrawRectangle(fancyPen, blobCoordinates[(int)hand.LEFT, (int)cord.X] - 50, blobCoordinates[(int)hand.LEFT, (int)cord.Y], 5, 5);
                            //graphics.DrawRectangle(fancyPen, blobCoordinates[(int)hand.RIGHT, (int)cord.X] - 50, blobCoordinates[(int)hand.RIGHT, (int)cord.Y], 5, 5);
                        }
                    }
                }
                bitmap.RotateFlip(RotateFlipType.Rotate180FlipY);
                MemoryStream memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ShutDown();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            ShutDown();
            this.Close();
        }

        private void ShutDown()
        {
            // Stop the Update thread
            update.Abort();

            // Dispose RealSense objects
            blobModule.Dispose();
            blobData.Dispose();
            senseManager.Dispose();
            session.Dispose();
        }

        void templateMatch(Mat img, Mat template, bool secondIteration)
        {
            DateTime checkTime = DateTime.Now;


                /// Create the result matrix
                int result_cols = img.Cols - template.Cols + 1;
                int result_rows = img.Rows - template.Rows + 1;
                Mat result = new Mat();
                result.Create(result_rows, result_cols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);

                /// Do the Matching and Normalize
                CvInvoke.MatchTemplate(img, template, result, Emgu.CV.CvEnum.TemplateMatchingType.CcoeffNormed);
                CvInvoke.Normalize(result, result, 0, 1, Emgu.CV.CvEnum.NormType.MinMax, Emgu.CV.CvEnum.DepthType.Default, null);

                /// Localizing the best match with minMaxLoc
                double minVal = 0; double maxVal = 0;
                System.Drawing.Point minLoc = new System.Drawing.Point(0, 0), maxLoc = new System.Drawing.Point(0, 0), matchLoc = new System.Drawing.Point(0, 0);

                CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc, null);

                /// For SQDIFF and SQDIFF_NORMED, the best matches are lower values. For all the other methods, the higher the better
                /*if (match_method == CV_TM_SQDIFF || match_method == CV_TM_SQDIFF_NORMED)
                { matchLoc = minLoc; }
                else
                { */

                matchLoc = maxLoc;
                if (!secondIteration)
                {
                    blobCoordinates[(int)hand.LEFT, (int)cord.X] = matchLoc.X + template.Cols / 2;
                    blobCoordinates[(int)hand.LEFT, (int)cord.Y] = matchLoc.Y + template.Rows / 2;
                }
                else
                {
                    if (matchLoc.X <= blobCoordinates[(int)hand.LEFT, (int)cord.X])
                    {
                        blobCoordinates[(int)hand.RIGHT, (int)cord.X] = matchLoc.X + template.Cols / 2;
                        blobCoordinates[(int)hand.RIGHT, (int)cord.Y] = matchLoc.Y + template.Rows / 2;
                    }
                    else
                    {
                        blobCoordinates[(int)hand.RIGHT, (int)cord.X] = blobCoordinates[(int)hand.LEFT, (int)cord.X];
                        blobCoordinates[(int)hand.RIGHT, (int)cord.Y] = blobCoordinates[(int)hand.LEFT, (int)cord.Y];
                        blobCoordinates[(int)hand.LEFT, (int)cord.X] = matchLoc.X + template.Cols / 2;
                        blobCoordinates[(int)hand.LEFT, (int)cord.Y] = matchLoc.Y + template.Rows / 2;
                    }
                }

                // Show me what you got
                using (var graphics = Graphics.FromImage(colorBitmap))
                {
                    System.Drawing.Pen fancierPen = new System.Drawing.Pen(System.Drawing.Color.Orange, 2);
                    graphics.DrawRectangle(fancierPen, matchLoc.X, matchLoc.Y, template.Cols, template.Rows);
                }

                if (!secondIteration)
                {
                    using (var graphics = Graphics.FromImage(checkBitmap))
                    {
                        SolidBrush fancyBrush = new SolidBrush(Color.Black);
                        graphics.FillRectangle(fancyBrush, matchLoc.X, matchLoc.Y, template.Cols, template.Rows);
                    }
                    checkBitmap.Save("checkBitmap.bmp", ImageFormat.Bmp);
                }
            Console.WriteLine((DateTime.Now.Ticks - checkTime.Ticks)/10000);
        }

            private Bitmap scale(Bitmap image, int factor)
    {
        Rectangle source = new Rectangle(0, 0, image.Width, image.Height);
        Rectangle dest = new Rectangle(0, 0, image.Width / factor, image.Height / factor);
        var graphics = Graphics.FromImage(image);
       
        graphics.DrawImage(image, dest, source, GraphicsUnit.Pixel);

        return image;
    }
    }


}
