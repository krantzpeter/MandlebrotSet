
//#define debugPaint
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
using AviFile;

namespace Mandlebrot_Set
{

    public partial class Form1 : Form
    {
        // Thread info array.
        //private ArrayList tiArrayList;
        //private static int maxThreads = 4;
        public Point mainBmpLocationInForm
            { get; set; } = new Point();

        private bool showZoomFrames = true; // True if each frame you zoom into should be drawn on the window.
        private MandImage dispMandImage = new MandImage();

        private void UpdateMainBmpLocationInForm()
        {
            mainBmpLocationInForm = new Point(
                Math.Max((ClientRectangle.Width - dispMandImage.MainBmp.Width) / 2, 0),
                Math.Max((ClientRectangle.Height - dispMandImage.MainBmp.Height) / 2, 0)
            );
        }

        private Rectangle dragBoxFromMouseDown = Rectangle.Empty;
        private bool dragActive = false;
        private Point dragMouseDownPoint = Point.Empty;

        private ArrayList mainBmpImageList = new ArrayList(); // Used to contain list of MandImage values that contain the image of each zoom level and associated Mandlebrot X and Y min and max values.
        private const int maxImages = 40;
        private int imageListDisplayedElCount = 0;

        public Form1()
        {
            InitializeComponent();
            //tiArrayList = new ArrayList();
            // Do setup tasks.
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
            using (Graphics g = Graphics.FromImage(dispMandImage.MainBmp))
                {
                    g.Clear(BackColor);
                }

            this.panel1.BackColor = Color.FromKnownColor(KnownColor.Transparent);

            // Enable timer to check whether threads are finished.
            //timer1.Enabled = true;

            //createMandlebortImage();
            UseWaitCursor = true;

            MandImage.BackColor = BackColor;

            dispMandImage.CalcMandelbrotImage(BitmapSectionCalcFinished, BitmapCalcFinshed);

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
        /// Adds the Form's MainBmp and associated dispMandImage.MandRect.X, dispMandImage.MandRect.Right, dispMandImage.MandRect.Y and dispMandImage.MandRect.Bottom values to a stack implemented as ArrayList mainBmpImageList.
        /// </summary>
        private void pushBitmap()
        {
            // If adding an element back to the middle of the stack then clear all remaining elemments.
            if (imageListDisplayedElCount < mainBmpImageList.Count)
            {
                // Remove any trailing elements.
                mainBmpImageList.RemoveRange(imageListDisplayedElCount, mainBmpImageList.Count - imageListDisplayedElCount);

            }

            // If too many elements then remove one from the stack bottom and continue.
            if (mainBmpImageList.Count >= maxImages)
            {
                // Remove bottom element
                mainBmpImageList.RemoveAt(0);

                // Decrement count to adjust "top" element in stack for removed element.
                imageListDisplayedElCount--;
            }

            // Now create another element for the current bitmap at the end.
            MandImage i = new MandImage();
            i.CopyFrom(dispMandImage);
            mainBmpImageList.Add(i);

            // Update the count of the last element in the stack.
            imageListDisplayedElCount++;
        }

        /// <summary>
        /// Update MainBmp and associated dispMandImage.MandRect.X, dispMandImage.MandRect.Right, dispMandImage.MandRect.Y and dispMandImage.MandRect.Bottom values from the "stack top" in ArrayList mainBmpImageList.
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
            MandImage i = (MandImage)mainBmpImageList[imageListDisplayedElCount-1];
            dispMandImage.CopyFrom(i);

            // Indicate that pop succeeded.
            return true;
        }

        /// <summary>
        /// Gets the next image in the stack (mainBmpImageLIst) and updates MainBmp and associated dispMandImage.MandRect.X, dispMandImage.MandRect.Right, dispMandImage.MandRect.Y and dispMandImage.MandRect.Bottom values from it.
        /// </summary>
        /// <returns>True if there was a next image otherwise false</returns>
        private bool nextBitmap()
        {
            if (imageListDisplayedElCount >= mainBmpImageList.Count)
            {
                // There is no element to return so indicate this.
                return false;
            }

            // There is a next element so update MainBmp (etc) from it.
            dispMandImage.CopyFrom((MandImage)mainBmpImageList[imageListDisplayedElCount]);

            // Increment count in line with count of new "top" element in stack.
            imageListDisplayedElCount++;

            // Retrieve a new element so indicate success.
            return true;
        }

        /// <summary>
        /// Called when this section of the form's MandImage has been calculated so that the form can invalidate this section
        /// and thus the form will know to paint it.
        /// </summary>
        /// <param name="updateRect"> The Rectangle with the coordinates in the image of the section to be updated (NB: not window coords which are offset by the images location in the window) </param>
        private void BitmapSectionCalcFinished(Rectangle updateRect)
        {
            // Shift this image section by the offset of the location of the image in the form.
            updateRect.Offset(mainBmpLocationInForm);
            Invalidate(updateRect);
        }

