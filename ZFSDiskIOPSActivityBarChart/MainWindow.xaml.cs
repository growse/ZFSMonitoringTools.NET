using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Data;
using System.Windows.Media;

namespace ZFSDiskIOPSActivityBarChart
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly ObservableDictionary<string, int> _readdata = new ObservableDictionary<string, int>();
        private readonly ObservableDictionary<string, int> _writedata = new ObservableDictionary<string, int>();
        public MainWindow()
        {
            InitializeComponent();
            ThreadPool.QueueUserWorkItem(Connect);
            OutputChart.Series.Clear();

            OutputChart.Axes.Clear();

            var readseries = new ColumnSeries { Title = "Read", Background = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0)) };
            var writeseries = new ColumnSeries { Title = "Write", Background = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)) };
            var diskaxis = new CategoryAxis { Orientation = AxisOrientation.X };
            OutputChart.Axes.Add(diskaxis);

            var iopsaxis = new LinearAxis { Minimum = 0, Maximum = 50, Orientation = AxisOrientation.Y, Title = "IOPS", ExtendRangeToOrigin = true };
            OutputChart.Axes.Add(iopsaxis);

            readseries.IndependentValueBinding = new Binding("Key");
            readseries.DependentValueBinding = new Binding("Value");
            readseries.ItemsSource = _readdata;

            writeseries.IndependentValueBinding = new Binding("Key");
            writeseries.DependentValueBinding = new Binding("Value");
            writeseries.ItemsSource = _writedata;
            OutputChart.Series.Add(readseries);
            OutputChart.Series.Add(writeseries);
        }

        private delegate void Updatedatacallback(string disk, int read, int value);
        private void Updatedata(string disk, int read, int write)
        {
            if (Dispatcher.CheckAccess())
            {
                if (_readdata.ContainsKey(disk))
                {
                    _readdata[disk] = read;
                }
                else
                {
                    _readdata.Add(disk, read);
                }

                if (_writedata.ContainsKey(disk))
                {
                    _writedata[disk] = write;
                }
                else
                {
                    _writedata.Add(disk, write);
                }
                foreach (var linearaxis in from Axis axis in OutputChart.Axes where axis.Orientation == AxisOrientation.Y select axis as LinearAxis into linearaxis where linearaxis != null && (read > linearaxis.Maximum || write > linearaxis.Maximum) select linearaxis)
                {
                    linearaxis.Maximum = read > write ? read : write;
                }
            }
            else
            {
                var d = new Updatedatacallback(Updatedata);
                Dispatcher.Invoke(d, new object[] { disk, read, write });
            }
        }

        private void AddData(string line)
        {
            var dataparts = line.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            var disk = dataparts[0];
            var iopsread = int.Parse(dataparts[3]);
            var iopswrite = int.Parse(dataparts[4]);
            Updatedata(disk, iopsread, iopswrite);
        }

        private void Connect(object state)
        {
            var client = new TcpClient("10.13.111.50", 1234);
            var firehose = client.GetStream();

            TextReader tr = new StreamReader(firehose);
            do
            {
                var line = tr.ReadLine();
                if (line == null) { break; }
                if (line.TrimStart().StartsWith("c7"))
                {
                    AddData(line);
                }
            } while (true);
        }
    }
}
