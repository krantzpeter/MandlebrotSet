using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using FloatType = System.Double;


namespace Mandlebrot_Set
{
    class MandlebrotImage
    {
        public FloatType minX;
        public FloatType maxX;
        public FloatType minY;
        public FloatType maxY;
        public Bitmap mainBmp = null;
    }
}
