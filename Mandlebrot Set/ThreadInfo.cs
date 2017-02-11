using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.ComponentModel;
using System.Threading;
//using FloatType = System.Quadruple;
using FloatType = System.Double;

namespace Mandlebrot_Set
{
    public class ThreadInfo
    {
        //public bool threadFinished = false;
        //public CancellationTokenSource cts = null;

        // the pixel row number (first row = 0) of the first row in the main image to calc
        public int startRow
            { get; set; }
        
        // the row number of last row to calc
        public int endRow
            { get; set; }

        // the full image that we're trying to calculate via threads.
        public MandlebrotImage fullMandImage;
        public Bitmap bmpSection = null; // working bmp with width mainBmpWidth and height (endRow – startRow + 1)

        // The background worker thread
        public BackgroundWorker bwThread
            { get; set; }


        public ThreadInfo()
        {
            //cts = new CancellationTokenSource();
            bwThread = new BackgroundWorker();
        }

        ~ThreadInfo()
        {
            if (bmpSection != null)
            {
                bmpSection.Dispose();
                bmpSection = null;
            }

            //if (cts != null)
            //{
            //    cts.Dispose();
            //    cts = null;
            //}

        }
    }
}
