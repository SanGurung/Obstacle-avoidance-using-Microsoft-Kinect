using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Navigation;
using Microsoft.Kinect;
using System.Windows.Forms;
using System.Threading.Tasks;

/*
 *  Obstacle Avoidance and Road Crossing Sys (OARC sys)
 *  Author: Santosh Gurung
 */

namespace OARC_V2
{
    /// <summary>
    /// Interaction logic for OARCsystem.xaml
    /// </summary>
    public partial class OARCsystem : Window
    {
        //Kinect Sensor Variables
        private KinectSensor myKinectSensor;
        private Stream myKinectAudioStream;
        private int vibrationDelayNum = 6;
        private bool vibrationStatus = true;          
        int pixCount;
        int voiceWaitTimer = 7;
        private DepthImagePixel[] kinectDepthPixels;
        private byte[] kinectColorPixels;
        private byte[] colorPixelsRGB;
        private WriteableBitmap colorBitmap;
        private WriteableBitmap colorBitmapRGB;
        public bool kinectHelmetInclinationWarning;
        public bool kinectHelmetZInclinationWarning;
        public int nearRangeValue = 0, farRangeValue = 0;
        string SantoshGurung="Sun-tosh Guu-roong"; //It Santosh Gurung for Speech
        public bool quietMode = false;
        SpeechSynthesizer assistatantAgentSpeaker = new SpeechSynthesizer();
        // Create a new SpeechRecognitionEngine instance.
        SpeechRecognitionEngine srec = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-GB"));

        //Timers to scan the obstacle and recognize commands.
        System.Windows.Threading.DispatcherTimer obstacleDetectTimer = new System.Windows.Threading.DispatcherTimer();
        System.Windows.Threading.DispatcherTimer maintainKinectHelmet = new System.Windows.Threading.DispatcherTimer();
        System.Windows.Threading.DispatcherTimer obstacleMessageAlertTimer = new System.Windows.Threading.DispatcherTimer();

        public OARCsystem()
        {
            InitializeComponent();
            initMyKinect();
            cindySpeechInnit();
            quietMode = false;

            kinectRangeA1.Visibility = Visibility.Visible;
            kinectRangeB1.Visibility = Visibility.Hidden;
            kinectRangeC1.Visibility = Visibility.Hidden;

            //RangeA(100:500), RangeB(100:1100), RangeC(100:1500) 
            kinectRangeA.IsEnabled = true;
            nearRangeValue = 100;
            farRangeValue = 500;

            pixCount = 0;
            assistatantAgentSpeaker = new SpeechSynthesizer();

            //Kinect Helmet position timer
            maintainKinectHelmet.Tick += new EventHandler(getKinectHelmetSafety);
            maintainKinectHelmet.Interval = new TimeSpan(0, 0, 1); // 1 here means 1 Second
            maintainKinectHelmet.Start();

            //Local Timer to detect obstacle in 1 second interval.
            obstacleDetectTimer.Tick += new EventHandler(detectObstacle_counts);
            obstacleDetectTimer.Interval = new TimeSpan(0, 0, 1); // 1 here means 1 Second
            obstacleDetectTimer.Start();

            //Timer to tell when to alert the user again.
            obstacleMessageAlertTimer.Tick += new EventHandler(obstacleMessage_Timer);
            obstacleMessageAlertTimer.Interval = new TimeSpan(0, 0, 1); // 1 here means 1 Second
            obstacleMessageAlertTimer.Start();
        }

