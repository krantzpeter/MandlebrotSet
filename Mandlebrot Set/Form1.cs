﻿
// #define debugPaint
//#define ShowPaintThreadProgress
#define stdColors

using System;
using System.Numerics;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
//using FloatType = System.Quadruple;
using FloatType = System.Double;

using System.Runtime.InteropServices;
using System.Reflection;

namespace Mandlebrot_Set
{

    public partial class Form1 : Form
    {
        // Thread info array.
        //private ArrayList tiArrayList;
        //private static int maxThreads = 4;
        private static readonly object _locker = new object();
        private static int maxThreads = 16;
        private ThreadInfo[] threadInfoArray = null;
        private int numberActiveBackGroundThreads = 0; // To count number of active threads.
        // private Boolean threadsActive = false;
        private bool imageCalcInProgress = false;
        private Bitmap mainBmp = null;
        const int bitmapWidth = 1800/2; // Size of the bitmap.
        //const int bitmapWidth = 1040; // Size of the bitmap.
        const int bitmapHeight = 1040/2;
        private Point mainBmpLocationInForm = new Point();
        private bool showZoomFrames = true; // True if each frame you zoom into should be drawn on the window.

        private bool bHighPrecisionOn = false;

        private void UpdateMainBmpLocationInForm()
        {
            mainBmpLocationInForm.X = Math.Max((ClientRectangle.Width - mainBmp.Width) / 2, 0);
            mainBmpLocationInForm.Y = Math.Max((ClientRectangle.Height - mainBmp.Height) / 2, 0);
        }

        //private const int bitmapWidth = 81;  // Size of the bitmap.
        //private const int bitmapHeight = 81;
        private const int pixelsToCalcPerThread = 50000;
        private const int rowsToCalcPerThread = pixelsToCalcPerThread / bitmapWidth;
        private int lastImageRowCalcRequested = -1;  // Counts the number of rows in the main bitmap have been calculated by currently active threads.

        // BackgroundWorker variables to assist with multiple threads for time consuming calculations.
        //private BackgroundWorker[] threadArray = new BackgroundWorker[maxThreads];

        private Rectangle dragBoxFromMouseDown = Rectangle.Empty;
        private bool dragActive = false;
        private Point dragMouseDownPoint = Point.Empty;

        private ArrayList mainBmpImageList = new ArrayList(); // Used to contain list of MandlebrotImage values that contain the image of each zoom level and associated Mandlebrot X and Y min and max values.
        private const int maxImages = 40;
        private int imageListDisplayedElCount = 0;

        private FloatType baseval;
        private FloatType breakoutval;
        private FloatType aspectRatio;

        private FloatType minXBound; // Outer bounds of the Mandelbrot set.
        private FloatType maxXBound;
        private FloatType minYBound;
        private FloatType maxYBound;

        // Current scaled bounds of the current image.
        //private FloatType minX = minXBound;
        //private FloatType maxX = maxXBound;
        //private FloatType minY = minYBound;
        //private FloatType maxY = maxYBound;
        private myRect mandRect = new myRect();

        //private const int maxColorIndex = 768;
        private const int maxColorIndex = 768;
        private Color[] colors;

        // Increment values for each pixel in the image.
        //private FloatType XInc = (FloatType)(maxXBound - minXBound) / ((FloatType)bitmapWidth - 1);
        //private FloatType YInc = (FloatType)(maxYBound - minYBound) / ((FloatType)bitmapHeight - 1);
        private FloatType XInc;
        private FloatType YInc;

        private void setupColors()
        {
            colors = new Color [maxColorIndex+1];
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
                colors[enumValue] = Color.FromArgb(255,r, g, b);
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
        private void InitializeThreadInfoArray()
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
        }

