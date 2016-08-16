using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using NAudio.Dsp;
using Emgu.CV;

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
        private Capture imageCapture;
        private Thread update;
        private float[,] handCoordinates = new float[2, 2] { { 0, float.MaxValue }, { 0, float.MaxValue } };
        private int width = 640, height = 480, fps = 30, scalingFactor = 4;
        private DateTime startTime;
        private Bitmap colorBitmap;
        private Mat hand1, hand2;
        private Boolean templatesSet = false; //defines whether hand-templates are already set

        public MainWindow()
        {
            InitializeComponent();

            //Initialize image capturing
            imageCapture = new Capture();
            imageCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, width);
            imageCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, height);
            imageCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps, fps);
            imageCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Staturation, 90.0f);
            imageCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Contrast, 100.0f);

            //Audio init
            //ToDo

            //Begin of template Initialization
            startTime = DateTime.Now;

            update = new Thread(new ThreadStart(Update));
            update.Start();
        }

        private void Update()
        {
            //Start Acquire/Release frame loop
            while (imageCapture!=null)
            { 
                colorBitmap = imageCapture.QueryFrame().Bitmap;
                Mat colorMat = new Mat();
                CvInvoke.Resize(imageCapture.QueryFrame(), colorMat, new System.Drawing.Size(width / scalingFactor, height / scalingFactor), 0, 0, Emgu.CV.CvEnum.Inter.Cubic);
                
                if ((DateTime.Now.Ticks - startTime.Ticks) < 70000000)
                    //Initialization phase
                    using (var graphics = Graphics.FromImage(colorBitmap))
                    {
                        Pen fancyPen = new Pen(Color.Cyan, 4);
                        graphics.DrawRectangle(fancyPen, colorBitmap.Width / 5, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                        graphics.DrawRectangle(fancyPen, colorBitmap.Width / 5 * 3, colorBitmap.Height / 3, colorBitmap.Width / 5, colorBitmap.Height / 3);
                    }
                else if (!templatesSet)
                {
                    //set hand-templates if not already set
                    Rectangle rectHand1 = new Rectangle(colorMat.Cols / 5, colorMat.Rows / 3, colorMat.Cols / 5, colorMat.Rows / 3);
                    Rectangle rectHand2 = new Rectangle((colorMat.Cols / 5) * 3, colorMat.Rows / 3, colorMat.Cols / 5, colorMat.Rows / 3);

                    hand1 = new Mat(colorMat, rectHand1);
                    hand2 = new Mat(colorMat, rectHand2);

                    templatesSet = true;
                }
                else
                {
                    //Initializing Template Matching
                    Rectangle left = new Rectangle(0, 0, colorMat.Cols / 2, colorMat.Rows);
                    Rectangle right = new Rectangle(colorMat.Cols / 2, 0, colorMat.Cols / 2, colorMat.Rows);
                    Mat leftBitmap = new Mat(colorMat, left);
                    Mat rightBitmap = new Mat(colorMat, right);

                    //match templates
                    templateMatch(leftBitmap, hand1, false);
                    templateMatch(rightBitmap, hand2, true);
                }
                //Render bitmap to be forwarded into imageStream
                render(colorBitmap);

                //Release frame
                colorBitmap.Dispose();
                colorMat.Dispose();
            }
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
            //Saveguard for bugs that shouldn't even be possible
            else if ((matchLoc.X + image.Cols) * scalingFactor <= handCoordinates[(int)hand.LEFT, (int)coordinate.X])
            {
                handCoordinates[(int)hand.RIGHT, (int)coordinate.X] = (matchLoc.X + image.Cols + template.Cols / 2) * scalingFactor;
                handCoordinates[(int)hand.RIGHT, (int)coordinate.Y] = (matchLoc.Y + template.Rows / 2) * scalingFactor;
            }
            else
            {
                handCoordinates[(int)hand.RIGHT, (int)coordinate.X] = handCoordinates[(int)hand.LEFT, (int)coordinate.X];
                handCoordinates[(int)hand.RIGHT, (int)coordinate.Y] = handCoordinates[(int)hand.LEFT, (int)coordinate.Y];
                handCoordinates[(int)hand.LEFT, (int)coordinate.X] = (matchLoc.X + image.Cols + template.Cols / 2) * scalingFactor;
                handCoordinates[(int)hand.LEFT, (int)coordinate.Y] = (matchLoc.Y + template.Rows / 2) * scalingFactor;
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
                if (handCoordinates[(int)hand.RIGHT, (int)coordinate.X] != 0)
                {
                    //Draw UI and set wave parameters
                    soundUI(bitmap);
                }
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

        private void soundUI(Bitmap image)
        {
            int bottomOffset = (int)(hand1.Rows*scalingFactor), topOffset = (int)(hand1.Rows/2*scalingFactor);
            float correctedFreqValue = (height - bottomOffset) - handCoordinates[(int)hand.RIGHT, (int)coordinate.Y] ;

            if (correctedFreqValue < 0) //disable Sound under certain margin
            {
                correctedFreqValue = -125.0f;
            }
            float frequency = 250 + correctedFreqValue * 2f;  //frequency of output wavesound  

            int correctedVolumeValue = (height - bottomOffset) - (int)handCoordinates[(int)hand.LEFT, (int)coordinate.Y];

            if (correctedVolumeValue < 0) //disable Sound under certain margin
            {
                correctedVolumeValue = 0;
            }
            int volume = correctedVolumeValue * 100 / (height - bottomOffset - topOffset);  //volume of output wavesound

            using (var graphics = Graphics.FromImage(image))
            {
                //define System.Drawing utensils for creating UI
                Brush fancyBrush = new SolidBrush(Color.DarkGray);
                Brush labelBrush = new SolidBrush(Color.Orange);
                Pen fancyPen = new Pen(Color.Gray, 2);
                Pen scalePen = new Pen(Color.Orange, 4);
                Font labelFont = new Font("Arial", 11);
                PointF pointVol1 = new PointF(35.0f, topOffset);
                PointF pointVol2 = new PointF(60.0f, topOffset);
                PointF pointVol3 = new PointF(35.0f, height-bottomOffset);
                PointF pointFreq1 = new PointF(width-35.0f, topOffset);
                PointF pointFreq2 = new PointF(width-55.0f, topOffset);
                PointF pointFreq3 = new PointF(width-55.0f, height - topOffset);
                PointF pointFreq4 = new PointF(width-35.0f, height - topOffset);
                PointF[] volPoints = {pointVol1, pointVol2, pointVol3};
                PointF[] freqPoints = { pointFreq1, pointFreq2, pointFreq3, pointFreq4 };
                FillMode newFillMode = FillMode.Winding;

                // Draw volume scale
                graphics.FillPolygon(fancyBrush, volPoints, newFillMode);
                graphics.DrawPolygon(fancyPen, volPoints);
                if (volume > 0)
                {
                    graphics.DrawLine(scalePen, 70.0f, handCoordinates[(int)hand.LEFT, (int)coordinate.Y], 25.0f, handCoordinates[(int)hand.LEFT, (int)coordinate.Y]);
                    graphics.DrawString(volume.ToString(), labelFont, labelBrush, 73.0f, handCoordinates[(int)hand.LEFT, (int)coordinate.Y] - 10);
                }
                else
                {
                    graphics.DrawLine(scalePen, 70.0f, pointVol3.Y, 25.0f, pointVol3.Y);
                    graphics.DrawString(volume.ToString(), labelFont, labelBrush, 73.0f, pointVol3.Y - 10);
                }

                // Draw frequency scale
                graphics.FillPolygon(fancyBrush, freqPoints, newFillMode);
                graphics.DrawPolygon(fancyPen, freqPoints);
                graphics.DrawLine(scalePen, width-65.0f, handCoordinates[(int)hand.RIGHT, (int)coordinate.Y], width-25.0f, handCoordinates[(int)hand.RIGHT, (int)coordinate.Y]);
                graphics.DrawString(getNote(frequency), labelFont, labelBrush, width-90.0f, handCoordinates[(int)hand.RIGHT, (int)coordinate.Y] - 10);
            }
        }

        private string getNote(float freq)
        {
            //Mapping frequency to the closest note between c' and h''
            if (freq < 277)
                return "c'";
            if (freq < 311)
                return "d'";
            if (freq < 339)
                return "e'";
            if (freq < 370)
                return "f'";
            if (freq < 416)
                return "g'";
            if (freq < 466)
                return "a'";
            if (freq < 508)
                return "h'";
            if (freq < 559)
                return "c''";
            if (freq < 623)
                return "d''";
            if (freq < 678)
                return "e''";
            if (freq < 740)
                return "f''";
            if (freq < 831)
                return "g''";
            if (freq < 933)
                return "a''";

            return "h''";
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

            //Normalize camera settings
            imageCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Staturation, 50.0f);
            imageCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Contrast, 50.0f);
            imageCapture.Dispose();
        }
    }
}