        private void initMyKinect()
        {
            kinectHelmetInclinationWarning = false;
            kinectHelmetZInclinationWarning = false;
            foreach (var potentialKinectSensor in KinectSensor.KinectSensors)
            {
                if (potentialKinectSensor.Status == KinectStatus.Connected)
                {
                    myKinectSensor = potentialKinectSensor;
                    break;
                }
            }
            if (this.myKinectSensor != null)
            {
                this.myKinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.myKinectSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                //These variables are for the Depth Image (Left Hand side image)
                this.kinectDepthPixels = new DepthImagePixel[this.myKinectSensor.DepthStream.FramePixelDataLength];
                this.kinectColorPixels = new byte[this.myKinectSensor.ColorStream.FramePixelDataLength * sizeof(int)];
                this.colorBitmap = new WriteableBitmap(this.myKinectSensor.DepthStream.FrameWidth, this.myKinectSensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                kinectDepthImage.Source = this.colorBitmap;

                //These variable are for the RGB Image (Right Hand Side image)
                this.colorPixelsRGB = new byte[this.myKinectSensor.ColorStream.FramePixelDataLength];
                this.colorBitmapRGB = new WriteableBitmap(this.myKinectSensor.ColorStream.FrameWidth, this.myKinectSensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.kinectRGBimage.Source = this.colorBitmapRGB;
            }
            else
            {
                cindy("Sorry, kinect not detected. Please connect the kinect and retry again.", 0,true);
                this.vibrationAlert(false);
            }

            if (this.myKinectSensor != null)
            {
                this.myKinectSensor.Start();
                this.myKinectAudioStream = this.myKinectSensor.AudioSource.Start();
                //Adding events for Depth and Color image, so that the images will appear continuously.
                this.myKinectSensor.DepthFrameReady += this.SensorDepthFrameReady;
                this.myKinectSensor.ColorFrameReady += this.SensorColorFrameReady;
            }
        }

        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    colorFrame.CopyPixelDataTo(this.colorPixelsRGB);
                    //Now we are going to write the pixels into bitmap
                    this.colorBitmapRGB.WritePixels(
                      new Int32Rect(0, 0, this.colorBitmapRGB.PixelWidth, this.colorBitmapRGB.PixelHeight),
                      this.colorPixelsRGB,
                      this.colorBitmapRGB.PixelWidth * sizeof(int),
                      0);
                }
            }   
        }

