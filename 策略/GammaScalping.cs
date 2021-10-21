using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class GammaScalping:TradingInterface
    {
       // List<Contract> OptionChain = new List<Contract>();
        public Contract callToTradeToday;
        public Contract underlying;
        public Contract putToTradeToday;

        public int underlyingConid;//用在reqSecDefOptParams中

        HashSet<string> ExpireDate = new HashSet<string>();
        HashSet<double> allStrikes = new HashSet<double>();

        string mostRecentExpireDate;//存储最近的到期日
        double ATMstrikePrice;
        // double 

        double callDelta;

        public GammaScalping(IBClient ibclient) :base(ibclient)
        {
            fillEvent += onFill;
        }
        
        public void reqSecDefOptParams(Contract contract)
        {            
            ibclient.ClientSocket.reqSecDefOptParams(requestId[contract], contract.Symbol, "", "STK", underlyingConid);
        }
        public void reqOptionSummary(Contract contract)
        {
            ibclient.ClientSocket.reqContractDetails(requestId[contract], contract);            
        }
        
        public override void SecurityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
        {
           // Console.WriteLine("Security Definition Option Parameter. Reqest: {0}, Exchange: {1}, Undrelying contract id: {2}, Trading class: {3}, Multiplier: {4}, Expirations: {5}, Strikes: {6}",
             //                 reqId, exchange, underlyingConId, tradingClass, multiplier, string.Join(", ", expirations), string.Join(", ", strikes));

            allStrikes.UnionWith(strikes);
            ExpireDate.UnionWith(expirations);
        }
        public override void SecurityDefinitionOptionParameterEnd(int reqId)
        {

            mostRecentExpireDate = ExpireDate.Min();//目前最近的到期日
            Console.WriteLine("最近到期日："+mostRecentExpireDate);
            double nowPrice = contractPrice[underlying].Last();

            var higherStrikes = from n in allStrikes
                          where n - nowPrice >= 0
                          select n;

            ATMstrikePrice = higherStrikes.Min();            

            Console.WriteLine("交易的Call Strike是:" + ATMstrikePrice);

            callToTradeToday = new Contract();
            callToTradeToday.Strike = ATMstrikePrice;
            callToTradeToday.Symbol = underlying.Symbol;
            callToTradeToday.SecType = "OPT";
            callToTradeToday.Right = "C";
            callToTradeToday.Currency = "USD";
            callToTradeToday.Exchange = "SMART";
            callToTradeToday.LastTradeDateOrContractMonth = mostRecentExpireDate;

            var lowerStrikes = from n in allStrikes
                               where n - nowPrice <= 0
                               select n;

            ATMstrikePrice = lowerStrikes.Max();

            putToTradeToday = new Contract();
            putToTradeToday.Strike = ATMstrikePrice;
            putToTradeToday.Symbol = underlying.Symbol;
            putToTradeToday.SecType = "OPT";
            putToTradeToday.Right = "P";
            putToTradeToday.Currency = "USD";
            putToTradeToday.Exchange = "SMART";
            putToTradeToday.LastTradeDateOrContractMonth = mostRecentExpireDate;
        }

        public override void ContractDetailsEnd(int reqId)
        {
            Console.WriteLine("ContractDETAILS END");
            /*
            double nearestPrice = OptionChain[0].Strike;
            for (int i=OptionChain.Count-1;i>=0;i--)
            {
                if (OptionChain[i].Strike-contractPrice[contractIDrecord[reqId]][0]<0)//去掉所有的价内期权
                {
                    OptionChain.RemoveAt(i);
                }
            }            
                
            int index = OptionChain.FindIndex(t => (t.Strike == OptionChain.Min(a => a.Strike)));//搜索了好多文档，总结出这个办法，findindex参数接受一个bool。解释，目的是找到目标的index,目标是contract，=>后面是一个bool值，表示目标应该符合什么要求
            Console.WriteLine("index:" + index);
            callToTradeToday = OptionChain[index];
            Console.WriteLine("Call to trade today is:" + callToTradeToday.Symbol + ",价格：" + callToTradeToday.Strike);
            */
        }

        public override void ContractDetails(int reqId, ContractDetails contractDetails)
        {
            underlyingConid = contractDetails.Summary.ConId;
            Console.WriteLine("Symbol:" + contractDetails.Summary.Symbol + ",ContractID:" + contractDetails.Summary.ConId);
            /*
            //将最近的到期日的期权遴选出来
            if (OptionChain.Count < 1)
                OptionChain.Add(contractDetails.Summary);
            else
            {
                for (int i=OptionChain.Count-1;i>=0;i--)
                {                   

                    if (contractDetails.Summary.LastTradeDateOrContractMonth.CompareTo(OptionChain[i].LastTradeDateOrContractMonth) <0)
                    {
                        OptionChain.RemoveAt(i);
                        OptionChain.Add(contractDetails.Summary);
                    }
                    else if(contractDetails.Summary.LastTradeDateOrContractMonth.CompareTo(OptionChain[i].LastTradeDateOrContractMonth)  == 0)
                    {
                        OptionChain.Add(contractDetails.Summary);
                    }
                }               
            }
            Console.WriteLine(OptionChain[0].Symbol + ",strike:" + OptionChain[0].Strike + ",lastTradingDay:" + OptionChain[0].LastTradeDateOrContractMonth);
            */
        }
        public override void TickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {
            
            Contract curContract = contractIDrecord[tickerId];
            if (field==12)//成交价
            {                
                if (!buyPlaced[curContract] && contractPosition[curContract].holding==0)
                {
                    Order buyCall = new Order();
                    buyCall.Action = "BUY";
                    // buyCall.OrderType = "LMT";
                    buyCall.OrderType = "MKT";
                    buyCall.TotalQuantity = 1;
                   // buyCall.LmtPrice =Math.Round(optPrice,2);
                    buyCall.Transmit = true;
                    Console.WriteLine("Lmt Price:" + buyCall.LmtPrice);
                    PlaceOrder(curContract, buyCall);
                    buyPlaced[curContract] = true;
                }
                else if (contractPosition[curContract].holding>0)
                {
                    if (contractPosition[underlying].holding == 0)
                    {
                        Order sellUnderlying = new Order();
                        sellUnderlying.Action = "SELL";
                        sellUnderlying.OrderType = "MKT";
                        sellUnderlying.TotalQuantity = Math.Round(100 * callDelta, 0);
                        sellUnderlying.Transmit = true;
                        PlaceOrder(underlying, sellUnderlying);
                    }
                    else if (contractPosition[underlying].holding < 0)
                    {
                        double oughtHoldQty = -100 * callDelta;
                        if (oughtHoldQty - contractPosition[underlying].holding > 10)
                        {
                            Order buyUnderLying = new Order();
                            buyUnderLying.Action = "BUY";
                            buyUnderLying.OrderType = "MKT";
                            buyUnderLying.TotalQuantity = oughtHoldQty - contractPosition[underlying].holding;
                            buyUnderLying.Transmit = true;
                            PlaceOrder(underlying, buyUnderLying);
                        }
                        else if (oughtHoldQty - contractPosition[underlying].holding < -10)
                        {
                            Order sellUnderlying = new Order();
                            sellUnderlying.Action = "SELL";
                            sellUnderlying.OrderType = "MKT";
                            sellUnderlying.TotalQuantity = contractPosition[underlying].holding - oughtHoldQty;
                            sellUnderlying.Transmit = true;
                            PlaceOrder(underlying, sellUnderlying);
                        }
                    }
                }
                callDelta = delta;
            }
        }
        void onFill(Contract contract)
        {
            
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            ibclient.ClientSocket.reqMarketDataType(2);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());//获取
        }
        
    }
}
