using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
//using FloatType = System.Quadruple;
using FloatType = System.Double;


namespace Mandlebrot_Set
{
    public class MandlebrotImage
    {
        // Current scaled bounds of the current image.
        public myRect mandRect
            { get; set; }
        public Bitmap mainBmp
            { get; set; }

        private bool m_bitmapCalculated;
        public bool bitmapCalculated
            { get { return m_bitmapCalculated; } }

        // Set to the form which should be updated when the image is calculated
        public Form1 formToUpdate
            { get; set; } = null;

        public static Color BackColor
            { get; set; }

        private static readonly object _locker = new object();
        private static readonly int maxThreads = 16;
        private static ThreadInfo[] threadInfoArray = null;
        private static int numberActiveBackGroundThreads = 0; // To count number of active threads.
        // private Boolean threadsActive = false;
        private static bool imageCalcInProgress = false;
        public static readonly int bitmapWidth = 1800 / 2; // Size of the bitmap.
        //const int bitmapWidth = 1040; // Size of the bitmap.
        public static readonly int bitmapHeight = 1040 / 2;
        //private const int bitmapWidth = 81;  // Size of the bitmap.
        //private const int bitmapHeight = 81;
        private static readonly int pixelsToCalcPerThread = 50000;
        private static readonly int rowsToCalcPerThread = pixelsToCalcPerThread / bitmapWidth;
        private static int lastImageRowCalcRequested = -1;  // Counts the number of rows in the main bitmap have been calculated by currently active threads.

        // BackgroundWorker variables to assist with multiple threads for time consuming calculations.
        //private BackgroundWorker[] threadArray = new BackgroundWorker[maxThreads];


        private static readonly FloatType baseval;
        private static readonly FloatType breakoutval;
        private static readonly FloatType aspectRatio;

        private static readonly FloatType minXBound; // Outer bounds of the Mandelbrot set.
        private static readonly FloatType maxXBound;
        private static readonly FloatType minYBound;
        private static readonly FloatType maxYBound;

        //private const int maxColorIndex = 768;
        private static readonly int maxColorIndex = 768;
        private static Color[] colors;
        private static bool bHighPrecisionOn = false;


        // Increment values for each pixel in the image.
        //private FloatType XInc = (FloatType)(maxXBound - minXBound) / ((FloatType)bitmapWidth - 1);
        //private FloatType YInc = (FloatType)(maxYBound - minYBound) / ((FloatType)bitmapHeight - 1);
        private FloatType XInc;
        private FloatType YInc;

        // Static constructor to initialise base values
        static MandlebrotImage()
        {
            baseval = 2.0;
            breakoutval = baseval * baseval;
            aspectRatio = (FloatType)bitmapWidth / (FloatType)bitmapHeight;

            minXBound = -baseval * aspectRatio; // Outer bounds of the Mandelbrot set.
            maxXBound = baseval * aspectRatio;
            minYBound = baseval;
            maxYBound = -baseval;

            //pictureBox1.Image = (Image)mainBmp;
            setupColors();
            InitializeThreadInfoArray();

        }

        // Constructor
        public MandlebrotImage()
        {
            bitmapCalculated = false;
            mainBmp = new Bitmap(bitmapWidth, bitmapHeight);

        }

        // Destructor
        ~MandlebrotImage()
        {
            if (mainBmp != null)
            {
                mainBmp.Dispose();
                mainBmp = null;
            }
        }

