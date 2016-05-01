using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.Runtime.InteropServices;

namespace WpfApplication3
{   /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor _kinect;
        private bool isKeyboardActive = false;
        private int previous = 0;
        private int tplayer = 0;
        bool isWindowsClosing = false;
        const int MaxSkeletonTrackingCount = 6;
        Skeleton[] allSkeletons = new Skeleton[MaxSkeletonTrackingCount];
        private const double ArmRaised = 0.2;
        private const double JumpDiff = 0.03;
        private double leftKeenPrevious = 1.0;
        private double rightKeenPrevious = 1.0;
        public MainWindow()
        {
            InitializeComponent();
        }
        private void startKinect()
        {  
            if (KinectSensor.KinectSensors.Count>0)
            {
                //选择第一个Kinect设备
                _kinect = KinectSensor.KinectSensors[0];
                //启用彩色摄像头，红外线和骨骼跟踪
                _kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                _kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                var parameters = new TransformSmoothParameters
                {
                    Smoothing = 0.5f,
                    Correction = 0.5f,
                    Prediction = 0.5f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                };
                _kinect.SkeletonStream.Enable(parameters);
                _kinect.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(Kinect_SkeletonFrameReady);
                labelTracked.Visibility = System.Windows.Visibility.Visible;
                //注册彩色和深度模型
                _kinect.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(_kinect_ColorFrameReady);
                _kinect.DepthFrameReady += new EventHandler<DepthImageFrameReadyEventArgs>(_kinect_DepthFrameReady);
                _kinect.Start();  
            }
            else
            {
                MessageBox.Show("没有发现设备");
            }
        }
        //通过深度图像事件探测人体
        private void _kinect_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            labelTracked.Visibility = System.Windows.Visibility.Hidden;
            bool isOutFlag = true;
            using (DepthImageFrame DFrame = e.OpenDepthImageFrame())
            {
                
                if (DFrame != null)
                {
                    short[] pixelData = new short[_kinect.DepthStream.FramePixelDataLength];
                    DFrame.CopyPixelDataTo(pixelData);
                    for(int i = 0; i <pixelData.Length; i+= DFrame.BytesPerPixel)
                    {
                        var depth = pixelData[i] >>DepthImageFrame.PlayerIndexBitmaskWidth;
                        var player = pixelData[i] & DepthImageFrame.PlayerIndexBitmask;
                        tplayer = player;
                        if (player > 0)
                        {
                            if (player > 0 && previous != player)
                            {
                                if(previous == 0)
                                {
                                    previous = player;
                                    break;
                                }
                                labelTracked.Visibility = System.Windows.Visibility.Visible;
                                //映射到键盘事件
                                if (!isKeyboardActive)
                                {
                                    mappingKeyboard.Keyboard.Type(Key.C);
                                    isKeyboardActive = true;
                                }
                                isOutFlag = false;
                                break;
                            }
                            previous = player;
                        }                    
                    }                   
                }
            }
            if (isOutFlag && isKeyboardActive && tplayer == 0)
            {
                mappingKeyboard.Keyboard.Type(Key.P);
                isKeyboardActive = false;
            }
        }
        private void Kinect_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            labelTracked.Visibility = System.Windows.Visibility.Hidden;
            if (isWindowsClosing)
            {
                return;
            }
            Skeleton s = GetClosetSkeleton(e);
            if (s == null)
            {
                return;
            }
            if (s.TrackingState != SkeletonTrackingState.Tracked)
            {
                return;
            }
            //提示用户可以玩游戏
            if (s.TrackingState == SkeletonTrackingState.Tracked)
                labelTracked.Visibility = System.Windows.Visibility.Visible;
            //映射到键盘事件
            mappingGesture2Keyboard(s);
        }
        Skeleton GetClosetSkeleton(SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if (skeletonFrameData == null)
                {
                    return null;
                }
                skeletonFrameData.CopySkeletonDataTo(allSkeletons);
                Skeleton closestSkeleton = (from s in allSkeletons
                                            where s.TrackingState == SkeletonTrackingState.Tracked && s.Joints[JointType.Head].TrackingState == JointTrackingState.Tracked
                                            select s).OrderBy(s => s.Joints[JointType.Head].Position.Z).FirstOrDefault();
                return closestSkeleton;
            }

        }
        void mappingGesture2Keyboard(Skeleton s)
        {
            SkeletonPoint leftshoulder = s.Joints[JointType.ShoulderLeft].Position;
            SkeletonPoint rightshoulder = s.Joints[JointType.ShoulderRight].Position;
            SkeletonPoint lefthand = s.Joints[JointType.HandLeft].Position;
            SkeletonPoint righthand = s.Joints[JointType.HandRight].Position;
            SkeletonPoint leftkeen = s.Joints[JointType.KneeLeft].Position;
            SkeletonPoint rightkeen = s.Joints[JointType.KneeRight].Position;
            bool isRightHandRaised = (righthand.Y - rightshoulder.Y) > ArmRaised;
            bool isLeftHandRaised = (lefthand.Y - leftshoulder.Y) > ArmRaised;
            bool isLeftKeenRaised = (leftkeen.Y - leftKeenPrevious) > JumpDiff;
            bool isRightKeenRaised= (rightkeen.Y - rightKeenPrevious) > JumpDiff;
            //原地踏步，触发“W”键前进
            if (isLeftKeenRaised||isRightKeenRaised)
                mappingKeyboard.Keyboard.Type(Key.W);
            leftKeenPrevious = leftkeen.Y;
            rightKeenPrevious = rightkeen.Y;
            //左手举高，触发“S”键后退
            if (isLeftHandRaised)
                mappingKeyboard.Keyboard.Type(Key.S);
            //右手举高，触发“T”键发射炮弹
            if (isRightHandRaised)
                mappingKeyboard.Keyboard.Type(Key.T);
        }
        private void _kinect_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            //启用彩色摄像头
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if(colorFrame == null)
                {
                    return;
                }
                byte[] pixels = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(pixels);
                int stride = colorFrame.Width * 4;
                imageCamera.Source = BitmapSource.Create(colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
            }
        }

        

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            labelTracked.Visibility = System.Windows.Visibility.Hidden;
            startKinect();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _kinect.Stop();
        }
    }
}
