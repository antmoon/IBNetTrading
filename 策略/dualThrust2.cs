using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    /// <summary>
    /// 300个tick，最高-最低，乘以K，得到range然后用最后一个tick加减range，作为STP价格，发送出去；有成交后，浮盈加仓；跟踪止损为（最高tick+成本价格）/2-range/2
    /// </summary>
    class dualThrust2 : TradingInterface
    {
        double K = 0.15;
        private Dictionary<Contract, double> buySellRange = new Dictionary<Contract, double>();
        Dictionary<Contract, Dictionary<DateTime, bar>> barsList = new Dictionary<Contract, Dictionary<DateTime, bar>>();   

        Order buyOrder = null;
        Order sellOrder = null;

        Dictionary<Contract, int> buyorderid = new Dictionary<Contract, int>();
        Dictionary<Contract, int> sellorderid = new Dictionary<Contract, int>();

        private Dictionary<Contract, double> buyPrice = new Dictionary<Contract, double>();
        private Dictionary<Contract, double> sellPrice = new Dictionary<Contract, double>();
      
        int barsMinute;

        public dualThrust2(IBClient ibclient,int minute) : base(ibclient)
        {
            barsMinute = minute;           
        }

        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            buySellRange.Add(contract, new double());            
            buyPrice.Add(contract, new double());
            sellPrice.Add(contract, new double());
            buyorderid.Add(contract, 0);
            sellorderid.Add(contract, 0);
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
            DateTime currentBarTime = new DateTime(DateTime.Now.Year,DateTime.Now.Month,DateTime.Now.Day,DateTime.Now.Hour, DateTime.Now.Minute - DateTime.Now.Minute % barsMinute, 0);            

            if (!barsList[contract].ContainsKey(currentBarTime))//第一个价格，open
            {
                //不包含只有两种情况，此时这是第一个，或者是新bar的开端
                //如果不是第一个，预示着上一个bar的结束
                if (barsList[contract].Count>0)
                {
                    this.onCandle(contract);
                }

                barsList[contract].Add(currentBarTime, new bar(currentPrice, currentPrice, currentPrice, currentPrice, currentPrice, currentPrice));                               
            }
            else if(barsList[contract].ContainsKey(currentBarTime))
            {
                barsList[contract][currentBarTime].high = Math.Max(barsList[contract][currentBarTime].high,currentPrice);
                barsList[contract][currentBarTime].low =Math.Min(currentPrice, barsList[contract][currentBarTime].low);
                barsList[contract][currentBarTime].close = currentPrice;
            }            

        }
        public override void onCandle(Contract contract)
        {
            if (!barsList.ContainsKey(contract))
                return;
            //测试            
            if (buyorderid[contract]!=0&&OpenOrderID[contract].Contains(buyorderid[contract]))
            {
                CancelOrder(buyorderid[contract]);
            }
            if (sellorderid[contract]!=0&& OpenOrderID[contract].Contains(sellorderid[contract]))
            {
                CancelOrder(sellorderid[contract]);
            }
            buySellRange[contract] = (barsList[contract].Last().Value.high - barsList[contract].Last().Value.low) * K;
            Console.WriteLine(contract.Symbol + " Last bar time:" + barsList[contract].Last().Key + ",Open:" + barsList[contract].Last().Value.open + ",close:" + barsList[contract].Last().Value.close + ",high:" + barsList[contract].Last().Value.high + ",low:" + barsList[contract].Last().Value.low+",buySellRange:"+buySellRange[contract]);
            buyPrice[contract] = contractPrice[contract].Last() + buySellRange[contract];
            sellPrice[contract] = contractPrice[contract].Last() - buySellRange[contract];

            if (contractPosition[contract].holding == 0)
            {
                buyOrder = new Order();
                buyOrder.Action = "BUY";
                buyOrder.TotalQuantity = 100;
                buyOrder.OrderType = "STP";//MIT?
                buyOrder.Transmit = true;
                buyOrder.AuxPrice = Math.Round(buyPrice[contract], 2);
                buyOrder.OutsideRth = true;
                buyOrder.Hidden = true;                
                buyorderid[contract] = createOrderId();
                ibclient.ClientSocket.placeOrder(buyorderid[contract], contract, buyOrder);
            }
            else if(contractPosition[contract].holding > 0)
            {
                sellOrder = new Order();
                sellOrder.Action = "SELL";
                sellOrder.TotalQuantity = 100+ contractPosition[contract].holding;
                sellOrder.OrderType = "STP";
                sellOrder.Transmit = true;
                sellOrder.AuxPrice = Math.Round(sellPrice[contract], 2);
                sellOrder.Hidden = true;
                sellOrder.OutsideRth = true;
                sellorderid[contract] = createOrderId();
                ibclient.ClientSocket.placeOrder(sellorderid[contract], contract, sellOrder);
            }
            else if (contractPosition[contract].holding < 0)
            {
                buyOrder = new Order();
                buyOrder.Action = "BUY";
                buyOrder.TotalQuantity = 100+Math.Abs(contractPosition[contract].holding);
                buyOrder.OrderType = "STP";//MIT?
                buyOrder.Transmit = true;
                buyOrder.AuxPrice = Math.Round(buyPrice[contract], 2);
                buyOrder.OutsideRth = true;
                buyOrder.Hidden = true;
                buyorderid[contract] = createOrderId();
                ibclient.ClientSocket.placeOrder(buyorderid[contract], contract, buyOrder);
            }

        }
             
    }
}