        private static void setupColors()
        {
            colors = new Color[maxColorIndex + 1];
#if !stdColors
            int r;
            int g;
            int b;
            int colorBase = 0x000000;
            int colorMult = 50;
#endif
            for (int enumValue = 0; enumValue < colors.Length; enumValue++)
            {
#if stdColors
                colors[enumValue] = Color.FromKnownColor((KnownColor)(enumValue % 139 + 28));
#else
                r = ((enumValue & 0x1c0) >> 6 + colorBase) * colorMult % 256;
                g = ((enumValue & 0x38) >> 3 + colorBase) * colorMult % 256;
                b = ((enumValue & 0x7) + colorBase) * colorMult % 256;
                colors[enumValue] = Color.FromArgb(255, r, g, b);
#endif
                //colors[enumValue] = Color.FromArgb((enumValue >= 256 ? 0 : enumValue), (enumValue >= 256 && enumValue < 512 ? enumValue % 256 : 0), (enumValue >= 512 ? enumValue % 256 : 0));
            }

            //colors[0] = Color.Red;
            //colors[1] = Color.Green;
            //colors[2] = Color.Blue;
            //colors[255] = Color.Blue;
        }

        // Set up the threadInfo and objects and backgroundWorkders by 
        // attaching event handlers. 
        private static void InitializeThreadInfoArray()
        {
            threadInfoArray = new ThreadInfo[maxThreads];

            for (int f = 0; f < maxThreads; f++)
            {
                threadInfoArray[f] = new ThreadInfo();
                threadInfoArray[f].bwThread = new BackgroundWorker();
                threadInfoArray[f].bwThread.DoWork +=
                    new DoWorkEventHandler(backgroundWorkerCalcs_DoWork);
                threadInfoArray[f].bwThread.RunWorkerCompleted +=
                    new RunWorkerCompletedEventHandler(backgroundWorkerCalcs_RunWorkerCompleted);
                // threadInfoArray[f].bwThread.ProgressChanged +=
                //    new ProgressChangedEventHandler(backgroundWorkerCalcs_ProgressChanged);
                threadInfoArray[f].bwThread.WorkerReportsProgress = false;
                threadInfoArray[f].bwThread.WorkerSupportsCancellation = true;

                threadInfoArray[f].bmpSection = new Bitmap(bitmapWidth, rowsToCalcPerThread);
            }
            // Array.Sort(threadInfoArray, (el1, el2) => Compare(el1.bwThread, el2.bwThread));
        }

        //When any thread finishes, the backgroundWorkerCalcs_RunWorkerCompleted function 
        //    •	Copies the updated bitmap to the main bitmap and disposes of the section bmp and invalidate that section of the main bmp image so it is repainted.
        //    •	checks the noImageRowsCalcRequested value to see if all rows have been issued for calculation.  
        //    •	If not all issued for calc, calls AddImageCalcThread to reactivate this thread for the next section.
        //    •	If all issued for calc,  if this is the last thread running (check activeThreadCount) remove wait mousepointer and reset noRowsCalcd to 0.  If not the last active thread running then simply return so that the next finishing thread can do the finalisation work.

