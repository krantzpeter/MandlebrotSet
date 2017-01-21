using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.ComponentModel;
using System.Threading;
using FloatType = System.Double;

namespace Mandlebrot_Set
{
    public class ThreadInfo
    {
        //public bool threadFinished = false;
        //public CancellationTokenSource cts = null;
        public int mainBmpWidth;    // the width and height of the main bitmap in pixels
        public int mainBmpHeight;
        public int startRow;       // the pixel row number (first row = 0) of the first row in the main image to calc
        public int endRow;         // the row number of last row to calc
        public FloatType minX;        // FloatType precision min X value of scaled bounds of the main Bmp image 
        public FloatType maxX;        // FloatType precision max X value of scaled bounds of the main Bmp image 
        public FloatType minY;        // FloatType precision min Y value of scaled bounds of the main Bmp image
        public FloatType maxY;        // FloatType precision max Y value of scaled bounds of the main Bmp image
        public Bitmap bmpSection = null; // working bmp with width mainBmpWidth and height (endRow – startRow + 1)

        
        public BackgroundWorker bwThread;


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