        private void BitmapCalcFinshed(MandImage updatedMandImage)
        {
            UseWaitCursor = false;
            Cursor = Cursors.Default;
            pushBitmap();
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

            //pictureBox1.Image = MainBmp;
            //pictureBox1.Invalidate();

            //e.ClipRectangle
            //e.Graphics.Clear(Color.Black);
            //e.Graphics.DrawImage(MainBmp, Math.Max((this.Width - MainBmp.Width) / 2, 0), Math.Max((this.Height - MainBmp.Height) / 2, 0), MainBmp.Width, MainBmp.Height);
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
                dest.Width = Math.Min(e.ClipRectangle.Width - (mainBmpLocationInForm.X - e.ClipRectangle.X), MandImage.bitmapWidth);
                src.Width = dest.Width;
            }
            else
            {
                // Trying to paint a portion of the screen which requires no margin as it's into a part of the bitmap.
                src.X = e.ClipRectangle.X - mainBmpLocationInForm.X;
                src.Width = Math.Min(e.ClipRectangle.Width, MandImage.bitmapWidth - src.X);
                dest.X = e.ClipRectangle.X;
                dest.Width = src.Width;
            }

            if (e.ClipRectangle.Y < mainBmpLocationInForm.Y)
            {
                // Trying to paint a portion of the screen which requires a margin as the bmp is smaller than the form.
                src.Y = 0;
                dest.Y = mainBmpLocationInForm.Y;
                //src.Height = e.ClipRectangle.Height;
                dest.Height = Math.Min(e.ClipRectangle.Height - (mainBmpLocationInForm.Y - e.ClipRectangle.Y), MandImage.bitmapHeight);
                src.Height = dest.Height;
            }
            else
            {
                // Trying to paint a portion of the screen which requires no margin as it's into a part of the bitmap.
                src.Y = e.ClipRectangle.Y - mainBmpLocationInForm.Y;
                src.Height = Math.Min(e.ClipRectangle.Height, MandImage.bitmapHeight - src.Y);
                dest.Y = e.ClipRectangle.Y;
                dest.Height = src.Height;
            }

            //src = e.ClipRectangle;
            //src.Offset(-mainBmpLocationInForm.X, -mainBmpLocationInForm.Y);
            e.Graphics.DrawImage(dispMandImage.MainBmp, dest, src, GraphicsUnit.Pixel);

            if (showZoomFrames)
            {

                // Draw zoom rectangles for remaining images in the stack.
                int zoomX, zoomY, zoomWidth, zoomHeight;
                Pen p = new Pen(Color.White);
                Rectangle zoomRect;
                FloatType zoomPixelXInc = MandImage.bitmapWidth / (dispMandImage.MandRect.Width);
                FloatType zoomPixelYInc = MandImage.bitmapHeight / (dispMandImage.MandRect.Height);

                    for (int i = imageListDisplayedElCount; i < mainBmpImageList.Count; i++)
                    {
                        // Find display rectange for this image
                        MandImage m = (MandImage)mainBmpImageList[i];

                        zoomWidth = (int)(m.MandRect.Width * zoomPixelXInc);
                        zoomHeight = (int)(m.MandRect.Height * zoomPixelYInc);
                        zoomX = mainBmpLocationInForm.X + (int)((m.MandRect.X - dispMandImage.MandRect.X) * zoomPixelXInc);
                        zoomY = mainBmpLocationInForm.Y + (int)((m.MandRect.Y - dispMandImage.MandRect.Y) * zoomPixelYInc);

                        zoomRect = new Rectangle(zoomX, zoomY, zoomWidth, zoomHeight);
                        e.Graphics.DrawRectangle(p, zoomRect);
                    }

                // Dispose of pen.
                p.Dispose();

            }
            //e.Graphics.DrawImage(MainBmp, Math.Max((this.Width - MainBmp.Width) / 2, 0), Math.Max((this.Height - MainBmp.Height) / 2, 0), 9, 9);

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