        private static void backgroundWorkerCalcs_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Grab lock to make sure that no other thread updates the main bitmap (CopyBitmapSectionToFormBitmap()) or any other shared fields (e.g numberActiveBackGroundThreads) whilst this thread is updating.
            lock (_locker)
            {
                // First, handle the case where an exception was thrown.
                if (e.Error != null)
                {
                    MessageBox.Show(e.Error.Message);
                }

                // decrement the count of active threads
                numberActiveBackGroundThreads--;

                // Get the BackgroundWorker that raised this event.
                BackgroundWorker worker = sender as BackgroundWorker;

                if (e.Cancelled)
                {
                    // Thread has been cancelled so exit without further updates.
                    return;
                }
                
                // Find which threadInfo element this is so that we can reinstatiate this thread with the next section of the main bmp.
                ThreadInfo ti = null;
                ti = Array.Find(threadInfoArray, element => element.bwThread == worker);
                //for (int i = 0; i < maxThreads; i++)
                //{
                //    if (threadInfoArray[i].bwThread == worker)
                //    {
                //        ti = threadInfoArray[i];
                //        break;
                //    }
                //}

#if ShowPaintThreadProgress
                Debug.WriteLine("backgroundWorkerCalcs_RunWorkerCompleted() Thread ID " + Thread.CurrentThread.ManagedThreadId + " finished calculating rows " + ti.startRow + ", " + ti.endRow);
#endif
                if (ti != null)
                {
                    // Copy the update image from this sectionBmp to the main bmp.
                    int bmpSectionHeight = ti.endRow - ti.startRow + 1;
                    Rectangle updateRect = new Rectangle(0, ti.startRow, bitmapWidth, bmpSectionHeight);
                    //updateRect.X = Math.Max((this.ClientRectangle.Width - mainBmp.Width) / 2, 0);
                    //CopyBitmapSectionToFormBitmap(ti.bmpSection, updateRect);


                    // Copy the update image from this sectionBmp to the main bmp.
                    Graphics mainBmpGraphics = Graphics.FromImage(ti.fullMandImage.mainBmp);
                    //updateRect.Offset(mainBmpLocationInForm);
                    mainBmpGraphics.DrawImage(ti.bmpSection, updateRect, new Rectangle(0, 0, ti.bmpSection.Width, ti.bmpSection.Height), GraphicsUnit.Pixel);

                    // Cleanup
                    mainBmpGraphics.Dispose();


                    // Change coords to the correct psn in the form (vs the bitmap) so it can be invalidated.
                    Form1 mainForm = ti.fullMandImage.formToUpdate;
                    if (mainForm != null)
                    {
                        //  There is a form to update so do it.
                        updateRect.Offset(mainForm.mainBmpLocationInForm);
                        mainForm.Invalidate(updateRect);
                    }


                    //Application.DoEvents();
#if ShowPaintThreadProgress
                    Debug.WriteLine("backgroundWorkerCalcs_RunWorkerCompleted() Thread ID " + Thread.CurrentThread.ManagedThreadId + " invalidated Rect: " + updateRect);
#endif

                    if (imageCalcInProgress == false || lastImageRowCalcRequested + 1 >= bitmapHeight)
                    {
                        // We've finished requesting calculation of any further elements.
                        if (numberActiveBackGroundThreads <= 0)
                        {
                            // The final running thread has just ended so we're finished calculating.
                            imageCalcInProgress = false;
                            ti.fullMandImage.m_bitmapCalculated = true;
                            lastImageRowCalcRequested = -1;
                            mainForm.UseWaitCursor = false;
                            mainForm.Cursor = Cursors.Default;
                            mainForm.pushBitmap();
                        }
                    }
                    else
                    {
                        // We've not finished calculation so add another thread.
                        SetAndActivateThreadToCalcNextImageSection(ti);
                    }
                }
            }

        }

        /// <summary>
        /// Calculates the Mandlebrot "escape value" for a given complex number.  Note that for a graph of the Mandlebrot
        /// set, the real and imaginary portions of the complex number are generally used as the X and Y values
        /// for the graph and the point graphed is shown as the escape value with each distinct value generally shown as a 
        /// different colour.
        /// </summary>
        /// <param name="c">The Complex number for which the escape value is to be calculated.</param>
        /// <returns>The Mandlebrot escape value for the given complex number.  This is an integer count of the number of 
        /// recursive calculations completed prior to a breakout value being generated or until a maximum avlue is reached.</returns>
        //private int calcMandlebrotEscapeVal(Complex c)
        //{
        //    FloatType Z;
        //    FloatType dblTmp;
        //    Complex c1;
        //    Complex lastpassval;

        //    Z = c.Real * c.Real + c.Imaginary * c.Imaginary;

        //    if (Z >= breakoutval)
        //        // We've broken out in first pass so exit returning pass no.
        //        return 0;

        //    lastpassval = c;

        //    // Create new point in form (x^2-y^2, 2xy) + c
        //    dblTmp = c.Real * c.Imaginary;
        //    c1 = new Complex(c.Real * c.Real - c.Imaginary * c.Imaginary, dblTmp + dblTmp) + c;

        //    for (int i = 1; i <= maxColorIndex; i++)
        //    {
        //         if (lastpassval == c1)
        //            // Value has not changed on this pass so it will never change again so escape and return max value.
        //            return maxColorIndex;