        public Form1()
        {
            InitializeComponent();
            //tiArrayList = new ArrayList();
            // Do setup tasks.
            mainBmp = new Bitmap(bitmapWidth, bitmapHeight);
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

        private void Form1_Load(object sender, EventArgs e)
        {


            //calcMandlebrotEscapeVal(new Complex(-1, 1));
            UpdateMainBmpLocationInForm();

            typeof(Panel).GetProperty("DoubleBuffered",
                          BindingFlags.NonPublic | BindingFlags.Instance)
             .SetValue(panel1, true, null);
            DoubleBuffered = true;

            //Quadruple x;
            //Quadruple y = 10;
            //Quadruple z = 3;
            //x = y / z;
            //Debug.WriteLine(x);

            // Clear main bitmap
            using (Graphics g = Graphics.FromImage(mainBmp))
                {
                    g.Clear(BackColor);
                }

            this.panel1.BackColor = Color.FromKnownColor(KnownColor.Transparent);

            // Enable timer to check whether threads are finished.
            //timer1.Enabled = true;

            //createMandlebortImage();
            UseWaitCursor = true;
            CreateMandelbrotImageThreads(minXBound, maxXBound, minYBound, maxYBound);

            //flagGraphics = Graphics.FromImage(flag);
            //int red = 0;
            //int white = 11;
            //while (white <= 100)
            //{
            //    flagGraphics.FillRectangle(Brushes.Red, 0, red, 200, 10);
            //    flagGraphics.FillRectangle(Brushes.White, 0, white, 200, 10);
            //    red += 20;
            //    white += 20;
            //}
            //for (int i = 50; i <= 100; i++)
            //{
            //    flag.SetPixel(i, 55, Color.Black);
            //}

        }

        //private void backgroundWorkerCalcs_ProgressChanged(object sender, ProgressChangedEventArgs e)
        //{
        //    // Use this method to report progress to GUI
        //}

        /// <summary>
        /// Adds the Form's mainBmp and associated mandRect.X, mandRect.Right, mandRect.Y and mandRect.Bottom values to a stack implemented as ArrayList mainBmpImageList.
        /// </summary>
        private void pushBitmap()
        {
            // If adding an element back to the middle of the stack then clear all remaining elemments.
            if (imageListDisplayedElCount < mainBmpImageList.Count)
            {
                // Run through remaining elements and clean any bitmaps.
                for (int i = imageListDisplayedElCount; i < mainBmpImageList.Count; i++)
                {
                    // Remove the bitmap we're abandoning
                    Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType> tkill = (Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType>)mainBmpImageList[i];
                    Bitmap b = tkill.Item1;
                    b.Dispose();

                }

                // Now remove any trailing elements.
                mainBmpImageList.RemoveRange(imageListDisplayedElCount, mainBmpImageList.Count - imageListDisplayedElCount);

            }

            // If too many elements then remove one from the stack bottom and continue.
            if (mainBmpImageList.Count >= maxImages)
            {
                // Remove bottom element but first clean up the bitmap we're abandoning
                Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType> tkill = (Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType>)mainBmpImageList[0];
                Bitmap b = tkill.Item1;
                b.Dispose();

                // Now remove element.
                mainBmpImageList.RemoveAt(0);

                // Decrement count to adjust "top" element in stack for removed element.
                imageListDisplayedElCount--;
            }

            // Now create another element for the current bitmap at the end.
            var t = Tuple.Create((Bitmap)mainBmp.Clone(), mandRect.X, mandRect.Right, mandRect.Y, mandRect.Bottom);
            mainBmpImageList.Add(t);

            // Update the count of the last element in the stack.
            imageListDisplayedElCount++;
        }

        /// <summary>
        /// Update mainBmp and associated mandRect.X, mandRect.Right, mandRect.Y and mandRect.Bottom values from the "stack top" in ArrayList mainBmpImageList.
        /// Note that the count of elemetns to the current stack top is imageListDisplayedElCount.  Returns true for success.
        /// </summary>
        private bool popBitmap()
        {
            if (imageListDisplayedElCount <= 1)
            {
                // There is no element to pop off so indicate that pop failed. (we always leave the top element on the stack).
                return false;
            }

            // Decrement count in line with count of new "top" element in stack.
            imageListDisplayedElCount--;

            // There is an element to pop so pop it.
            //Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType> t = (Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType>)mainBmpImageList[imageListDisplayedElCount-1];
            var t = (Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType>)mainBmpImageList[imageListDisplayedElCount-1];

            // Copy image on stack to mainBmp
            Graphics g = Graphics.FromImage(mainBmp);
            g.DrawImage(t.Item1, 0, 0);
            g.Dispose();
            //mainBmp = t.Item1;

            mandRect.X = t.Item2;
            mandRect.Right = t.Item3;
            mandRect.Y = t.Item4;
            mandRect.Bottom = t.Item5;

            XInc = (FloatType)(mandRect.Width) / (mainBmp.Width);
            YInc = (FloatType)(mandRect.Height) / (mainBmp.Height);

            // Indicate that pop succeeded.
            return true;
        }
        /// <summary>
        /// Gets the next image in the stack (mainBmpImageLIst) and updates mainBmp and associated mandRect.X, mandRect.Right, mandRect.Y and mandRect.Bottom values from it.
        /// </summary>
        /// <returns>True if there was a next image otherwise false</returns>
        private bool nextBitmap()
        {
            if (imageListDisplayedElCount >= mainBmpImageList.Count)
            {
                // There is no element to return so indicate this.
                return false;
            }

            // There is a next element so update mainBmp (etc) from it.
            Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType> t = (Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType>)mainBmpImageList[imageListDisplayedElCount];
            // Copy image on stack to mainBmp
            Graphics g = Graphics.FromImage(mainBmp);
            g.DrawImage(t.Item1, 0, 0);
            g.Dispose();

            //mainBmp = t.Item1;
            mandRect.X = t.Item2;
            mandRect.Right = t.Item3;
            mandRect.Y = t.Item4;
            mandRect.Bottom = t.Item5;

            XInc = (FloatType)(mandRect.Width) / (mainBmp.Width);
            YInc = (FloatType)(mandRect.Height) / (mainBmp.Height);

            // Increment count in line with count of new "top" element in stack.
            imageListDisplayedElCount++;

            // Retrieve a new element so indicate success.
            return true;
        }

//When any thread finishes, the backgroundWorkerCalcs_RunWorkerCompleted function 
//    •	Copies the updated bitmap to the main bitmap and disposes of the section bmp and invalidate that section of the main bmp image so it is repainted.
//    •	checks the noImageRowsCalcRequested value to see if all rows have been issued for calculation.  
//    •	If not all issued for calc, calls AddImageCalcThread to reactivate this thread for the next section.
//    •	If all issued for calc,  if this is the last thread running (check activeThreadCount) remove wait mousepointer and reset noRowsCalcd to 0.  If not the last active thread running then simply return so that the next finishing thread can do the finalisation work.

        private void backgroundWorkerCalcs_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
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
                for (int i = 0; i < maxThreads; i++)
                {
                    if (threadInfoArray[i].bwThread == worker)
                    {
                        ti = threadInfoArray[i];
                        break;
                    }
                }

#if ShowPaintThreadProgress
                Debug.WriteLine("backgroundWorkerCalcs_RunWorkerCompleted() Thread ID " + Thread.CurrentThread.ManagedThreadId + " finished calculating rows " + ti.startRow + ", " + ti.endRow);
#endif
                if (ti != null)
                {
                    // Copy the update image from this sectionBmp to the main bmp.
                    int bmpSectionHeight = ti.endRow - ti.startRow + 1;
                    Rectangle updateRect = new Rectangle(0, ti.startRow, ti.mainBmpWidth, bmpSectionHeight);
                    //updateRect.X = Math.Max((this.ClientRectangle.Width - mainBmp.Width) / 2, 0);
                    //CopyBitmapSectionToFormBitmap(ti.bmpSection, updateRect);


                    // Copy the update image from this sectionBmp to the main bmp.
                    Graphics mainBmpGraphics = Graphics.FromImage(mainBmp);
                    //updateRect.Offset(mainBmpLocationInForm);
                    mainBmpGraphics.DrawImage(ti.bmpSection, updateRect, new Rectangle(0, 0, ti.bmpSection.Width, ti.bmpSection.Height), GraphicsUnit.Pixel);

                    // Change coords to the correct psn in the form (vs the bitmap) so it can be invalidated.
                    updateRect.Offset(mainBmpLocationInForm);
                    this.Invalidate(updateRect);
                    //Application.DoEvents();
#if ShowPaintThreadProgress
                    Debug.WriteLine("backgroundWorkerCalcs_RunWorkerCompleted() Thread ID " + Thread.CurrentThread.ManagedThreadId + " invalidated Rect: " + updateRect);
#endif

                    // Cleanup
                    mainBmpGraphics.Dispose();

                    if (imageCalcInProgress == false || lastImageRowCalcRequested + 1 >= bitmapHeight)
                    {
                        // We've finished requesting calculation of any further elements.
                        if (numberActiveBackGroundThreads <= 0)
                        {
                            // The final running thread has just ended so we're finished calculating.
                            imageCalcInProgress = false;
                            lastImageRowCalcRequested = -1;
                            UseWaitCursor = false;
                            Cursor = Cursors.Default;
                            pushBitmap();
                        }
                    }
                    else
                    {
                        // We've not finished calculation so add another thread.
                        AddImageCalcThread(ti);
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
        private int calcMandlebrotEscapeVal(Quadruple cr, Quadruple ci)
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
        private int calcMandlebrotEscapeVal(double cr, double ci)
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
                        AddImageCalcThread(threadInfoArray[threadNum]);
                    }
                }
            }

        }

