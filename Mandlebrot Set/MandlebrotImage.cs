using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using FloatType = System.Quadruple;
//using FloatType = System.Double;


namespace Mandlebrot_Set
{
    class MandlebrotImage
    {
        public myRect mandRect
        { get; set; }
        public Bitmap mainBmp = null;
    }
}
