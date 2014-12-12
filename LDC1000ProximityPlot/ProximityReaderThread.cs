using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO.Ports;
using System.Collections.Concurrent;

namespace LDC1000ProximityPlot
{
    class ProximityReaderThread
    {
        private Thread t;
        private SerialPort evm;
        private int dataLength;
        private int readByteCounter = 0;
        private byte[] data = new byte[4];
        private int fftBinSize = 1024;

        //stores proximity data
        private float[] dataProx = new float[1024000];

        public double MostRecentProximity()
        {
            return dataProx[DataCounter];
        }

        public ArraySegment<float> SegmentOfProximities(int numSamples)
        {
            try
            {
                return new ArraySegment<float>(dataProx, DataCounter - fftBinSize, fftBinSize);
            }
            catch (Exception ex)
            {
                //return the first little guy instead
                return new ArraySegment<float>(dataProx, 0, fftBinSize);
            }
        }
        public int DataCounter { get; set; }

        private ConcurrentQueue<double> proxQueue;
        public void Dequeue(out double proximity)
        {
            proxQueue.TryDequeue(out proximity);
        }

        public ProximityReaderThread()
        {
            evm = new SerialPort("COM12", 115200); 
            t = new Thread(PollProximity);
            DataCounter = 0;
        }

        public void Start() 
        {
            evm.Open();
            t.Priority = ThreadPriority.AboveNormal;
            t.Start(); 
        }
        public void Stop() 
        {
            evm.Close();
            t.Abort(); 
        }

        // note: this event is fired in the background thread
        //public event EventHandler<DataEventArgs> DataReceived;

        private bool closed = false;
        public void Close() { closed = true; }

        private void PollProximity()
        {
            while (true)
            {
                try
                {
                    evm.Write("1");
                    dataLength = evm.BytesToRead;

                    while (evm.BytesToRead > 0)
                    {
                        if (readByteCounter == dataLength) break;
                        switch (readByteCounter)
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
                    if (DataCounter >= dataProx.Length) DataCounter = 0;
                    dataProx[DataCounter] = (float)int.Parse(hexFromASCII, System.Globalization.NumberStyles.AllowHexSpecifier);
                    DataCounter++;

                    //proxQueue.Enqueue(double.Parse(hexFromASCII, System.Globalization.NumberStyles.AllowHexSpecifier));
                }
                catch (Exception ex)
                {
                    //do nothing...yet
                }
            }
        }
    }
}