        //This method produces Depth image and also detects obstacle.
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame aDepthSliceFrame = e.OpenDepthImageFrame())
            {
                if (aDepthSliceFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    aDepthSliceFrame.CopyDepthImagePixelDataTo(this.kinectDepthPixels);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = aDepthSliceFrame.MinDepth;
                    int maxDepth = aDepthSliceFrame.MaxDepth;

                    pixCount= 0;
                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.kinectDepthPixels.Length; ++i)
                    {
                        //Now we are going to get the depth for a pixel.
                        short depth = kinectDepthPixels[i].Depth;

                        //If the pixel depth shows its near to the Kinect it will be counted as obstacle.
                        //However only 1 pixel depth isn't accounted, about 1000 Pixel count is accounted for obstacle
                        //This can be changed however I think its a good number for true obstacle detection.
                        //As few pixel count might give false alarm.

                        //This is the length of the scanning area for obstacle detection
                        //This is where the magic happens
                        if (depth > nearRangeValue && depth < farRangeValue) 
                        {
                            pixCount = pixCount + 1;
                        }
                        
                        byte colourIntensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);
                        this.kinectColorPixels[colorPixelIndex++] = colourIntensity; //For Blue byte
                        this.kinectColorPixels[colorPixelIndex++] = colourIntensity; //For Green byte                    
                        this.kinectColorPixels[colorPixelIndex++] = colourIntensity; //For Red byte
                        ++colorPixelIndex;
                    }
                    if (pixCount < 200) this.voiceWaitTimer = 8;
                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight), this.kinectColorPixels, this.colorBitmap.PixelWidth * sizeof(int), 0);
                }
            }
        }

        private void detectObstacle_counts(object sender, EventArgs e)
        { 
            if (pixCount >1000)
            {
                String msg = "";
                if (this.voiceWaitTimer>4)
                {   
                    this.obstacleDetectLabel.Content = "Obstacle Pixel counts:" + pixCount;
                    if (pixCount > 2300) msg = "Be careful, something ahead. Has large surface.";
                    else msg = "Be careful, something ahead.";
                    cindy(msg,0,false);
                    this.voiceWaitTimer = 0;
                    //vibrationAlert(true); Perfomance issue if vibration is allowed.
                }
            }
            else
            {
                this.obstacleDetectLabel.Content = "No Obstacle in range.";
                pixCount = 0;
            }
        }


        public void getKinectHelmetSafety(object sender, EventArgs e)
        {
            bool alertVibration= false;
            Vector4 accMeterReading = this.myKinectSensor.AccelerometerGetCurrentReading();
            this.kinectAxisX.Content = "X: " + accMeterReading.X;
            this.kinectAxisY.Content = "Y: " + accMeterReading.Y;
            this.kinectAxisZ.Content = "Z: " + accMeterReading.Z;

            //To detect if the Kinect Helmet (Person) is leaning forward or backward or normal.
            if (accMeterReading.Z > 0.70)
            {
                if (!this.kinectHelmetZInclinationWarning)
                {
                    cindy("Please don't lean forward. Be Careful your kinect helmet might fall.",0,false);
                    this.kinectHelmetZInclinationWarning = true;
                    alertVibration = true;
                }
            }
            else if (accMeterReading.Z < -0.50)
            {
                if (!this.kinectHelmetZInclinationWarning)
                {
                    cindy("Please don't lean backwards. You might get back pain",0,false);
                    this.kinectHelmetZInclinationWarning = true;
                    alertVibration = true;
                }
            }
            else 
            {
                this.kinectHelmetZInclinationWarning = false;
            }

            //To detect if the Kinect Helmet(Person) is leaning left, right or straight.
            if (accMeterReading.X > 0.35)
            {
                if (!this.kinectHelmetInclinationWarning)
                {
                    cindy("You are leaning left. Be Careful your kinect helmet might fall.",0,false);
                    this.kinectHelmetInclinationWarning = true;
                    alertVibration = true;
                }
            }
            else if (accMeterReading.X < -0.35)
            {
                if (!this.kinectHelmetInclinationWarning)
                {
                    cindy("You are leaning right. Be Careful your kinect helmet might fall.",0,false);
                    this.kinectHelmetInclinationWarning = true;
                    alertVibration = true;
                }
            }
            else
            {
                this.kinectHelmetInclinationWarning = false;
            }

            if (this.vibrationDelayNum >= 6)
            {
                this.vibrationStaus.Content = "Ready";
            }
            else
            {
                vibrationDelayNum = vibrationDelayNum + 1;
                this.vibrationStaus.Content = "Wait";
            }
            if (alertVibration) this.vibrationAlert(true);
        }

        public async void cindy(String Sentence,int speechRate,bool forceToSpeak)
        {
            try
            {
                if (!quietMode || forceToSpeak)
                {
                    assistatantAgentSpeaker.Dispose();
                    assistatantAgentSpeaker = new SpeechSynthesizer();
                    assistatantAgentSpeaker.Rate = speechRate;
                    assistatantAgentSpeaker.Volume = 100;
                    assistatantAgentSpeaker.SpeakAsync(Sentence);
                    await Task.Delay(5000);
                    //assistatantAgentSpeaker.Rate = 0;
                }
            }
            catch (Exception ex)
            {
                assistatantAgentSpeaker.SpeakAsync("Sorry, cannot recognize your command! " + ex.Message);
            }
        }

        private void vibrationAlertOFF_Click(object sender, RoutedEventArgs e)
        {
            this.vibrationAlert(false);
        }


        private void vibrationAlertON_Click(object sender, RoutedEventArgs e)
        {
            this.vibrationAlert(true);
        }

        private void vibrationAlert(bool polarity)
        {
            try
            {
                if (vibrationStatus)
                {
                    if (this.vibrationStaus.Content.Equals("Ready"))
                    {
                        if (polarity) this.myKinectSensor.ElevationAngle = -27;
                        else this.myKinectSensor.ElevationAngle = -27;
                        this.vibrationStaus.Content = "Wait";
                        this.vibrationDelayNum = 0;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error! " + e.Message);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.vibrationAlert(false);
            assistatantAgentSpeaker.SpeakAsync("Welcome to Oarc System by "+SantoshGurung+". Hi! I am Cindy, here to assist you.");
        }


        private void OARCSYS_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            myKinectSensor.Stop();
        }


        private void currentTimeButton_Click(object sender, RoutedEventArgs e)
        {
            sayDetailTime();
        }

        private void obstacleMessage_Timer(object sender, EventArgs e)
        {
            if (voiceWaitTimer < 7) voiceWaitTimer = voiceWaitTimer + 1;
        }

        private void sayDetailTime()
        {
            cindy("Today is " + DateTime.Now.DayOfWeek.ToString() + 
                  ", " + DateTime.Now.ToString("dd")+" "+DateTime.Now.ToString("MMMM")+" "+
                  DateTime.Now.ToString("yyyy")+". Are you travelling, today?",0,false);
        }

        private void cindySpeechInnit()
        {
            assistatantAgentSpeaker.Volume = 100;
            srec.SetInputToDefaultAudioDevice();
            Choices speechCommand = new Choices();
            speechCommand.Add(new string[] 
                              { "Help me", "the time","Todays Weather","Yes",
                                "Vibration off", "Vibration on","Thank you",
                                "What's your name?", "me a story",
                                "How are you","Be quiet","Dont be so quiet.","No",
                                "Set range a","Set range b","Set range c", "Apple",
                                "Can you sing"});

            GrammarBuilder gbr = new GrammarBuilder();
            gbr.Append(speechCommand);
            Grammar g = new Grammar(gbr);
            srec.LoadGrammar(g);
            // An Event handler for AudioLevelUpdated event.
            srec.AudioLevelUpdated += new EventHandler<System.Speech.Recognition.AudioLevelUpdatedEventArgs>(check_audioLevel);
            srec.SpeechRecognized += new EventHandler<System.Speech.Recognition.SpeechRecognizedEventArgs>(cindyListens_SpeechRecognized);

            // Start recognition.
            srec.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Multiple);
        }

        private void check_audioLevel(object sender, System.Speech.Recognition.AudioLevelUpdatedEventArgs e)
        {
            // This event handler occurs when the audio level is updated or every interval.
            // e.AudioLevel value ranges from 0 to 100
            audioLevelBar.Value = e.AudioLevel;
        }

        private void cindyListens_SpeechRecognized(object sender, System.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            string recogText = e.Result.Text, temp="";
            bool qm = false;
            try
            {
                //if (recogText.Equals("Help me")) cindy("Ok, I am here to assist you.");
                if (recogText.Equals("the time")) sayDetailTime();
                else if (recogText.Equals("No")) cindy("", 0, true); //keeps cindy silent.
                else if (recogText.Equals("Todays Weather")) cindy("Sorry, I can't tell weather information at the moment. I am waiting for upgrade", 0, false);
                else if (recogText.Equals("Vibration off")) vibrationOFF();
                else if (recogText.Equals("Vibration on")) vibrationON();
                else if (recogText.Equals("Thank you")) cindy("You are welcome", 0, false);
                else if (recogText.Equals("me a story"))
                {
                    // Quote by Albert Einstein however the speech doesnt read as its supposed to be.
                    cindy("Sorry, I don't know any story but, I have a quote by L-bert i-stien, " +
                          "'Everything should be made as simple as possible, but not simpler'. ", -2, false);
                }
                else if (recogText.Equals("Set range a"))
                {
                    cindy("Ok, i will only be scanning anything on 0.50 meters range.", 0, false);
                    kinectRangeImageSet(1);
                }
                else if (recogText.Equals("Set range b"))
                {
                    cindy("Ok, i will only be scanning anything on 1.25 meters range.", 0, false);
                    kinectRangeImageSet(2);
                }
                else if (recogText.Equals("Set range c"))
                {
                    cindy("Ok, i will only be scanning anything on 2 meters range.", 0, false);
                    kinectRangeImageSet(3);
                }
                else if (recogText.Equals("How are you"))
                {
                    temp = "I am well, How about you. ";
                    if (quietMode) { temp = temp + "You told me, to be quiet before and "; }
                    if (vibrationStatus) { temp = temp + "you have set vibration on."; }
                    else { temp = temp + "you have set vibration off."; }

                    if (kinectRangeA.IsEnabled) { temp = temp + ". I am scanning any obstacle at 0.50 meter distance."; }
                    else if (kinectRangeB.IsEnabled) { temp = temp + ". I am scanning any obstacle at 1.25 meter distance."; }
                    else { temp = temp + ". I am scanning any obstacle at 1.75 meter distance."; }
                    cindy(temp, -1, false);
                }
                else if (recogText.Equals("Can you sing")) cindy("You know that I can't sing. I'm not Adele but I can try, I heard that you found a girl and you married now ooowo oow ow.", -1, true);
                else if (recogText.Equals("Be quiet"))
                {
                    if (quietMode)
                    {
                        quietMode = false;
                        cindy("Don't get angry, i didn't say anything.", 0, true);
                    }
                    else { cindy("Thank god, I'm going to be quiet. Bye.", 0, true); }
                    quietMode = true;
                }
                else if (recogText.Equals("Dont be so quiet."))
                {
                    quietMode = false;
                    cindy("That's great, now i can talk to you.", 0, true);
                }
                else if (recogText.Equals("Apple")) { cindy("Ha ha ha, i don't have it. Go to Tesco.", 0, false); }
                else if (recogText.Equals("Help me"))
                {
                    qm = quietMode;
                    quietMode = false;
                    rangeScanAhead();
                    quietMode = qm;
                }
                else if (recogText.Equals("What's your name?")) cindy("I am cindy. Part of your OARC System, by " + SantoshGurung, 0, false);
                // else if (recogText.Equals("Yes")) cindy("Great, don't worry. I am here for you.", 0);
                else { /* Do nothing */   }
                this.Title = "Voice: "+ recogText;
            }
            catch (Exception exv)
            {
                MessageBox.Show("Something went wrong:" + exv);
            }
        }

        public async void rangeScanAhead()
        {
            String msg = "";
            int num=1;
            obstacleDetectTimer.Stop();
            cindy("Ok, scanning the area.",0,true);

            if (kinectRangeA.IsEnabled) num = 1;
            else if (kinectRangeA.IsEnabled) num = 2;
            else num = 3;
            //Now scan Range A for obstacle
            kinectRangeImageSet(1);
            if (pixCount > 400) msg = "in 0.50 meters range.";
            
            //Now scan Range B for obstacle
            kinectRangeImageSet(2);
            if(msg.Equals(""))
            {
                await Task.Delay(3000);
                if (pixCount > 1000) msg = "between 0.50 meters and 1.25 meters range.";
            }

            //Now scan Range C for obstacles
            kinectRangeImageSet(3);
            if (msg.Equals(""))
            {
                await Task.Delay(4000);
                if (pixCount > 1000) msg = "between 1.25 meters and 2 meters range.";
            }    

            if (msg.Equals(""))  cindy("Scanning complete.There is nothing in the range.",0,true);
            else cindy("Scanning complete. There is something, " + msg,0,true);
            pixCount = 0;
            kinectRangeImageSet(num);
            await Task.Delay(2000);
            obstacleDetectTimer.Start();
        }

        public void vibrationOFF()
        {
            this.vibrationStatus = false;
            this.vibrationAlertON.IsEnabled = false;
            this.vibrationAlertOFF.IsEnabled = false;
            cindy("Ok, vibration is now off.",0,false);
        }

        public void vibrationON()
        {
            this.vibrationStatus = true;
            this.vibrationAlertON.IsEnabled = true;
            this.vibrationAlertOFF.IsEnabled = true;
            cindy("Ok, vibration is now on.",0,false);
            this.vibrationAlert(true);
        }

        private void kinectRangeA_Checked(object sender, RoutedEventArgs e)
        {
            kinectRangeImageSet(1);
        }

        private void kinectRangeB_Checked(object sender, RoutedEventArgs e)
        {
            kinectRangeImageSet(2);
        }

        private void kinectRangeC_Checked(object sender, RoutedEventArgs e)
        {
            kinectRangeImageSet(3);
        }

        public void kinectRangeImageSet(int x)
        {
            //RangeA(100mm:500mm), RangeB(100mm:1100mm), RangeC(100mm:1500mm) 
            nearRangeValue = 50;
            if(x==1)
            {
                kinectRangeA1.Visibility = Visibility.Visible;
                kinectRangeB1.Visibility = Visibility.Hidden;
                kinectRangeC1.Visibility = Visibility.Hidden;
                kinectRangeA.IsChecked = true;
                farRangeValue = 500;
            }
            else if(x==2)
            {
                kinectRangeA1.Visibility = Visibility.Hidden;
                kinectRangeB1.Visibility = Visibility.Visible;
                kinectRangeC1.Visibility = Visibility.Hidden;
                kinectRangeB.IsChecked = true;
                farRangeValue = 1200;
            }
            else
            {
                kinectRangeA1.Visibility = Visibility.Hidden;
                kinectRangeB1.Visibility = Visibility.Hidden;
                kinectRangeC1.Visibility = Visibility.Visible;
                kinectRangeC.IsChecked = true;
                farRangeValue = 1400;
            }
        }
    }
}