            // Dispose of bitmaps in image stack.
            for (int i = 0; i < mainBmpImageList.Count; i++)
            {
                // Remove the bitmap we're abandoning
                ((MandImage)mainBmpImageList[i]).Dispose();
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
            if (!MandImage.imageCalcInProgress && (e.Button & MouseButtons.Left) == MouseButtons.Left)
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
                    this.panel1.Width = (int)(this.panel1.Height * MandImage.aspectRatio);
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
                r = panel1.Bounds;
                //r.Offset(this.panel1.Location);
                r.Offset(-mainBmpLocationInForm.X, -mainBmpLocationInForm.Y);
                //r.Offset(-Math.Max((this.Width - MainBmp.Width) / 2, 0), -Math.Max((this.Height - MainBmp.Height) / 2, 0));

                // Adjust bounding rectangle to "Zoom in".
                dispMandImage.ZoomMandRect(r);

                //createMandlebrotImage();
                // Switch to Quadruple instead of double values when (Math.Abs(XInc)<8.0E-16 || Math.Abs(YInc)<8.0E-16)
                dispMandImage.CalcMandelbrotImage(BitmapSectionCalcFinished, BitmapCalcFinshed);

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

        //            Graphics mainBmpGraphics = Graphics.FromImage(MainBmp);
        //            //mainBmpGraphics.DrawImage(ti.bmp, new Rectangle(0, 0, MandImage.bitmapWidth, MandImage.bitmapHeight / 2), new Rectangle(0, 0, MandImage.bitmapWidth, MandImage.bitmapHeight / 2), GraphicsUnit.Pixel);
        //            MainBmp = ti.bmpSection;
        //            this.Invalidate();
        //            //pictureBox1.Image = MainBmp;
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
            MandImage.CancelThreads();

            //MessageBox.Show("Counted to " + c + "during thread exit wait.");
            UseWaitCursor = oldUseWaitCursor;
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {

            switch (keyData)
            {
                case Keys.Escape:
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
                    return true;    // indicate that you handled this keystroke

                // case (char)Keys.Up:
                case Keys.O:
                case Keys.Left:
                    // Request to zoom out.
                    if (MandImage.imageCalcInProgress)
                    {
                        // Ignore keystroke as calculating.
                        Console.Beep();
                    }
                    else
                    {
                        if (popBitmap())
                        {
                            // Succeeded in getting a new bitmap off the stack so display it.
                            Invalidate(new Rectangle(mainBmpLocationInForm, new Size(MandImage.bitmapWidth, MandImage.bitmapHeight)));
                            //Refresh();
                        }
                        else
                        {
                            // No bitmap to pop so beep.
                            Console.Beep();
                        }
                    }
                    return true;    // indicate that you handled this keystroke

                //case (char)Keys.Down:
                case Keys.I:
                case Keys.Right:
                    // Request to zoom in.
                    if (MandImage.imageCalcInProgress)
                    {
                        // Ignore keystroke as calculating.
                        Console.Beep();
                    }
                    else
                    {

                        if (nextBitmap())
                        {
                            // Succeeded in getting a new bitmap off the stack so display it.
                            Invalidate(new Rectangle(mainBmpLocationInForm, new Size(MandImage.bitmapWidth, MandImage.bitmapHeight)));
                            //Refresh();
                        }
                        else
                        {
                            // No bitmap to go to so beep.
                            Console.Beep();
                        }
                    }
                    return true;    // indicate that you handled this keystroke

                case Keys.W:
                    // Write images to file
                    if (mainBmpImageList.Count <= 0)
                    {
                        // No images to write.
                        Console.Beep();
                        return true;    // indicate that you handled this keystroke
                    }

                    // Write images in arraylist
                    string newFileName = @"E:\UP\Peter4\Documents\Visual Studio 2015\Projects\Mandlebrot Set\Mandlebrot Set\test.avi";
                    AviManager newFile = new AviManager(newFileName, false);
                    try
                    {
                        Bitmap bmp = ((MandImage)(mainBmpImageList[0])).MainBmp;

                        VideoStream newStream = newFile.AddVideoStream(false, 1, bmp);
                        for (int n = 1; n < mainBmpImageList.Count; n++)
                        {
                            bmp = ((MandImage)(mainBmpImageList[n])).MainBmp;
                            newStream.AddFrame(bmp);
                        }
                        newFile.Close();
                    }
                    catch (Exception ex)
                    {
                        newFile.Close();
                        throw ex;
                    }
                    return true;    // indicate that you handled this keystroke

                default:
                    // Call the base class
                    return base.ProcessCmdKey(ref msg, keyData);

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
        // ##################################
        // PK 14/02/2017: I think there is a simple GetPIxel as part of the Bitmap object so this is probably old code that is unnecessary!!!!!!!!!!!!
        // ##################################
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
        //            r.Offset(-Math.Max((this.Width - MainBmp.Width) / 2, 0), -Math.Max((this.Height - MainBmp.Height) / 2, 0));
        //            dispMandImage.MandRect.Right = dispMandImage.MandRect.X + r.Right * XInc;
        //            dispMandImage.MandRect.X = dispMandImage.MandRect.X + r.X * XInc;
        //            dispMandImage.MandRect.Bottom = dispMandImage.MandRect.Y + r.Bottom * YInc;
        //            dispMandImage.MandRect.Y = dispMandImage.MandRect.Y + r.Y * YInc;
        //            XInc = (FloatType)(dispMandImage.MandRect.Right - dispMandImage.MandRect.X) / (MainBmp.Width - 1);
        //            YInc = (FloatType)(dispMandImage.MandRect.Bottom - dispMandImage.MandRect.Y) / (MainBmp.Height - 1);

        //            //createMandlebrotImage();
        //            CalcMandelbrotImage();

        //            //this.Invalidate();
        //        }
        //    }

    }
}
