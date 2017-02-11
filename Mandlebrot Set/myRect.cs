using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FloatType = System.Double;

namespace Mandlebrot_Set
{
    public class myRect
    {
        public FloatType X { get; set; }
        public FloatType Y { get; set; }
        public FloatType Right { get; set; }
        public FloatType Bottom { get; set; }
        public FloatType Width { get { return Right - X; } set { Right = X + Width; } }
        public FloatType Height { get { return Bottom - Y ; } set { Bottom = Y + Height; } } 

    }
}
