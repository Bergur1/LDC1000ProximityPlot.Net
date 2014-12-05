// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainViewModel.cs" company="OxyPlot">
// Copyright (c) 2014 OxyPlot contributors
// </copyright>
// <summary>
// Represents the view-model for the main window.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace LDC1000ProximityPlot
{
    using OxyPlot;
    using OxyPlot.Series;

    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using System.IO.Ports;
   
    /// <summary>
    /// Represents the view-model for the main window.
    /// </summary>
    public class MainViewModel: INotifyPropertyChanged
    {
        //some of these shouldn't be global...reorganize

        private ICommand _readCommand;
        private readonly Stopwatch watch = new Stopwatch();
        public Timer MainTimer;
        int count = 0;
        long Proximity;

        int dataLength;
        byte[] data;
        int i = 0;
        int j = 0;
        int k = 0;

        LineSeries MainSeries { get; set; }
        public SerialPort evm { get; set; }
        public ICommand ReadCommand
        {
            get
            {
                if (_readCommand == null)
                {
                    _readCommand = new RelayCommand(
                        param => this.ConnectSensor(),
                        param => this.CanRead()
                    );
                }
                return _readCommand;
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

        
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel" /> class.
        /// </summary>
        public MainViewModel()
        {           
            // Create the plot model
            var tmp = new PlotModel { Title = "Proximity" };
            // Create two line series (markers are hidden by default)
            MainSeries = new LineSeries { Title = "Data", MarkerType = MarkerType.Circle };
            // Add the series to the plot model
            tmp.Series.Add(MainSeries);
            // Axes are created automatically if they are not defined
            // Set the Model property, the INotifyPropertyChanged event will make the WPF Plot control update its content
            this.Model = tmp;
        }
        int FFTSampleSize = 1024;
        private void OnTimerElapsed(object state)
        {
            while (start)
            {
                try
                {
                    ProxRetrieveTask();
                    if (Proximity > 0)
                    {
                        lock (this.Model.SyncRoot)
                        {
                            MainSeries.Points.Add(new DataPoint(count, Proximity));
                            count++;
                            if (count > FFTSampleSize)
                            {
                                MainSeries.Points.RemoveAt(0);
                                this.Model.Axes[0].Minimum = MainSeries.Points[0].X;
                            }
                        }

                        this.Model.InvalidatePlot(true);
                    }
                    
                }
                catch (Exception ex)
                { 
                }
            }
        }

        bool start = false;

        /// <summary>
        /// Opens a connection on the serial port andstarts the timer. Timer will need tweaking
        /// </summary>
        public void ConnectSensor()
        {
            start = true;
            evm = new SerialPort("COM12", 9600);
            evm.Open();

            Thread timerThread = new Thread(new ParameterizedThreadStart(OnTimerElapsed));
            timerThread.Start();
            

            //this.MainTimer = new System.Threading.Timer(OnTimerElapsed);

            //long delta = 1;
            //this.watch.Start();
            //this.MainTimer.Change(10000, delta);
        }

        private void ProxRetrieveTask()
        {
            Task<int> task = Task<int>.Factory.StartNew(() =>
            {
                try
                {
                    evm.Write("1");
                    dataLength = evm.BytesToRead;

                    data = new byte[dataLength];
                    while (evm.BytesToRead > 0)
                    {
                        if (i == dataLength) break;
                        data[i] = (byte)evm.ReadByte();
                        i++;
                    }

                    i = 0;

                    if (data.Length >= 7)
                    {
                        byte[] evmReturn = new byte[] { data[2], data[3], data[4], data[5] };

                        string hexFromASCII = System.Text.Encoding.ASCII.GetString(evmReturn);
                        int proximity = int.Parse(hexFromASCII, System.Globalization.NumberStyles.AllowHexSpecifier);
                        return proximity;

                    }
                    return -1;
                }
                catch (Exception ex)
                {
                    return -1;
                }
            });
            Proximity = task.Result;
        }
       
        public bool CanRead()
        {
            return true;
        }


        /// <summary>
        /// Gets the plot model.
        /// </summary>
        public PlotModel Model { get; private set; }
    }
}