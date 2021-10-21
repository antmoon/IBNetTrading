using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{

    class ScalpingHFT : TradingInterface
    {//高频交易程序，Buy signal appears when Ask Price is faster than Bid Price by a predefined number of points；Sell signal appears when Bid price is faster than Ask Price by a predefined number of points.
     //https://www.forexfactory.com/thread/578029-tick-chart-scalper
        public ScalpingHFT(IBClient ibclient) : base(ibclient)
        {
           
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
        }
        protected override void OnRemovecontract(Contract contract)
        {
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }
        public override void OnQuoteSize(Contract contract)
        {
            
            if (contractBid[contract].Count < 4 || contractAsk[contract].Count < 4)
                return;

            int bidIndex = contractBid[contract].Count - 1;
            int askIndex = contractAsk[contract].Count - 1;

            double bidChangeRate = contractBid[contract][bidIndex] + contractBid[contract][bidIndex - 2] - contractBid[contract][bidIndex - 1] * 2;
            double askChangeRate = contractAsk[contract][askIndex] + contractAsk[contract][askIndex - 2] - contractAsk[contract][askIndex - 1] * 2;

            if (bidChangeRate > 0 && askChangeRate > bidChangeRate)
            {
                Order buy = new Order();
                buy.Action = "BUY";
                buy.TotalQuantity = 200;
                buy.Transmit = true;
                buy.OrderType = "LMT";
                buy.Hidden = true;
                buy.Tif = "IOC";
                buy.LmtPrice = Math.Round((contractBid[contract][bidIndex] + contractAsk[contract][askIndex]) / 2, 2);
                PlaceOrder(contract, buy);
                Console.WriteLine(contract.Symbol + DateTime.Now + ",buyAt:" + buy.LmtPrice);
            }
            else if (askChangeRate < 0 || askChangeRate > bidChangeRate)
            {
                Order sellOrder = new Order();
                sellOrder.Action = "SELL";
                sellOrder.TotalQuantity = 200;
                sellOrder.OrderType = "LMT";
                sellOrder.Transmit = true;
                sellOrder.LmtPrice = Math.Round((contractBid[contract][bidIndex] + contractAsk[contract][askIndex]) / 2, 2);
                sellOrder.Hidden = true;
                sellOrder.Tif = "IOC";
                PlaceOrder(contract, sellOrder);
                Console.WriteLine(contract.Symbol + DateTime.Now + ",sellAt:" + sellOrder.LmtPrice);
            }
        }

        public override void OnQuote(Contract contract)
        {
          

        }

    }
}
