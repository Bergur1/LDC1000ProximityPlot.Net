using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Concurrent;
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
        

        public void StartSensor()
        {
            pollingThread = new ProximityReaderThread();
            pollingThread.Start();

            //initializes the specified task
            Thread uiThread = new Thread(UIThreadLogic);
            uiThread.Start();

            //Thread fftThread = new Thread(new Par
        }

        int proxGraphCounter = 0;
        private void UIThreadLogic()
        {
            while (true)
            {
                    double proximity = pollingThread.MostRecentProximity();
                    if (proximity > 24000)
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
                            }
                        }
                        this.Model.InvalidatePlot(true);
                    }
            }
        }

        public int FFTSampleSize { get; set; }

        private void FFT()
        {
            var vector = pollingThread.SegmentOfProximities(FFTSampleSize);
            //probably unnessecary
            int vectorLength = vector.Count;
            Exocortex.DSP.ComplexF[] complexData = new Exocortex.DSP.ComplexF[vectorLength];

            for (int i = 0; i < vectorLength; ++i)
            {
                complexData[i].Re = vector.ElementAt(i); // Add your real part here
                //    complexData[i].Im = 2; // Add your imaginary part here
            }

            Exocortex.DSP.Fourier.FFT(complexData, Exocortex.DSP.FourierDirection.Forward);
        }
    }
}