        private void AddImageCalcThread(ThreadInfo ti)
        {
            // Grab lock to make sure that no other thread updates the lastImageRowCalcRequested or numberActiveBackGroundThreads.
            lock (_locker)
            {
                ti.startRow = lastImageRowCalcRequested + 1;
                ti.mainBmpWidth = bitmapWidth;
                ti.mainBmpHeight = bitmapHeight;

                if (ti.startRow >= ti.mainBmpHeight)
                {
                    // We've finished calculating the image so exit.
                    return;
                    //throw new Exception("ERROR - AddImageCalcThread() requested to calc rows beyond end of image.  Start row " + ti.startRow + " whereas image length " + ti.mainBmpHeight + ".");
                }

                ti.endRow = Math.Min(ti.startRow + rowsToCalcPerThread - 1, ti.mainBmpHeight - 1);
                lastImageRowCalcRequested = ti.endRow;
                ti.minX = mandRect.X;
                ti.maxX = mandRect.Right;
                ti.minY = mandRect.Y;
                ti.maxY = mandRect.Bottom;

                //Call the "RunWorkerAsync()" method of the thread.  
                //This will call the delegate method "backgroundWorkerFiles_DoWork()" method defined above.  
                //The parameter passed (the loop counter "f") will be available through the delegate's argument "e" through the ".Argument" property.
                ti.bwThread.RunWorkerAsync(ti);
                numberActiveBackGroundThreads++;
            }

        }

