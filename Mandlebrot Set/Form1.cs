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

namespace Mandlebrot_Set
{

    public partial class Form1 : Form
    {
        // Thread info array.
        //private ArrayList tiArrayList;
        private static int maxThreads = 4;
        private ThreadInfo[] threadInfoArray = null;
        private int numberActiveBackGroundThreads = 0; // To count number of active threads.
        // private Boolean threadsActive = false;
        private Boolean imageCalcInProgress = false;
        private Bitmap mainBmp = null;
        const int bitmapWidth = 1800; // Size of the bitmap.
        //const int bitmapWidth = 1040; // Size of the bitmap.
        const int bitmapHeight = 1040;

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

        private const double baseval = 2.0;
        private const double breakoutval = baseval * baseval;
        private const double aspectRatio = (double)bitmapWidth / (double)bitmapHeight;

        private const double minXBound = -baseval * aspectRatio; // Outer bounds of the Mandelbrot set.
        private const double maxXBound = baseval * aspectRatio;
        private const double minYBound = baseval;
        private const double maxYBound = -baseval;

        // Current scaled bounds of the current image.
        //private double minX = minXBound;
        //private double maxX = maxXBound;
        //private double minY = minYBound;
        //private double maxY = maxYBound;
        private double minX;
        private double maxX;
        private double minY;
        private double maxY;

        //private const int maxColorIndex = 768;
        private const int maxColorIndex = 768;
        private Color[] colors;

        // Increment values for each pixel in the image.
        //private double XInc = (double)(maxXBound - minXBound) / ((double)bitmapWidth - 1);
        //private double YInc = (double)(maxYBound - minYBound) / ((double)bitmapHeight - 1);
        private double XInc;
        private double YInc;

        private void setupColors()
        {
            colors = new Color [maxColorIndex+1];
            for (int enumValue = 0; enumValue < colors.Length; enumValue++)
            {
                colors[enumValue] = Color.FromKnownColor((KnownColor)(enumValue % 139 + 28));
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
            //pictureBox1.Image = (Image)mainBmp;
            setupColors();
            InitializeThreadInfoArray();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            

            //calcMandlebrotEscapeVal(new Complex(-1, 1));

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


//When any thread finishes, the backgroundWorkerCalcs_RunWorkerCompleted function 
//    •	Copies the updated bitmap to the main bitmap and disposes of the section bmp and invalidate that section of the main bmp image so it is repainted.
//    •	checks the noImageRowsCalcRequested value to see if all rows have been issued for calculation.  
//    •	If not all issued for calc, calls AddImageCalcThread to reactivate this thread for the next section.
//    •	If all issued for calc,  if this is the last thread running (check activeThreadCount) remove wait mousepointer and reset noRowsCalcd to 0.  If not the last active thread running then simply return so that the next finishing thread can do the finalisation work.

        private void backgroundWorkerCalcs_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
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

            if (ti != null)
            {
                // Copy the update image from this sectionBmp to the main bmp.
                Graphics mainBmpGraphics = Graphics.FromImage(mainBmp);
                int bmpSectionHeight = ti.endRow - ti.startRow + 1;
                Rectangle updateRect = new Rectangle(0, ti.startRow, ti.mainBmpWidth, bmpSectionHeight);
                mainBmpGraphics.DrawImage(ti.bmpSection, updateRect, new Rectangle(0, 0, ti.mainBmpWidth, bmpSectionHeight), GraphicsUnit.Pixel);
                updateRect.X = Math.Max((this.ClientRectangle.Width - mainBmp.Width) / 2, 0);
                CopyBitmapSectionToFormBitmap(ti.bmpSection, updateRect);

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
                    }
                }
                else
                {
                    // We've not finished calculation so add another thread.
                    AddImageCalcThread(ti);
                }
                // Cleanup
                mainBmpGraphics.Dispose();
            }
 
        }
        /// <summary>
        /// Copies a Bitmap to the Form's mainBmp bitmap and invalidates the rectange so it's repainted.
        /// </summary>
        /// <param name="bmp">The bitmap to copy to the Form's mainBmp instance</param>
        protected void CopyBitmapToFormBitmap(Bitmap bmp)
        {
            CopyBitmapSectionToFormBitmap(bmp, new Rectangle(0, 0, mainBmp.Width, mainBmp.Height));
        }

