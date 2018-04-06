using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using Microsoft.Kinect;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Windows.Forms;

namespace OARC_V2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            checkKinect();
        }

        public void checkKinect()
        {
            KinectSensor myKinectSensor = null;
            SpeechSynthesizer assistatantAgentSpeaker = new SpeechSynthesizer();
            foreach (var potentialKinectSensor in KinectSensor.KinectSensors)
            {
                if (potentialKinectSensor.Status == KinectStatus.Connected)
                {
                    myKinectSensor = potentialKinectSensor;
                    break;
                }
            }
            if (myKinectSensor != null)
            {
                myKinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                myKinectSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                myKinectSensor.SkeletonStream.Enable();
                //myKinectSensor.ColorStream.Enable(ColorImageFormat.InfraredResolution640x480Fps30);
                OARCsystem osm = new OARCsystem();
                this.Close();
                osm.Show();
            }
            else
            {
                this.Show();
                assistatantAgentSpeaker.Dispose();
                assistatantAgentSpeaker = new SpeechSynthesizer();
                assistatantAgentSpeaker.Volume = 100;
                assistatantAgentSpeaker.Speak("Sorry, kinect not detected. Please connect the kinect and retry again.");
                Application.Current.Shutdown();
            }
        }
    }
}
