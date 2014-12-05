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
           
        int Proximity;
        int dataLength;
        byte[] data = new byte[4];
        int readByteCounter = 0;

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
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel" /> class.
        /// </summary>
        public MainViewModel()
        {           
            // Create the plot model
            var tmp = new PlotModel { Title = "Proximity" };
            // Create two line series (markers are hidden by default)
            MainSeries = new LineSeries { MarkerType = MarkerType.None };
            // Add the series to the plot model
            tmp.Series.Add(MainSeries);
            // Axes are created automatically if they are not defined
            //OxyPlot.Axes.LinearAxis yAxis = new OxyPlot.Axes.LinearAxis();
            ////yAxis.AbsoluteMaximum = 32000;
            //yAxis.Maximum = 32000;
            ////yAxis.AbsoluteMinimum = 1000;
            //yAxis.Minimum = 10000;
            //yAxis.Position = OxyPlot.Axes.AxisPosition.Left;

            //tmp.Axes.Add(yAxis);

            // Set the Model property, the INotifyPropertyChanged event will make the WPF Plot control update its content
            this.Model = tmp;
        }

        int FFTSampleSize = 1024; 
        private void OnTimerElapsed(object state)
        {
            //rotating buffer for moving average
            //todo: delay response by 1 and center the window
            int i = 0;
            int[] avgArray = new int[3];

            //total number of samples
            int count = 0;

            while (start)
            {
                try
                {
                    if (Proximity > 24000)
                    {
                        ///moving average smoothing
                        if (count < 3)
                        {
                            avgArray[count] = Proximity;
                        }
                        else
                        {
                            avgArray[i] = Proximity;
                            Proximity = (avgArray[0] + avgArray[1] + avgArray[2]) / 3;
                            if (i == 2) i = 0;
                            i++;
                        }

                        //update the graph
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
                        ProxRetrieveTask();
                    }
                    
                }
                catch (Exception ex)
                { 
                }
            }
        }

        bool start = false;
        /// <summary>
        /// Opens a connection on the serial port and starts polling. Clicking again closes connection and thread.
        /// </summary>
        public void ConnectSensor()
        {
            start = !start;
            if(evm == null) evm = new SerialPort("COM12", 115200);
            

            Thread timerThread = new Thread(new ParameterizedThreadStart(OnTimerElapsed));

            if (start)
            {
                ProxyButtonText = "Stop";
                evm.Open();
                timerThread.Start();
            }
            else
            {
                ProxyButtonText = "Start";
                evm.Close();
                timerThread.Abort();
            }
        }

        private void ProxRetrieveTask()
        {
            Task<int> task = Task<int>.Factory.StartNew(() =>
            {
                try
                {
                    evm.Write("1");
                    dataLength = evm.BytesToRead;

                    while (evm.BytesToRead > 0)
                    {
                        if (readByteCounter == dataLength) break;
                        switch(readByteCounter)
                        {
                            case 2:
                                data[0] = (byte)evm.ReadByte();
                                break;
                            case 3:
                                data[1] = (byte)evm.ReadByte();
                                break;
                            case 4:
                                data[2] = (byte)evm.ReadByte();
                                break;
                            case 5:
                                data[3] = (byte)evm.ReadByte();
                                break;
                            default:
                                evm.ReadByte(); 
                                break;
                        }
                        readByteCounter++;
                    }
                    readByteCounter = 0;

                    string hexFromASCII = System.Text.Encoding.ASCII.GetString(data);
                    int proximity = int.Parse(hexFromASCII, System.Globalization.NumberStyles.AllowHexSpecifier);
                    return proximity;
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