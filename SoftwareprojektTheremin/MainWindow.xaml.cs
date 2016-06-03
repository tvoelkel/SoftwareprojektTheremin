using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SoftwareprojektTheremin
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum coordinates: int{
            LEFT = 0, RIGHT = 1, X = 0, Y = 1
        };
        private static bool isPlaying = false;
        private PXCMSession session;
        private float[,] blobCoordinates = new float[4,2] { { -1, -1 },{ -1, -1 },{ -1, -1 }, { -1, -1 } };
        private PXCMSenseManager senseManager;
        private Thread update;
        private PXCMBlobModule blobModule;
        private PXCMBlobConfiguration blobConfig;
        private PXCMBlobData blobData;
        private PXCMBlobData.IBlob[] blobList = new PXCMBlobData.IBlob[2];
        private int trackingDistance = 1000;
        private static UInt32 frequency;
        private static UInt32 volume;

        public MainWindow()
        {
            InitializeComponent();

            // Configure RealSense session and SenseManager interface
            session = PXCMSession.CreateInstance();
            senseManager = session.CreateSenseManager();
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);
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
                Bitmap colorBitmap;
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
                   {
                       trackingDistance = 600;
                   }
                }
                */

                for (int i = 0; i<2; i++)
                {
                    blobData.QueryBlobByAccessOrder(i, PXCMBlobData.AccessOrderType.ACCESS_ORDER_NEAR_TO_FAR, out blobList[i]);
                }
                if (blobData.QueryNumberOfBlobs() == 2)
                {
                    if(blobCoordinates[3, (int)coordinates.Y] == -1) {
                        //smoothing
                    }
                    if (blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x > blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x)
                    {
                        blobCoordinates[(int)coordinates.LEFT,(int)coordinates.Y] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)coordinates.RIGHT, (int)coordinates.Y] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                    }
                    else
                    {
                        blobCoordinates[(int)coordinates.RIGHT, (int)coordinates.Y] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)coordinates.LEFT, (int)coordinates.Y] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                    }
                }

                // Get color image data
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                // Update UI
                Render(colorBitmap);

                //Play Sound
                frequency = (uint)(500 - blobCoordinates[(int)coordinates.LEFT, (int)coordinates.Y]) * 2;
                volume = (uint)(500- blobCoordinates[(int)coordinates.RIGHT, (int)coordinates.Y]) * 42;
                if (!isPlaying)
                {
                    PlaySound();
                }
           
                
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
                    labelR.Content = blobCoordinates[(int)coordinates.RIGHT, (int)coordinates.Y];
                    labelL.Content = blobCoordinates[(int)coordinates.LEFT, (int)coordinates.Y];
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
                            graphics.DrawRectangle(fancyPen, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x-50, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y, 5, 5);
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

        private static void PlaySound()
        {
            var mStrm = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(mStrm);
            isPlaying = true;

            const double TAU = 2 * Math.PI;
            int formatChunkSize = 16;
            int headerSize = 8;
            short formatType = 1;
            short tracks = 1;
            int samplesPerSecond = 44100;
            short bitsPerSample = 16;
            short frameSize = (short)(tracks * ((bitsPerSample + 7) / 8));
            int bytesPerSecond = samplesPerSecond * frameSize;
            int waveSize = 4;
            int samples = (int)((decimal)samplesPerSecond * 20000 / 1000);
            int dataChunkSize = samples * frameSize;
            int fileSize = waveSize + headerSize + formatChunkSize + headerSize + dataChunkSize;
            // var encoding = new System.Text.UTF8Encoding();
            writer.Write(0x46464952); // = encoding.GetBytes("RIFF")
            writer.Write(fileSize);
            writer.Write(0x45564157); // = encoding.GetBytes("WAVE")
            writer.Write(0x20746D66); // = encoding.GetBytes("fmt ")
            writer.Write(formatChunkSize);
            writer.Write(formatType);
            writer.Write(tracks);
            writer.Write(samplesPerSecond);
            writer.Write(bytesPerSecond);
            writer.Write(frameSize);
            writer.Write(bitsPerSample);
            writer.Write(0x61746164); // = encoding.GetBytes("data")
            writer.Write(dataChunkSize);
            {
                //double theta = frequency * TAU / (double)samplesPerSecond;
                // 'volume' is UInt16 with range 0 thru Uint16.MaxValue ( = 65 535)
                // we need 'amp' to have the range of 0 thru Int16.MaxValue ( = 32 767)
                //double amp = volume >> 2; // so we simply set amp = volume / 2
                for (int step = 0; step < samples; step++)
                {
                    short s = (short)((volume>>2) * Math.Sin((frequency * TAU/(double)samplesPerSecond) * (double)step));
                    writer.Write(s);
                }
            }

            mStrm.Seek(0, SeekOrigin.Begin);
            new System.Media.SoundPlayer(mStrm).Play();
            isPlaying = false;
            writer.Close();
            mStrm.Close();
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
    }
}
