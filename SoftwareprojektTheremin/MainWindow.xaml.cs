using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using NAudio.Dsp;
using Emgu.CV;
using System.Drawing.Imaging;

namespace SoftwareprojektTheremin
{
    public partial class MainWindow : System.Windows.Window
    {
        enum hand : Int32
        {
            LEFT = 0, RIGHT = 1
        }

        enum coordinate : Int32
        {
            X = 0, Y = 1
        }
        private PXCMSession session;
        private PXCMSenseManager senseManager;
        private Thread update;
        private float[,] handCoordinates = new float[2, 2] { { 0, 0 }, { 0, 0 } };
        private int heigth = 640, width = 480, fps = 30, scalingFactor = 4;
        private DateTime startTime;
        private Bitmap colorBitmap, checkBitmap;
        private Mat hand1, hand2;
        private Boolean templatesSet = false; //defines whether hand-templates are already set

        public MainWindow()
        {
            InitializeComponent();

            //Configure RealSense session and SenseManager
            session = PXCMSession.CreateInstance();
            senseManager = session.CreateSenseManager();
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, heigth, width, fps);    //Get color image stream
            senseManager.Init();

            //Audio
            //ToDo

            startTime = DateTime.Now;   //Begin of template Initialization
            update = new Thread(new ThreadStart(Update));
            update.Start();
        }

