using System;
using System.Windows;
using System.Threading;
using System.IO.Ports;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace LDC1000ProximityPlot
{
    class ProximityReaderThread
    {
        private Thread t;
        private SerialPort evm;
        private int dataLength;
        private int readByteCounter = 0;
        private byte[] data = new byte[4];
        public int fftBinSize;
        private bool running = false;

        public string ComPort { get; set; }

        public long TimeElapsed { get; set; }

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

        public ProximityReaderThread(string comPort, int binSize)
        {
            evm = new SerialPort(comPort, 115200); 
            t = new Thread(PollProximity);
            DataCounter = 0;
            fftBinSize = binSize;
        }

        public void Start() 
        {
            try
            {
                evm.Open();
                t.Priority = ThreadPriority.AboveNormal;
                t.Start();
                running = true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public void Stop() 
        {
            try
            {
                running = false;
                evm.Close();
                t.Join();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private bool closed = false;
        public void Close() { closed = true; }

        

        private void PollProximity()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (running)
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
                    if (DataCounter % fftBinSize == 0)
                    {
                        TimeElapsed = stopwatch.ElapsedMilliseconds;
                        stopwatch.Reset();
                    }
                    
                    //maybe change to using int instead
                    dataProx[DataCounter] = (float)int.Parse(hexFromASCII, System.Globalization.NumberStyles.AllowHexSpecifier);
                    DataCounter++;
                    if (DataCounter == dataProx.Length -1)
                    {
                        DataCounter = 0;
                    }

                    

                    //proxQueue.Enqueue(double.Parse(hexFromASCII, System.Globalization.NumberStyles.AllowHexSpecifier));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}