        private void backgroundWorkerCalcs_DoWork(object sender, DoWorkEventArgs e)
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
        private int createMandlebrotImageSection(ThreadInfo ti, BackgroundWorker bw, DoWorkEventArgs e)
        {
            int bmpX, bmpY;
            FloatType zr, zi;
            int Z;
            int startRow = ti.startRow;
            int endRow = ti.endRow;
            int myBitmapWidth = ti.mainBmpWidth;
            FloatType myMinX = ti.minX;
            FloatType myMinY = ti.minY;
            FloatType myXInc = (FloatType)(ti.maxX - myMinX) / (ti.mainBmpWidth);
            FloatType myYInc = (FloatType)(ti.maxY - myMinY) / (ti.mainBmpHeight);
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
                for (bmpX = 0; bmpX <= myBitmapWidth - 1; bmpX++) 
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


        //private void SetPixel_Example(PaintEventArgs e)
        //{

        //    // Create a Bitmap object from a file.
        //    Bitmap myBitmap = new Bitmap("C:\\Users\\Peter\\Pictures\\HopperThumb.jpg");

        //    // Draw myBitmap to the screen.
        //    e.Graphics.DrawImage(myBitmap, 0, 0, myBitmap.Width,
        //        myBitmap.Height);

        //    // Set each pixel in myBitmap to black.
        //    for (int Xcount = 0; Xcount < myBitmap.Width; Xcount++)
        //    {
        //        for (int Ycount = 0; Ycount < myBitmap.Height; Ycount++)
        //        {
        //            myBitmap.SetPixel(Xcount, Ycount, Color.Black);
        //        }
        //    }

        //    // Draw myBitmap to the screen again.
        //    e.Graphics.DrawImage(myBitmap, myBitmap.Width, 0,
        //        myBitmap.Width, myBitmap.Height);
        //}

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            //return;

            //pictureBox1.Image = mainBmp;
            //pictureBox1.Invalidate();

            //e.ClipRectangle
            //e.Graphics.Clear(Color.Black);
            //e.Graphics.DrawImage(mainBmp, Math.Max((this.Width - mainBmp.Width) / 2, 0), Math.Max((this.Height - mainBmp.Height) / 2, 0), mainBmp.Width, mainBmp.Height);
            Rectangle src = new Rectangle();
            Rectangle dest = new Rectangle();

            // If nothing to paint then exit.
            if (e.ClipRectangle.Width == 0 && e.ClipRectangle.Height == 0)
                return;

            if (e.ClipRectangle.X < mainBmpLocationInForm.X)
            {
                // Trying to paint a portion of the screen which requires a margin as the bmp is smaller than the form.
                src.X = 0;
                dest.X = mainBmpLocationInForm.X;
                //src.Width = e.ClipRectangle.Width;
                dest.Width = Math.Min(e.ClipRectangle.Width - (mainBmpLocationInForm.X - e.ClipRectangle.X), bitmapWidth);
                src.Width = dest.Width;
            }
            else
            {
                // Trying to paint a portion of the screen which requires no margin as it's into a part of the bitmap.
                src.X = e.ClipRectangle.X - mainBmpLocationInForm.X;
                src.Width = Math.Min(e.ClipRectangle.Width, bitmapWidth - src.X);
                dest.X = e.ClipRectangle.X;
                dest.Width = src.Width;
            }

            if (e.ClipRectangle.Y < mainBmpLocationInForm.Y)
            {
                // Trying to paint a portion of the screen which requires a margin as the bmp is smaller than the form.
                src.Y = 0;
                dest.Y = mainBmpLocationInForm.Y;
                //src.Height = e.ClipRectangle.Height;
                dest.Height = Math.Min(e.ClipRectangle.Height - (mainBmpLocationInForm.Y - e.ClipRectangle.Y), bitmapHeight);
                src.Height = dest.Height;
            }
            else
            {
                // Trying to paint a portion of the screen which requires no margin as it's into a part of the bitmap.
                src.Y = e.ClipRectangle.Y - mainBmpLocationInForm.Y;
                src.Height = Math.Min(e.ClipRectangle.Height, bitmapHeight - src.Y);
                dest.Y = e.ClipRectangle.Y;
                dest.Height = src.Height;
            }

            //src = e.ClipRectangle;
            //src.Offset(-mainBmpLocationInForm.X, -mainBmpLocationInForm.Y);
            e.Graphics.DrawImage(mainBmp, dest, src, GraphicsUnit.Pixel);

            if (showZoomFrames)
            {

                // Draw zoom rectangles for remaining images in the stack.
                FloatType zoomMinX, zoomMaxX, zoomMinY, zoomMaxY;
                int zoomX, zoomY, zoomWidth, zoomHeight;
                Pen p = new Pen(Color.White);
                Rectangle zoomRect;
                FloatType zoomPixelXInc = bitmapWidth / (mandRect.Width);
                FloatType zoomPixelYInc = bitmapHeight / (mandRect.Height);


                for (int i = imageListDisplayedElCount; i < mainBmpImageList.Count; i++)
                {
                    // Find display rectange for this image
                    Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType> t = (Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType>)mainBmpImageList[i];

                    zoomMinX = t.Item2;
                    zoomMaxX = t.Item3;
                    zoomMinY = t.Item4;
                    zoomMaxY = t.Item5;

                    zoomWidth = (int)((zoomMaxX - zoomMinX) * zoomPixelXInc);
                    zoomHeight = (int)((zoomMaxY - zoomMinY) * zoomPixelYInc);
                    zoomX = mainBmpLocationInForm.X + (int)((zoomMinX - mandRect.X) * zoomPixelXInc);
                    zoomY = mainBmpLocationInForm.Y + (int)((zoomMinY - mandRect.Y) * zoomPixelYInc);

                    zoomRect = new Rectangle(zoomX, zoomY, zoomWidth, zoomHeight);
                    e.Graphics.DrawRectangle(p, zoomRect);
                }

                // Dispose of pen.
                p.Dispose();

            }
            //e.Graphics.DrawImage(mainBmp, Math.Max((this.Width - mainBmp.Width) / 2, 0), Math.Max((this.Height - mainBmp.Height) / 2, 0), 9, 9);

#if ShowPaintThreadProgress
            Debug.WriteLine("Form1_Paint() Thread ID " + Thread.CurrentThread.ManagedThreadId + " Paint invalidated Rect: " + e.ClipRectangle);
#endif

#if debugPaint == true
            // Check that what just got painted to the screen actually arrived on the screen by checking the pixel in the top left of the bit of the bitmap that
            // just got painted to see it's the same colour as the pixel that is now on the screen
            Color c1 = mainBmp.GetPixel(dest.X - mainBmpLocationInForm.X, dest.Y - mainBmpLocationInForm.Y);
            Color c2 = GetPixel(dest.X, dest.Y);

            if (c1 != c2)
            {
                Debugger.Break();
            }
#endif

        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (mainBmp != null)
            {
                mainBmp.Dispose();
                mainBmp = null;
            }

            // Dispose of bitmaps in image stack.
            for (int i = 0; i < mainBmpImageList.Count; i++)
            {
                // Remove the bitmap we're abandoning
                Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType> tkill = (Tuple<Bitmap, FloatType, FloatType, FloatType, FloatType>)mainBmpImageList[i];
                Bitmap b = tkill.Item1;
                if (b != null)
                    b.Dispose();
            }

        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            // Remember the point where the mouse down occurred. 
            dragMouseDownPoint = e.Location;

            // Remember the area where the mouse down occurred. The DragSize indicates
            // the size that the mouse can move before a drag event should be started.                
            Size dragSize = SystemInformation.DragSize;

            // Create a rectangle using the DragSize, with the mouse position being
            // at the center of the rectangle.
            dragBoxFromMouseDown = new Rectangle(new Point(e.X - (dragSize.Width / 2),
                                                           e.Y - (dragSize.Height / 2)), dragSize);

        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!imageCalcInProgress && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                // If the mouse moves outside the rectangle, start the drag.
                if (dragBoxFromMouseDown != Rectangle.Empty &&
                    !dragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    // Drag has commenced.
                    //this.SuspendLayout();
                    dragActive = true;
                    this.panel1.Location = dragMouseDownPoint;
                    //this.panel1.Size = (Size)e.Location - (Size)dragMouseDownPoint;
                    this.panel1.Height = Math.Min(e.X - dragMouseDownPoint.X, e.Y - dragMouseDownPoint.Y);
                    this.panel1.Width = (int)(this.panel1.Height * aspectRatio);
                    this.panel1.Visible = true;
                    //this.ResumeLayout();

                    //// If size or previous mouse down drag box is not zero, it must have been rendered so 


                    ////  Mouse moved and button held down so this is starting or continuing a drag event.
                    //dragBoxFromMouseDown.Size = (Size)(dragBoxFromMouseDown.Location) - (Size)(e.Location);
                }
            }


        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            if (dragActive)
            {
                // Drag has now ended.
                dragActive = false;
                this.panel1.Visible = false;

                // Turn on wait cursor and disable further mouse clicks until we've finished.
                UseWaitCursor = true;

                Rectangle r = new Rectangle();
                r = this.panel1.Bounds;
                //r.Offset(this.panel1.Location);
                r.Offset(-mainBmpLocationInForm.X, -mainBmpLocationInForm.Y);
                //r.Offset(-Math.Max((this.Width - mainBmp.Width) / 2, 0), -Math.Max((this.Height - mainBmp.Height) / 2, 0));
                FloatType myMaxX = mandRect.X + r.Right * XInc;
                FloatType myMinX = mandRect.X + r.X * XInc;
                FloatType myMaxY = mandRect.Y + r.Bottom * YInc;
                FloatType myMinY = mandRect.Y + r.Y * YInc;
                //XInc = (FloatType)(mandRect.Right - mandRect.X) / (mainBmp.Width - 1);
                //YInc = (FloatType)(mandRect.Bottom - mandRect.Y) / (mainBmp.Height - 1);

                //createMandlebrotImage();
                // Switch to Quadruple instead of double values when (Math.Abs(XInc)<8.0E-16 || Math.Abs(YInc)<8.0E-16)
                CreateMandelbrotImageThreads(myMinX, myMaxX, myMinY, myMaxY);

                //this.Invalidate();
            }
        }

        //private void timer1_Tick(object sender, EventArgs e)
        //{
        //    //Check whether threads are finished.
        //    ThreadInfo ti;
        //    for (int i = 0; i < tiArrayList.Count; i++)
        //    {
        //        ti = (ThreadInfo)tiArrayList[i];
        //        if (ti.threadFinished)
        //        {
        //            // Thread is finished so copy the bitmap back to the main bitmap and kill the thread info.

        //            Graphics mainBmpGraphics = Graphics.FromImage(mainBmp);
        //            //mainBmpGraphics.DrawImage(ti.bmp, new Rectangle(0, 0, bitmapWidth, bitmapHeight / 2), new Rectangle(0, 0, bitmapWidth, bitmapHeight / 2), GraphicsUnit.Pixel);
        //            mainBmp = ti.bmpSection;
        //            this.Invalidate();
        //            //pictureBox1.Image = mainBmp;
        //            //pictureBox1.Invalidate();
        //            UseWaitCursor = false;
        //            threadsActive = false;
        //            tiArrayList.RemoveAt(i);
        //            i--;
        //            mainBmpGraphics.Dispose();
        //        }
        //    }
        //}

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Turn of timer that is checking threads are finished.
            Boolean oldUseWaitCursor = UseWaitCursor;
            UseWaitCursor = true;

            //timer1.Enabled = false;
            
            // Instruct all threads to finish asap.
            
            ThreadInfo ti;
            for (int i = 0; i < maxThreads; i++)
            {
                ti = (ThreadInfo)threadInfoArray[i];
                ti.bwThread.CancelAsync();
            }

            // Wait for threads to finish.
            Boolean allFinished = false;
            while (!allFinished)
            {
                allFinished = true;
                for (int i = 0; i < maxThreads; i++)
                {
                    ti = (ThreadInfo)threadInfoArray[i];
                    allFinished = allFinished && !ti.bwThread.IsBusy;
                    Application.DoEvents();
                }
            }
            //MessageBox.Show("Counted to " + c + "during thread exit wait.");
            UseWaitCursor = oldUseWaitCursor;
        }

        public void Form1_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case (char)Keys.Escape:
                    e.Handled = true;
                    if (dragActive)
                    {
                        // There's a drag going on so assume pressing escape is trying to cancel it.
                        dragActive = false;
                        dragBoxFromMouseDown = Rectangle.Empty;
                        panel1.Visible = false;
                        //panel1.Refresh();
                    }
                    else
                    {
                        Application.Exit();
                    }
                    break;

               // case (char)Keys.Up:
                case 'o':
                    // Request to zoom out.
                    e.Handled = true;
                    if (imageCalcInProgress)
                    {
                        // Ignore keystroke as calculating.
                        Console.Beep();
                    }
                    else
                    {
                        if (popBitmap())
                        {
                            // Succeeded in getting a new bitmap off the stack so display it.
                            Invalidate(new Rectangle(mainBmpLocationInForm, new Size(bitmapWidth, bitmapHeight)));
                            //Refresh();
                        }
                        else
                        {
                            // No bitmap to pop so beep.
                            Console.Beep();
                        }
                    }
                    break;


                //case (char)Keys.Down:
                case 'i':
                    // Request to zoom in.
                    e.Handled = true;
                    if (imageCalcInProgress)
                    {
                        // Ignore keystroke as calculating.
                        Console.Beep();
                    }
                    else
                    {

                        if (nextBitmap())
                        {
                            // Succeeded in getting a new bitmap off the stack so display it.
                            Invalidate(new Rectangle(mainBmpLocationInForm, new Size(bitmapWidth, bitmapHeight)));
                            //Refresh();
                        }
                        else
                        {
                            // No bitmap to go to so beep.
                            Console.Beep();
                        }
                    }
                    break;

            }

        }


        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            UpdateMainBmpLocationInForm();
            Invalidate();
        }



        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr srchDc, int srcX, int srcY, int srcW, int srcH,
                                IntPtr desthDc, int destX, int destY, int op);

        public static Color GetPixel(int x, int y)
        {
            using (Bitmap screenPixel = new Bitmap(1, 1))
            {
                using (Graphics gdest = Graphics.FromImage(screenPixel))
                {
                    Process p = Process.GetCurrentProcess();
                    p.Refresh();
                    using (Graphics gsrc = Graphics.FromHwnd(p.MainWindowHandle))
                    {
                        IntPtr hsrcdc = gsrc.GetHdc();
                        IntPtr hdc = gdest.GetHdc();
                        BitBlt(hdc, 0, 0, 1, 1, hsrcdc, x, y, (int)CopyPixelOperation.SourceCopy);
                        gdest.ReleaseHdc();
                        gsrc.ReleaseHdc();
                    }
                }

                return screenPixel.GetPixel(0, 0);
            }
        }

        //    private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        //    {
        //        // Remember the point where the mouse down occurred. 
        //        dragMouseDownPoint = e.Location;

        //        // Remember the area where the mouse down occurred. The DragSize indicates
        //        // the size that the mouse can move before a drag event should be started.                
        //        Size dragSize = SystemInformation.DragSize;

        //        // Create a rectangle using the DragSize, with the mouse position being
        //        // at the center of the rectangle.
        //        dragBoxFromMouseDown = new Rectangle(new Point(e.X - (dragSize.Width / 2),
        //                                                       e.Y - (dragSize.Height / 2)), dragSize);

        //    }

        //    private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        //    {
        //        if (!threadsActive && (e.Button & MouseButtons.Left) == MouseButtons.Left)
        //        {
        //            // If the mouse moves outside the rectangle, start the drag.
        //            if (dragBoxFromMouseDown != Rectangle.Empty &&
        //                !dragBoxFromMouseDown.Contains(e.X, e.Y))
        //            {
        //                // Drag has commenced.
        //                //this.SuspendLayout();
        //                dragActive = true;
        //                this.panel1.Location = dragMouseDownPoint;
        //                //this.panel1.Size = (Size)e.Location - (Size)dragMouseDownPoint;
        //                this.panel1.Height = Math.Min(e.X - dragMouseDownPoint.X, e.Y - dragMouseDownPoint.Y);
        //                this.panel1.Width = this.panel1.Height;
        //                this.panel1.Visible = true;
        //                //this.ResumeLayout();

        //                //// If size or previous mouse down drag box is not zero, it must have been rendered so 


        //                ////  Mouse moved and button held down so this is starting or continuing a drag event.
        //                //dragBoxFromMouseDown.Size = (Size)(dragBoxFromMouseDown.Location) - (Size)(e.Location);
        //            }
        //        }
        //    }

        //    private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        //    {
        //        if (dragActive)
        //        {
        //            // Drag has now ended.
        //            dragActive = false;
        //            this.panel1.Visible = false;

        //            // Turn on wait cursor and disable further mouse clicks until we've finished.
        //            UseWaitCursor = true;

        //            Rectangle r = new Rectangle();
        //            r = this.panel1.DisplayRectangle;
        //            r.Offset(this.panel1.Location);
        //            r.Offset(-Math.Max((this.Width - mainBmp.Width) / 2, 0), -Math.Max((this.Height - mainBmp.Height) / 2, 0));
        //            mandRect.Right = mandRect.X + r.Right * XInc;
        //            mandRect.X = mandRect.X + r.X * XInc;
        //            mandRect.Bottom = mandRect.Y + r.Bottom * YInc;
        //            mandRect.Y = mandRect.Y + r.Y * YInc;
        //            XInc = (FloatType)(mandRect.Right - mandRect.X) / (mainBmp.Width - 1);
        //            YInc = (FloatType)(mandRect.Bottom - mandRect.Y) / (mainBmp.Height - 1);

        //            //createMandlebrotImage();
        //            CreateMandelbrotImageThreads();

        //            //this.Invalidate();
        //        }
        //    }

    }
}