        /// <summary>
        /// Copies a partial Bitmap ("section" Bitmap, which includes a number of rows of the form's main bitmap) 
        /// to the Form's mainBmp bitmap and invalidates the rectange so it's repainted.
        /// </summary>
        /// <param name="bmpSection">Bitmap section to update</param>
        /// <param name="updateRect">Recangle with coordinates in the mainBmp to which the section should be updated to</param>
        protected void CopyBitmapSectionToFormBitmap(Bitmap bmpSection, Rectangle updateRect)
        {
            // Copy the update image from this sectionBmp to the main bmp.
            Graphics mainBmpGraphics = Graphics.FromImage(mainBmp);
            mainBmpGraphics.DrawImage(bmpSection, updateRect, new Rectangle(0, 0, mainBmp.Width, bmpSection.Height), GraphicsUnit.Pixel);
            updateRect.X = Math.Max((this.ClientRectangle.Width - mainBmp.Width) / 2, 0);
            this.Invalidate(updateRect);
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
        private int calcMandlebrotEscapeVal(Complex c)
        {
            double Z;
            double dblTmp;
            Complex c1;
            Complex lastpassval;

            Z = c.Real * c.Real + c.Imaginary * c.Imaginary;

            if (Z >= breakoutval)
                // We've broken out in first pass so exit returning pass no.
                return 0;

            lastpassval = c;

            // Create new point in form (x^2-y^2, 2xy) + c
            dblTmp = c.Real * c.Imaginary;
            c1 = new Complex(c.Real * c.Real - c.Imaginary * c.Imaginary, dblTmp + dblTmp) + c;

            for (int i = 1; i <= maxColorIndex; i++)
            {
                 if (lastpassval == c1)
                    // Value has not changed on this pass so it will never change again so escape and return max value.
                    return maxColorIndex;

                Z = c1.Real * c1.Real + c1.Imaginary * c1.Imaginary;

                if (Z >= breakoutval)
                    // We've broken out so exit returning pass no.
                    return i;

                lastpassval = c1;
                // Create new point in form (x^2-y^2, 2xy) + c
                dblTmp = c1.Real * c1.Imaginary;
                c1 = new Complex(c1.Real * c1.Real - c1.Imaginary * c1.Imaginary, dblTmp + dblTmp) + c;
            }

            // We didn't break out so return max value.
            return maxColorIndex;
        }



        /// <summary>
        /// Spawns a series of threads up to maxThreads, each of which will calculate the points in successive sections of the image, defined by a start and stop row in threadInfo and
        ///  Form1.minX, maxX, minY, maxY, xInc and yInc which define the mandelbrot value range that maps to the overall bitmap to be generated.
        ///
        /// Checks that noRowsCalcd is 0 and if it’s not, issue error msg that we tried to recalc whilst a recalc was already underway and return.
        /// If it is zero, for 0 to maxThreads – 1, call AddImageCalcThread for the relevant threadInfo element.
        /// 
        /// </summary>
        private void CreateMandelbrotImageThreads(double myMinX, double myMaxX, double myMinY, double myMaxY)
        {
            // Update the boundng values defining the overall image for the current Mandlebrot "zoom level"
            minX = myMinX;
            maxX = myMaxX;
            minY = myMinY;
            maxY = myMaxY;
            XInc = (double)(maxX - minX) / (mainBmp.Width - 1);
            YInc = (double)(maxY - minY) / (mainBmp.Height - 1);

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

        private void AddImageCalcThread(ThreadInfo ti)
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
            ti.minX = minX;
            ti.maxX = maxX;
            ti.minY = minY;
            ti.maxY = maxY;

            //Call the "RunWorkerAsync()" method of the thread.  
            //This will call the delegate method "backgroundWorkerFiles_DoWork()" method defined above.  
            //The parameter passed (the loop counter "f") will be available through the delegate's argument "e" through the ".Argument" property.
            ti.bwThread.RunWorkerAsync(ti);
            numberActiveBackGroundThreads++;

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
            Complex pt;
            int Z;
            int startRow = ti.startRow;
            int endRow = ti.endRow;
            int myBitmapWidth = ti.mainBmpWidth;
            double myMinX = ti.minX;
            double myMinY = ti.minY;
            double myXInc = (double)(ti.maxX - myMinX) / (ti.mainBmpWidth - 1);
            double myYInc = (double)(ti.maxY - myMinY) / (ti.mainBmpHeight - 1);
            Bitmap bmp = ti.bmpSection;

            for (bmpY = startRow; bmpY <= endRow; bmpY++)
            {
                for (bmpX = 0; bmpX <= myBitmapWidth - 1; bmpX++) 
                {
                    //  Calculate point scaled coordinates.
                    pt = new Complex (myMinX + bmpX * myXInc, myMinY + bmpY * myYInc);

                    // Calculate Mandelbrot breakout value for this coordinate.
                    Z = calcMandlebrotEscapeVal(pt);

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
            e.Graphics.Clear(Color.Black);
            //e.Graphics.DrawImage(mainBmp, Math.Max((this.Width - mainBmp.Width) / 2, 0), Math.Max((this.Height - mainBmp.Height) / 2, 0), mainBmp.Width, mainBmp.Height);
            Rectangle src = new Rectangle();
            src = e.ClipRectangle;
            src.Offset(-Math.Max((this.ClientRectangle.Width - mainBmp.Width) / 2, 0), -Math.Max((this.ClientRectangle.Height - mainBmp.Height) / 2, 0));
            e.Graphics.DrawImage(mainBmp, e.ClipRectangle, src, GraphicsUnit.Pixel);
            //e.Graphics.DrawImage(mainBmp, Math.Max((this.Width - mainBmp.Width) / 2, 0), Math.Max((this.Height - mainBmp.Height) / 2, 0), 9, 9);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (mainBmp != null)
            {
                mainBmp.Dispose();
                mainBmp = null;
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
                r = this.panel1.DisplayRectangle;
                r.Offset(this.panel1.Location);
                r.Offset(-Math.Max((this.Width - mainBmp.Width) / 2, 0), -Math.Max((this.Height - mainBmp.Height) / 2, 0));
                double myMaxX = minX + r.Right * XInc;
                double myMinX = minX + r.X * XInc;
                double myMaxY = minY + r.Bottom * YInc;
                double myMinY = minY + r.Y * YInc;
                //XInc = (double)(maxX - minX) / (mainBmp.Width - 1);
                //YInc = (double)(maxY - minY) / (mainBmp.Height - 1);

                //createMandlebrotImage();
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
                    Application.Exit();
                    break;
                    
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
        //            maxX = minX + r.Right * XInc;
        //            minX = minX + r.X * XInc;
        //            maxY = minY + r.Bottom * YInc;
        //            minY = minY + r.Y * YInc;
        //            XInc = (double)(maxX - minX) / (mainBmp.Width - 1);
        //            YInc = (double)(maxY - minY) / (mainBmp.Height - 1);

        //            //createMandlebrotImage();
        //            CreateMandelbrotImageThreads();

        //            //this.Invalidate();
        //        }
        //    }

    }
}
