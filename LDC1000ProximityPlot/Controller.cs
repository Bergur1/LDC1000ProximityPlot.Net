using System;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Series;
using Exocortex;

namespace LDC1000ProximityPlot
{
    public class Controller : INotifyPropertyChanged
    {
        ProximityReaderThread pollingThread;
        BackTrackPlayer backingTrack;
        Thread uiThread;
        Exocortex.DSP.ComplexF[] complexData;

        #region constructor stuff
        public PlotModel Model { get; private set; }
        LineSeries MainSeries { get; set; }
        public Controller()
        {
            // Create the plot model
            var tmp = new PlotModel { Title = "Proximity" };
            // Create two line series (markers are hidden by default)
            MainSeries = new LineSeries { MarkerType = MarkerType.None };
            // Add the series to the plot model
            tmp.Series.Add(MainSeries);
            // Axes are created automatically if they are not defined
            // Set the Model property, the INotifyPropertyChanged event will make the WPF Plot control update its content
            this.Model = tmp;
            FFTSampleSize = 1024;
            backingTrack = new BackTrackPlayer();
        }
        #endregion

        private ICommand _readCommand;
        public ICommand ReadCommand
        {
            get
            {
                if (_readCommand == null)
                {
                    _readCommand = new RelayCommand(
                        param => this.StartSensor()
                    );
                }
                return _readCommand;
            }
        }

        private ICommand _executeFFTCommand;
        public ICommand ExecuteFFTCommand
        {
            get
            {
                if (_executeFFTCommand == null)
                {
                    _executeFFTCommand = new RelayCommand(
                        param => this.FFT()
                            );
                }
                return _executeFFTCommand;
            }
        }

        private ICommand _playCommand;
        public ICommand PlayCommand
        {
            get
            {
                if (_playCommand == null)
                {
                    _playCommand = new RelayCommand(
                        param => this.StopSong()
                            );
                }
                return _playCommand;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string property)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(property));
            }
        }

        private string _proxyButtonText = "Start";
        public string ProxyButtonText
        {
            get { return _proxyButtonText; }
            set
            {
                _proxyButtonText = value;
                RaisePropertyChanged("ProxyButtonText");
            }
        }

        bool isStarted = false;
        
        public void StartSensor()
        {
            isStarted = !isStarted;

            if (isStarted)
            {
                pollingThread = new ProximityReaderThread(comPort, FFTSampleSize);
                pollingThread.Start();
                ProxyButtonText = "Stop";

                uiRunning = true;
                uiThread = new Thread(UIThreadLogic);
                uiThread.Start();
            }
            else
            {
                pollingThread.Stop();
                ProxyButtonText = "Start";

                uiRunning = false;
                uiThread.Join();      
            }
        }

        int proxGraphCounter = 0;
        private bool uiRunning = false;
        private void UIThreadLogic()
        {
            while (uiRunning)
            {
                double proximity = pollingThread.MostRecentProximity();
                if (proximity > 0)
                {
                    //update the graph
                    lock (this.Model.SyncRoot)
                    {
                        MainSeries.Points.Add(new DataPoint(proxGraphCounter, proximity));
                        proxGraphCounter++;
                        if (proxGraphCounter > FFTSampleSize)
                        {
                            MainSeries.Points.RemoveAt(0);
                            this.Model.Axes[0].Minimum = MainSeries.Points[0].X;
                            this.Model.Axes[1].AbsoluteMinimum= 10000;
                        }
                    }
                    this.Model.InvalidatePlot(true);                
                }
            }
        }

        public int FFTSampleSize { get; set; }

        private string freq = "";
        public string Freq
        {
            get
            {
                return freq;
            }
            set
            {
                freq = value;
                RaisePropertyChanged("Freq");
            }
        }

        private string comPort = "COM9";
        public string ComPort
        {
            get
            {
                return comPort;
            }
            set
            {
                comPort = value;
                RaisePropertyChanged("ComPort");
            }
        }

        private void StopSong()
        {
            backingTrack.Stop();
        }

        private void FFT()
        {
            try
            {
                
                var vector = pollingThread.SegmentOfProximities(FFTSampleSize);

                complexData = new Exocortex.DSP.ComplexF[FFTSampleSize];
                for (int i = 0; i < vector.Count; ++i)
                {
                    complexData[i].Re = vector.ElementAt(i); // Add your real part here
                    //    complexData[i].Im = 2; // Add your imaginary part here
                }

                Exocortex.DSP.Fourier.FFT(complexData, Exocortex.DSP.FourierDirection.Forward);

                //StreamWriter stream = new StreamWriter("dataRe.txt");
                //StreamWriter stream2 = new StreamWriter("dataIm.txt");
                float max = 0;
                int maxIndex = 0;
                float sampleRate = 1000 * ((float)pollingThread.TimeElapsed / 1024);
                complexData[0].Re = 0;
                for (int i = 0; i < vector.Count; i++)
                {
                    float re = Math.Abs(complexData[i].Re);
                    float im = Math.Abs(complexData[i].Im);
                    if (max < im && i < 513 && i > 20)
                    {
                        max = im;
                        maxIndex = i;
                    }
                    //stream.WriteLine(re);
                    //stream2.WriteLine(im);
                }
                Freq = (maxIndex - 2 * sampleRate / FFTSampleSize).ToString();

                //select song based on frequency
                backingTrack.SelectSong(4);

            }
            catch (Exception ex)
            {
            }

        }
    }
}