        //        Z = c1.Real * c1.Real + c1.Imaginary * c1.Imaginary;

        //        if (Z >= breakoutval)
        //            // We've broken out so exit returning pass no.
        //            return i;

        //        lastpassval = c1;
        //        // Create new point in form (x^2-y^2, 2xy) + c
        //        dblTmp = c1.Real * c1.Imaginary;
        //        c1 = new Complex(c1.Real * c1.Real - c1.Imaginary * c1.Imaginary, dblTmp + dblTmp) + c;
        //    }

        //    // We didn't break out so return max value.
        //    return maxColorIndex;
        //}


        /// <summary>
        /// Calculates the Mandlebrot "escape value" for a given complex number.  Note that for a graph of the Mandlebrot
        /// set, the real and imaginary portions of the complex number are generally used as the X and Y values
        /// for the graph and the point graphed is shown as the escape value with each distinct value generally shown as a 
        /// different colour.
        /// </summary>
        /// <param name="cr">The real part of the Complex number for which the escape value is to be calculated.</param>
        /// <returns>The Mandlebrot escape value for the given complex number.  This is an integer count of the number of 
        /// <param name="cr">The real part of the complex number for which the escape value is to be calculated.</param>
        /// <param name="ci">The imaginary part of the complex number for which the escape value is to be calculated.</param>
        /// recursive calculations completed prior to a breakout value being generated or until a maximum avlue is reached.</returns>
        private static int calcMandlebrotEscapeVal(Quadruple cr, Quadruple ci)
        {
            Quadruple zr, zi;
            Quadruple zrsqr, zisqr;
            Quadruple lastpassZr, lastpassZi;

            zr = 0;
            zi = 0;
            zrsqr = 0;
            zisqr = 0;
            lastpassZi = 99999999;
            lastpassZr = 99999999;
            for (int i = 0; i <= maxColorIndex; i++)
            {
                if (zrsqr + zisqr > breakoutval)
                    // We've broken out so exit returning pass no.
                    return i;

                if (lastpassZr == zr && lastpassZi == zi)
                    // Value has not changed on this pass so it will never change again so escape and return max value.
                    return maxColorIndex;

                lastpassZi = zi;
                lastpassZr = zr;

                zi = zr * zi;
                zi += zi; // Multiply by two
                zi += ci;
                zr = zrsqr - zisqr + cr;
                zrsqr = zr * zr;
                zisqr = zi * zi;
            }

            // We didn't break out so return max value.
            return maxColorIndex;
        }


        /// <summary>
        /// Calculates the Mandlebrot "escape value" for a given complex number.  Note that for a graph of the Mandlebrot
        /// set, the real and imaginary portions of the complex number are generally used as the X and Y values
        /// for the graph and the point graphed is shown as the escape value with each distinct value generally shown as a 
        /// different colour.
        /// </summary>
        /// <param name="cr">The real part of the Complex number for which the escape value is to be calculated.</param>
        /// <returns>The Mandlebrot escape value for the given complex number.  This is an integer count of the number of 
        /// <param name="cr">The real part of the complex number for which the escape value is to be calculated.</param>
        /// <param name="ci">The imaginary part of the complex number for which the escape value is to be calculated.</param>
        /// recursive calculations completed prior to a breakout value being generated or until a maximum avlue is reached.</returns>
        private static int calcMandlebrotEscapeVal(double cr, double ci)
        {
            double zr, zi;
            double zrsqr, zisqr;
            double lastpassZr, lastpassZi;

            zr = 0;
            zi = 0;
            zrsqr = 0;
            zisqr = 0;
            lastpassZi = 99999999;
            lastpassZr = 99999999;
            for (int i = 0; i <= maxColorIndex; i++)
            {
                if (zrsqr + zisqr > breakoutval)
                    // We've broken out so exit returning pass no.
                    return i;

                if (lastpassZr == zr && lastpassZi == zi)
                    // Value has not changed on this pass so it will never change again so escape and return max value.
                    return maxColorIndex;

                lastpassZi = zi;
                lastpassZr = zr;

                zi = zr * zi;
                zi += zi; // Multiply by two
                zi += ci;
                zr = zrsqr - zisqr + cr;
                zrsqr = zr * zr;
                zisqr = zi * zi;
            }

            // We didn't break out so return max value.
            return maxColorIndex;
        }


