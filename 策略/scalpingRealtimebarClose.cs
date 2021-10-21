using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class scalpingRealtimebarClose : TradingInterface
    {//根据ES scalping strategy

        Dictionary<Contract, Dictionary<DateTime, bar>> barsList = new Dictionary<Contract, Dictionary<DateTime, bar>>();

        Dictionary<Contract, List<double>> high = new Dictionary<Contract, List<double>>();
        Dictionary<Contract, List<double>> low = new Dictionary<Contract, List<double>>();
        Dictionary<Contract, List<double>> close = new Dictionary<Contract, List<double>>();

        int barsMinute;
        public scalpingRealtimebarClose(IBClient ibclient, int minute) : base(ibclient)
        {
            barsMinute = minute;
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
          
            barsList.Add(contract, new Dictionary<DateTime, bar>());
            high.Add(contract, new List<double>());
            low.Add(contract, new List<double>());
            close.Add(contract, new List<double>());

            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
        }
        protected override void OnRemovecontract(Contract contract)
        {
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }

        public override void OnTrade(Contract contract)
        {
            double currentPrice = contractPrice[contract].Last();
            DateTime currentBarTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute - DateTime.Now.Minute % barsMinute, 0);

            if (!barsList[contract].ContainsKey(currentBarTime))//第一个价格，open
            {
                //不包含只有两种情况，此时这是第一个，或者是新bar的开端
                //如果不是第一个，预示着上一个bar的结束
                if (barsList[contract].Count > 0)
                {
                    this.onCandle(contract);
                }

                barsList[contract].Add(currentBarTime, new bar(currentPrice, currentPrice, currentPrice, currentPrice, currentPrice, currentPrice));

            }
            else if (barsList[contract].ContainsKey(currentBarTime))
            {
                barsList[contract][currentBarTime].high = Math.Max(barsList[contract][currentBarTime].high, currentPrice);
                barsList[contract][currentBarTime].low = Math.Min(currentPrice, barsList[contract][currentBarTime].low);
                barsList[contract][currentBarTime].close = currentPrice;
            }

        }

        public override void onCandle(Contract contract)
        {
            if (!barsList.ContainsKey(contract))
                return;

            high[contract].Add(barsList[contract].Last().Value.high);
            low[contract].Add(barsList[contract].Last().Value.low);
            close[contract].Add(barsList[contract].Last().Value.close);

            int count = close[contract].Count;
            double curPrice = contractPrice[contract].Last();
            if (count>1)
            {
                if (close[contract][count-1]>close[contract][count-2] && high[contract][count-1]-low[contract][count-1]>high[contract][count-2]-low[contract][count-2]&&contractPosition[contract].holding<=0)
                {
                    Order buy = new Order();
                    buy.Action = "BUY";
                    buy.TotalQuantity = 200;
                    buy.Transmit = true;
                    buy.OrderType = "LMT";
                    buy.Hidden = true;
                    buy.OutsideRth = true;
                    buy.Tif = "IOC";
                    buy.LmtPrice = Math.Round(curPrice, 2);
                    PlaceOrder(contract, buy);
                }
                else if (close[contract][count - 1] < close[contract][count - 2] && high[contract][count - 1] - low[contract][count - 1] > high[contract][count - 2] - low[contract][count - 2] && contractPosition[contract].holding >= 0)
                {
                    Order sellOrder = new Order();
                    sellOrder.Action = "SELL";
                    sellOrder.TotalQuantity = 200;
                    sellOrder.OrderType = "LMT";
                    sellOrder.Transmit = true;
                    sellOrder.LmtPrice = Math.Round(curPrice, 2);
                    sellOrder.Hidden = true;
                    sellOrder.OutsideRth = true;
                    sellOrder.Tif = "IOC";
                    PlaceOrder(contract, sellOrder);
                }
            }
        }

    }
}
