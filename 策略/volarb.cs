using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class volarb : TradingInterface
    {
        public volarb(IBClient ibclient, int tickerid, int barNumber = 6) : base(ibclient, tickerid)
        {
            histCompleteEvent += onHistPrice;
            bidEvent += onQuote;
            tradeEvent += onTick;
            fillEvent += onFill;
            FiveSecondsbarEvent += onBar;
            
        }
        private void onHistPrice(Contract contract)
        {

        }
        private void onQuote(Contract contract)
        {

        }
        private void onTick(Contract contract)
        {

        }
        private void onFill(Contract contract)
        {

        }
        private void onBar(Contract contract)
        {

        }

                
    }
}