        /// <summary>
        /// Spawns a series of threads up to maxThreads, each of which will calculate the points in successive sections of the image, defined by a start and stop row in threadInfo and
        ///  Form1.mandRect.X, mandRect.Right, mandRect.Y, mandRect.Bottom, xInc and yInc which define the mandelbrot value range that maps to the overall bitmap to be generated.
        ///
        /// Checks that noRowsCalcd is 0 and if it’s not, issue error msg that we tried to recalc whilst a recalc was already underway and return.
        /// If it is zero, for 0 to maxThreads – 1, call AddImageCalcThread for the relevant threadInfo element.
        /// 
        /// </summary>
        private void CreateMandelbrotImageThreads(FloatType myMinX, FloatType myMaxX, FloatType myMinY, FloatType myMaxY)
        {
            // Update the boundng values defining the overall image for the current Mandlebrot "zoom level"
            mandRect.X = myMinX;
            mandRect.Right = myMaxX;
            mandRect.Y = myMinY;
            mandRect.Bottom = myMaxY;
            XInc = (FloatType)(mandRect.Width) / (mainBmp.Width);
            YInc = (FloatType)(mandRect.Height) / (mainBmp.Height);

            // Queue the task.
            //    threadsActive = true;
            //    ThreadInfo ti = new ThreadInfo();
            //    ti.bmp = (Bitmap)mainBmp.Clone();
            //    tiArrayList.Add(ti);

            //    ThreadPool.QueueUserWorkItem(new WaitCallback(createMandlebrotImage), ti);

            if (numberActiveBackGroundThreads != 0)
            {
                MessageBox.Show("Error - cannot complete calculations as " + numberActiveBackGroundThreads + " caculcation threads already underway.", "Calculation Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }


            // Grab lock to make sure that no other thread updates the imageCalcInProgress etc.
            lock (_locker)
            {
                imageCalcInProgress = true;

                // Divide number of rows of bitmap to calc into chunks of rowsToCalcPerThread

                for (int threadNum = 0; threadNum < maxThreads; threadNum++)
                {
                    if (threadInfoArray[threadNum].bwThread.IsBusy)
                    {
                        // This thread is not available
                        throw new Exception("ERROR - numberActiveBackGroundThreads = 0 but thread " + threadNum + " is active.");
                    }
                    else
                    {
                        // Thread is available so set it up and start it.
                        ThreadInfo ti = threadInfoArray[threadNum];
                        ti.fullMandImage = this;
                        SetAndActivateThreadToCalcNextImageSection(ti);
                    }
                }
            }

        }

        private static void SetAndActivateThreadToCalcNextImageSection(ThreadInfo ti)
        {
            // Grab lock to make sure that no other thread updates the lastImageRowCalcRequested or numberActiveBackGroundThreads.
            lock (_locker)
            {
                ti.startRow = lastImageRowCalcRequested + 1;

                if (ti.startRow >= bitmapHeight)
                {
                    // We've finished calculating the image so exit.
                    return;
                    //throw new Exception("ERROR - AddImageCalcThread() requested to calc rows beyond end of image.  Start row " + ti.startRow + " whereas image length " + ti.mainBmpHeight + ".");
                }

                ti.endRow = Math.Min(ti.startRow + rowsToCalcPerThread - 1, bitmapHeight - 1);
                lastImageRowCalcRequested = ti.endRow;

                //Call the "RunWorkerAsync()" method of the thread.  
                //This will call the delegate method "backgroundWorkerFiles_DoWork()" method defined above.  
                //The parameter passed (the loop counter "f") will be available through the delegate's argument "e" through the ".Argument" property.
                ti.bwThread.RunWorkerAsync(ti);
                numberActiveBackGroundThreads++;
            }

        }

        private static void backgroundWorkerCalcs_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the BackgroundWorker that raised this event.
            BackgroundWorker worker = sender as BackgroundWorker;

            // Get argument from DoWorkEventArgs argument.  Can use any type here with cast
            ThreadInfo ti = (ThreadInfo)e.Argument;

            // Can return reulsts from this method, i.e. a status (OK, FAIL etc)
            e.Result = createMandlebrotImageSection(ti, worker, e);
        }

        /// <summary>
        /// Generates the Mandlebrot image for a section of the current main bitmap based on the information specified
        /// in the ThreadInfo for the specified thread. 
        /// </summary>
        /// <param name="ti">The ThreadInfo for the thread that is genarating this image section which includes the details of the section and a bitmap buffer of the section to populate</param>
        /// <param name="bw">The BackgroundWorker thread that is calling this routine</param>
        /// <param name="e">The DoWorkEventArgs arguments exchanged between the BackgroundWorker thread and this function</param>
        /// <returns>0 for normal exit or 1 if user has requested an exit that has caused cancellation of this thread to be requested</returns>
        private static int createMandlebrotImageSection(ThreadInfo ti, BackgroundWorker bw, DoWorkEventArgs e)
        {
            int bmpX, bmpY;
            FloatType zr, zi;
            int Z;
            int startRow = ti.startRow;
            int endRow = ti.endRow;
            FloatType myMinX = ti.fullMandImage.mandRect.X;
            FloatType myMinY = ti.fullMandImage.mandRect.Y;
            FloatType myXInc = (FloatType)(ti.fullMandImage.mandRect.Right - myMinX) / (bitmapWidth);
            FloatType myYInc = (FloatType)(ti.fullMandImage.mandRect.Bottom - myMinY) / (bitmapHeight);
            Bitmap bmp = ti.bmpSection;

            //if (ti.endRow == bitmapHeight - 1)
            //    Debugger.Break();

            using (Graphics g = Graphics.FromImage(ti.bmpSection))
            {
                g.Clear(BackColor);
            }

            Debug.WriteLine("createMandlebrotImageSection() Thread ID " + Thread.CurrentThread.ManagedThreadId + " started to calc rows " + ti.startRow + ", " + ti.endRow);
            //Debug.WriteLine("createMandlebrotImageSection() Thread ID " + Thread.CurrentThread.ManagedThreadId + " XInc, YInc " + XInc + ", " + YInc);
            //Debug.WriteLine("createMandlebrotImageSection() Thread ID " + Thread.CurrentThread.ManagedThreadId + " myYInc " + myYInc + " yStart " + (myMinY + startRow * myYInc));

            for (bmpY = startRow; bmpY <= endRow; bmpY++)
            {
                for (bmpX = 0; bmpX <= bitmapWidth - 1; bmpX++)
                {
                    //  Calculate point scaled coordinates.
                    // pt = new Complex (myMinX + bmpX * myXInc, myMinY + bmpY * myYInc);
                    zr = myMinX + bmpX * myXInc;
                    zi = myMinY + bmpY * myYInc;

                    // Calculate Mandelbrot breakout value for this coordinate.
                    if (bHighPrecisionOn)
                        Z = calcMandlebrotEscapeVal(zr, zi);
                    else
                        Z = calcMandlebrotEscapeVal((double)zr, (double)zi);

                    // Update pixel.
                    //c = Color.FromArgb(Z, Z, Z);
                    bmp.SetPixel(bmpX, bmpY - startRow, colors[Z]);
                }
                // If the operation was canceled by the user, 
                // set the DoWorkEventArgs.Cancel property to true.
                if (bw.CancellationPending)
                {
                    e.Cancel = true;
                    // Flag cancellation.
                    return 1;
                }

            }
            // Normal exit.
            return 0;
        }

    }

}