        private void Update()
        {
            //Start Acquire/Release frame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                PXCMCapture.Sample sample = senseManager.QuerySample();
                PXCMImage.ImageData colorData;

                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                if ((DateTime.Now.Ticks - startTime.Ticks) < 70000000)   //Initialization phase
                    using (var graphics = Graphics.FromImage(colorBitmap))
                    {
                        Pen fancyPen = new Pen(Color.Cyan, 4);
                        graphics.DrawRectangle(fancyPen, colorBitmap.Width / 5, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                        graphics.DrawRectangle(fancyPen, colorBitmap.Width / 5 * 3, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                    }
                else if (!templatesSet)
                {
                    //set hand-templates if not already set
                    Bitmap template1 = new Bitmap(colorBitmap.Width / 5, colorBitmap.Height / 3);
                    Bitmap template2 = new Bitmap(template1);
                    Rectangle rectHand1 = new Rectangle(colorBitmap.Width / 5, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                    Rectangle rectHand2 = new Rectangle((colorBitmap.Width / 5) * 3, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                    Rectangle dest = new Rectangle(0, 0, template1.Width, template1.Height);

                    using (var graphics = Graphics.FromImage(template1))
                    {
                        graphics.DrawImage(colorBitmap, dest, rectHand1, GraphicsUnit.Pixel);
                    }
                    using (var graphics = Graphics.FromImage(template2))
                    {
                        graphics.DrawImage(colorBitmap, dest, rectHand2, GraphicsUnit.Pixel);
                    }
                    scale(template1, scalingFactor).Save("hand1.bmp");
                    scale(template2, scalingFactor).Save("hand2.bmp");
                    hand1 = CvInvoke.Imread("hand1.bmp", Emgu.CV.CvEnum.LoadImageType.Color);
                    hand2 = CvInvoke.Imread("hand2.bmp", Emgu.CV.CvEnum.LoadImageType.Color);

                    templatesSet = true;
                }
                else
                {
                    //Initializing Template Matching
                    Bitmap colorBitmapScaled = new Bitmap(colorBitmap);
                    scale(colorBitmapScaled, scalingFactor).Save("bitmap.bmp", ImageFormat.Bmp);
                    checkBitmap = new Bitmap(colorBitmap);
                    Mat image = CvInvoke.Imread("bitmap.bmp", Emgu.CV.CvEnum.LoadImageType.Color);
                    templateMatch(image, hand1, false);
                    Mat image2 = CvInvoke.Imread("checkBitmap.bmp", Emgu.CV.CvEnum.LoadImageType.Color);
                    templateMatch(image2, hand2, true);
                }

                //Audio Output (stop current output and start new one)
                //ToDo

                render(colorBitmap);

                //Release frame
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();
            }
        }

        private Bitmap scale(Bitmap image, int factor)
        {
            Bitmap scaled = new Bitmap(image.Width / scalingFactor, image.Height / scalingFactor);
            Rectangle source = new Rectangle(0, 0, image.Width, image.Height);
            Rectangle dest = new Rectangle(0, 0, image.Width / factor, image.Height / factor);
            var graphics = Graphics.FromImage(scaled);

            graphics.DrawImage(image, dest, source, GraphicsUnit.Pixel);

            return scaled;
        }

        private void templateMatch(Mat image, Mat template, bool secondIteration)
        {
            //Create result matrix
            int resultCols = image.Cols - template.Cols + 1;
            int resultRows = image.Rows - template.Rows + 1;
            Mat result = new Mat();
            result.Create(resultRows, resultCols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);

            //Match template and normalize
            CvInvoke.MatchTemplate(image, template, result, Emgu.CV.CvEnum.TemplateMatchingType.CcoeffNormed);
            CvInvoke.Normalize(result, result, 0, 1, Emgu.CV.CvEnum.NormType.MinMax, Emgu.CV.CvEnum.DepthType.Default, null);

            //Apply minMaxLoc to find best match
            double minVal = 0; double maxVal = 0;
            System.Drawing.Point minLoc = new System.Drawing.Point(0, 0), maxLoc = new System.Drawing.Point(0, 0), matchLoc = new System.Drawing.Point(0, 0);
            CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc, null);

            matchLoc = maxLoc;

            //Update hand coordinate array
            if (!secondIteration)
            {
                handCoordinates[(int)hand.LEFT, (int)coordinate.X] = (matchLoc.X + template.Cols / 2) * scalingFactor;
                handCoordinates[(int)hand.LEFT, (int)coordinate.Y] = (matchLoc.Y + template.Rows / 2) * scalingFactor;
            }
            else if (matchLoc.X * scalingFactor <= handCoordinates[(int)hand.LEFT, (int)coordinate.X])
            {
                handCoordinates[(int)hand.RIGHT, (int)coordinate.X] = (matchLoc.X + template.Cols / 2) * scalingFactor;
                handCoordinates[(int)hand.RIGHT, (int)coordinate.Y] = (matchLoc.Y + template.Rows / 2) * scalingFactor;
            }
            else
            {
                handCoordinates[(int)hand.RIGHT, (int)coordinate.X] = handCoordinates[(int)hand.LEFT, (int)coordinate.X];
                handCoordinates[(int)hand.RIGHT, (int)coordinate.Y] = handCoordinates[(int)hand.LEFT, (int)coordinate.Y];
                handCoordinates[(int)hand.LEFT, (int)coordinate.X] = (matchLoc.X + template.Cols / 2) * scalingFactor;
                handCoordinates[(int)hand.LEFT, (int)coordinate.Y] = (matchLoc.Y + template.Rows / 2) * scalingFactor;
            }

            //create checkBitmap for second iteration (same bitmap, without the location of the first hand)
            if (!secondIteration)
            {
                using (var graphics = Graphics.FromImage(checkBitmap))
                {
                    SolidBrush fancyBrush = new SolidBrush(Color.HotPink);
                    graphics.FillRectangle(fancyBrush, matchLoc.X*scalingFactor, matchLoc.Y*scalingFactor, template.Cols*scalingFactor, template.Rows*scalingFactor);
                }
                scale(checkBitmap, scalingFactor).Save("checkBitmap.bmp", ImageFormat.Bmp);
            }
        }

        private void render(Bitmap bitmap)
        {
            BitmapImage bitmapImage = ConvertBitmap(bitmap);

            if (bitmapImage != null)
            {
                // Update the WPF Image control
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    imgStream.Source = bitmapImage;
                    labelR.Content = handCoordinates[(int)hand.RIGHT, (int)coordinate.Y];
                    labelL.Content = handCoordinates[(int)hand.LEFT, (int)coordinate.Y];
                }));
            }
        }

        private BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            BitmapImage bitmapImage = null;

            if (bitmap != null)
            {
                if(handCoordinates[(int)hand.RIGHT, (int)coordinate.X] != 0)
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    System.Drawing.Pen fancierPen = new System.Drawing.Pen(System.Drawing.Color.Orange, 2);
                    graphics.DrawRectangle(fancierPen,
                        handCoordinates[(int)hand.RIGHT, (int)coordinate.X] - (hand1.Cols * scalingFactor) / 2,
                        handCoordinates[(int)hand.RIGHT, (int)coordinate.Y] - (hand1.Rows * scalingFactor) / 2,
                        hand1.Cols * scalingFactor,
                        hand1.Rows * scalingFactor);

                    graphics.DrawRectangle(fancierPen,
                        handCoordinates[(int)hand.LEFT, (int)coordinate.X] - (hand2.Cols * scalingFactor) / 2,
                        handCoordinates[(int)hand.LEFT, (int)coordinate.Y] - (hand2.Rows * scalingFactor) / 2,
                        hand2.Cols * scalingFactor,
                        hand2.Rows * scalingFactor);
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
            senseManager.Dispose();
            session.Dispose();
        }
    }
}
