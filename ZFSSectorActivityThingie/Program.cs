using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZFSSectorActivityThingie
{
    class Program
    {
        private const string hostname = "10.13.111.50";
        private const int port = 1234;

        static WriteableBitmap writeableBitmap;
        static Window w;
        static System.Windows.Controls.Image i;

        const Int64 disksize = 1000000000000; //1TB
        const int blocksize = 1024;

        static Stream firehose;
        static int imagexsize, imageysize;
        static int disksizex, disksizey;
        static int pixelsperdisk;
        static Int64 blocksperdisk, blocksperpixel;
        static Font disklabelfont;
        private const int xboxes = 6;
        private const int yboxes = 5;


        [STAThread]
        static void Main()
        {
            i = new System.Windows.Controls.Image();
            //RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.NearestNeighbor);
            //RenderOptions.SetEdgeMode(i, EdgeMode.Aliased);

            disklabelfont = new Font("Arial", 12f);

            w = new Window { Content = i };

            i.Width = 1000;
            i.Height = 800;
            w.SizeToContent = SizeToContent.WidthAndHeight;
            w.Show();
            imagexsize = (int)i.Width;
            imageysize = (int)i.Height;

            writeableBitmap = new WriteableBitmap(
                imagexsize,
                imageysize,
                96,
                96,
                PixelFormats.Bgr32,
                null);

            i.Source = writeableBitmap;
            i.Stretch = Stretch.None;
            i.HorizontalAlignment = HorizontalAlignment.Left;
            i.VerticalAlignment = VerticalAlignment.Top;

            disksizex = Convert.ToInt32(imagexsize / xboxes);
            disksizey = Convert.ToInt32(imageysize / yboxes);
            pixelsperdisk = disksizex * disksizey;
            blocksperdisk = disksize / blocksize;
            blocksperpixel = blocksperdisk / pixelsperdisk;

            DrawGrid();
            ThreadPool.QueueUserWorkItem(new WaitCallback(Listen));
            var app = new Application();
            app.Run();
        }

        private static void DrawGrid()
        {
            writeableBitmap.Lock();
            var b = new Bitmap(
                (int)writeableBitmap.Width,
                (int)writeableBitmap.Height,
                writeableBitmap.BackBufferStride,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb,
                writeableBitmap.BackBuffer);

            Graphics g = Graphics.FromImage(b);

            for (int ii = 1; ii < xboxes; ii++)
            {
                g.DrawLine(new System.Drawing.Pen(System.Drawing.Color.White), disksizex * ii, 0, disksizex * ii, imageysize);
            }
            for (int ii = 1; ii < yboxes; ii++)
            {
                g.DrawLine(new System.Drawing.Pen(System.Drawing.Color.White), 0, disksizey * ii, imagexsize, disksizey * ii);
            }

            int counter = 64;
            for (int yy = 0; yy < yboxes; yy++)
            {
                for (int xx = 0; xx < xboxes; xx++)
                {
                    float stringheight = g.MeasureString(counter.ToString(), disklabelfont).Height;
                    g.DrawString(
                        counter.ToString(),
                        disklabelfont,
                        new SolidBrush(System.Drawing.Color.White),
                        new PointF(
                            (xx) * Convert.ToInt32(disksizex),
                            ((yy + 1) * Convert.ToInt32(disksizey) - stringheight)
                            )
                            );
                    counter += 64;
                }
            }

            g.Dispose();
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, b.Width, b.Height));
            writeableBitmap.Unlock();

        }

        private static void Listen(object o)
        {
            var pixellist = new List<Pixel>();
            var client = new TcpClient(hostname, port);
            firehose = client.GetStream();

            TextReader tr = new StreamReader(firehose);
            const int timeperframe = 1000;
            Int64 frametime = 0;


            do
            {
                string line = tr.ReadLine();
                Console.WriteLine(line);
                if (line == null) { break; }
                if (!line.Contains(",")) continue;
                var parts = line.Split(",".ToCharArray());
                decimal xoffset = 0;
                decimal yoffset = 0;
                var print = true;
                //Disks are multiples of 64 starting at 128.
                var disknumber = (int.Parse(parts[1]) / 64) - 1;

                //Work out where to start drawing based on what disk we're looking at
                xoffset = (disknumber % xboxes) * disksizex;
                yoffset = Math.Floor((decimal)disknumber / xboxes) * disksizey;

                //Curtime is the timestamp of the current row.
                var curtime = Int64.Parse(parts[0]);


                if (curtime - frametime > timeperframe)
                {
                    //We've got enough data for this frame, draw it!
                    Console.WriteLine(string.Format("Pixelbuffer size: {0}", pixellist.Count));
                    UpdateBitmap(pixellist);
                    frametime = curtime;
                }


                //We're ready to draw a dot. Lets go do that.
                int color = 0;
                color |= (parts[3] == "0") ? 255 << 8 : 255 << 16;
                int block = int.Parse(parts[2]);
                decimal xd, yd;
                decimal pixnum = Math.Floor((decimal)block / blocksperpixel);
                yd = Math.Floor(pixnum / disksizey);
                xd = ((pixnum / disksizey) - yd) * disksizex;
                xd += xoffset;
                yd += yoffset;

                var x = Convert.ToInt32(xd);
                var y = Convert.ToInt32(yd);

                pixellist.Add(new Pixel { x = x, y = y, color = color });
            } while (true);
        }

        private delegate void UpdateBitmapCallback(List<Pixel> pixellist);
        private static void UpdateBitmap(List<Pixel> pixellist)
        {
            if (writeableBitmap.Dispatcher.Thread != Thread.CurrentThread)
            {
                var d = new UpdateBitmapCallback(UpdateBitmap);
                writeableBitmap.Dispatcher.Invoke(d, new object[] { pixellist });
            }
            else
            {
                int minx = 0, miny = 0, maxx = 0, maxy = 0;

                lock (pixellist)
                {
                    foreach (var pixel in pixellist)
                    {
                        if (pixel.x > maxx) { maxx = pixel.x; }
                        if (pixel.y > maxy) { maxy = pixel.y; }
                        if (pixel.x < minx) { minx = pixel.x; }
                        if (pixel.y < miny) { miny = pixel.y; }
                        unsafe //Woah, Captain
                        {
                            //Reserve the back buffer
                            writeableBitmap.Lock();

                            //Grab pointer to the back buffer
                            var bbPtr = (int)writeableBitmap.BackBuffer;

                            //Figure out where to actually draw the thing. 4bytes per pixel
                            bbPtr += pixel.y * writeableBitmap.BackBufferStride;
                            bbPtr += pixel.x * 4;

                            //Colour it in
                            *((int*)bbPtr) = pixel.color;

                            //tell the world which bit changed.
                            writeableBitmap.AddDirtyRect(new Int32Rect(pixel.x, pixel.y, 1, 1));

                            //Let it be changed
                            writeableBitmap.Unlock();

                        }
                    }
                    pixellist.Clear();
                }
            }
        }
    }

    public struct Pixel
    {
        public int x;
        public int y;
        public int color;
    }
}

