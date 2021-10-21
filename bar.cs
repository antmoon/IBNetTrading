using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBNetTrading
{
    class bar
    {
        public bar(double h,double l,double o,double c,double v,double wapPrice)
        {
            high = h;
            low = l;
            open = o;
            close = c;
            volume = v;
        }
        public double high { get; set; }
        public double low { get; set; }
        public double open { get; set; }
        public double close { get; set; }
        public double volume { get; set; }
        public double TickNumber { get; set; }
        public double wap { get; set; }
        public DateTime barTime;
    }
}
