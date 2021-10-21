using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class SupportResistance : TradingInterface
    {//151 Trading strategies
        int barsMinute;
        Dictionary<Contract, Dictionary<DateTime, bar>> barsList = new Dictionary<Contract, Dictionary<DateTime, bar>>();
        Dictionary<Contract, double> C = new Dictionary<Contract, double>();
        Dictionary<Contract, double> R = new Dictionary<Contract, double>();
        Dictionary<Contract, double> S = new Dictionary<Contract, double>();

        public SupportResistance(IBClient ibclient, int minute) : base(ibclient)
        {
            barsMinute = minute;
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            C.Add(contract, new double());
            R.Add(contract, new double());
            S.Add(contract, new double());
            barsList.Add(contract, new Dictionary<DateTime, bar>());
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

            if (barsList[contract].Count>=2)
            {
                if (contractPosition[contract].holding < 0)
                {
                    if (currentPrice <= S[contract])
                    {
                        Order buy = new Order();
                        buy.Action = "BUY";
                        buy.TotalQuantity = Math.Abs(contractPosition[contract].holding);
                        buy.Transmit = true;
                        buy.OrderType = "LMT";
                        buy.Hidden = true;
                        buy.OutsideRth = true;
                        buy.Tif = "IOC";
                        buy.LmtPrice = Math.Round(currentPrice, 2);
                        PlaceOrder(contract, buy);
                        Console.WriteLine(DateTime.Now + " Scalping RealTime Bar平仓空单，" + contract.Symbol + ",价格是：" + buy.LmtPrice);
                    }
                }
                else if (contractPosition[contract].holding > 0)
                {
                    if (currentPrice >= R[contract])
                    {
                        Order sellOrder = new Order();
                        sellOrder.Action = "SELL";
                        sellOrder.TotalQuantity = contractPosition[contract].holding;
                        sellOrder.OrderType = "LMT";
                        sellOrder.Transmit = true;
                        sellOrder.LmtPrice = Math.Round(currentPrice, 2);
                        sellOrder.Hidden = true;
                        sellOrder.OutsideRth = true;
                        sellOrder.Tif = "IOC";
                        PlaceOrder(contract, sellOrder);
                        Console.WriteLine(DateTime.Now + contract.Symbol + ",平仓多单，价格:" + sellOrder.LmtPrice);
                    }
                }
                else if (contractPosition[contract].holding == 0)
                {
                    if (currentPrice > C[contract])
                    {
                        Order buy = new Order();
                        buy.Action = "BUY";
                        buy.TotalQuantity = 200;
                        buy.Transmit = true;
                        buy.OrderType = "LMT";
                        buy.Hidden = true;
                        buy.OutsideRth = true;
                        buy.Tif = "IOC";
                        buy.LmtPrice = Math.Round(currentPrice, 2);
                        PlaceOrder(contract, buy);
                    }
                    else if (currentPrice < C[contract])
                    {
                        Order sellOrder = new Order();
                        sellOrder.Action = "SELL";
                        sellOrder.TotalQuantity = 200;
                        sellOrder.OrderType = "LMT";
                        sellOrder.Transmit = true;
                        sellOrder.LmtPrice = Math.Round(currentPrice, 2);
                        sellOrder.Hidden = true;
                        sellOrder.OutsideRth = true;
                        sellOrder.Tif = "IOC";
                        PlaceOrder(contract, sellOrder);
                    }
                }
            }
        }
        public override void onCandle(Contract contract)
        {
            if (!barsList.ContainsKey(contract))
                return;

            double curPrice = contractPrice[contract].Last();
            double Highest = barsList[contract].Last().Value.high;
            double Lowest = barsList[contract].Last().Value.low;

            C[contract] = (curPrice + Highest + Lowest) * 0.33333;
            R[contract] = C[contract] * 2 - Lowest;
            S[contract] = C[contract] * 2 - Highest;

            Console.WriteLine(contract.Symbol + " Last bar time:" + barsList[contract].Last().Key + ",Open:" + barsList[contract].Last().Value.open + ",close:" + barsList[contract].Last().Value.close + ",high:" + barsList[contract].Last().Value.high + ",low:" + barsList[contract].Last().Value.low );


        }

    }
}

