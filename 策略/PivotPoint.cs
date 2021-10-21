using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class PivotPoint : TradingInterface
    {//个人设想的简单策略，根据昨天的高，低，收和现在的实时价格，计算一个平均数，大于就买进，小于就卖出。世界上没有什么暴利，昨天上涨，今天上涨的可能性也大，挣点上涨钱就是了。用python统计了下，发现这个假设不成立
        //如果这个策略可行，可以考虑做个变种，比如再加一个日内VWAP做趋势的过滤。
        //增加了一个20日均线，大方向上不能出现问题啊

        Dictionary<Contract, double> yClose = new Dictionary<Contract, double>();
        Dictionary<Contract, double> yHigh = new Dictionary<Contract, double>();
        Dictionary<Contract, double> yLow = new Dictionary<Contract, double>();

        Dictionary<Contract, List<double>> twentyDayCloses = new Dictionary<Contract, List<double>>();
        Dictionary<Contract, double> twentyDayAverage = new Dictionary<Contract, double>();

        public PivotPoint(IBClient ibclient) : base(ibclient)
        {
            
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            yClose.Add(contract, new double());
            yHigh.Add(contract, new double());
            yLow.Add(contract, new double());
            twentyDayCloses.Add(contract, new List<double>());
            twentyDayAverage.Add(contract, new double());

            String queryTime = usStockCloseTime.CorrespondingChinaTime.AddHours(-35).ToString("yyyyMMdd HH:mm:ss");
            ibclient.ClientSocket.reqHistoricalData(requestId[contract], contract, queryTime, "20 D", "1 day", "TRADES", 1, 1, null);

        }
        public override void HisticalData(int reqId, string date, double open, double high, double low, double close, int volume, int count, double WAP, bool hasGapsa)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            
            yClose[contractIDrecord[reqId]] = close;
            yHigh[contractIDrecord[reqId]] = high;
            yLow[contractIDrecord[reqId]] = low;
            twentyDayCloses[contractIDrecord[reqId]].Add(close);
            Console.WriteLine(date +" "+ contractIDrecord[reqId].Symbol + ", Open: " + open + ", High: " + yHigh[contractIDrecord[reqId]] + ", Low: " + yLow[contractIDrecord[reqId]] + ", Close: " + yClose[contractIDrecord[reqId]] + ", Volume: " + volume);

        }
        public override void HistoricalDataEnd(int reqId, string startDate, string endDate)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            twentyDayAverage[contractIDrecord[reqId]] = twentyDayCloses[contractIDrecord[reqId]].Average();

            Console.WriteLine(contractIDrecord[reqId].Symbol + "昨日收盘价为：" + yClose[contractIDrecord[reqId]] + ",昨日高为：" + yHigh[contractIDrecord[reqId]]+ "，20日均价为:" + twentyDayAverage[contractIDrecord[reqId]]);
            

            ibclient.ClientSocket.cancelHistoricalData(reqId);//得到数据后，取消请求
            ibclient.ClientSocket.reqMktData(reqId, contractIDrecord[reqId], "", false, new List<TagValue>());            
        }

        protected override void OnRemovecontract(Contract contract)
        {
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }

        public override void OnTrade(Contract contract)
        {
            double currentPrice = contractPrice[contract].Last();
            double DivideLine = (yLow[contract] + yHigh[contract] + yClose[contract]) * 0.3333;
            Console.WriteLine(contract.Symbol + " divide price:" + DivideLine+",while current trade price:"+currentPrice+",Current position:"+ contractPosition[contract].holding);

            if (contractPosition[contract].holding == 0)
            {//每天就交易一次，收盘平仓，就这样，看一段时间效果如何。日内平仓开仓总是赔钱。MD.
                if (currentPrice > DivideLine && currentPrice>twentyDayAverage[contract])
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
                    Console.WriteLine(DateTime.Now + "PivotPoint开多单，" + contract.Symbol + ",价格是：" + buy.LmtPrice);
                }
                if (currentPrice < DivideLine && currentPrice < twentyDayAverage[contract])
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
                    Console.WriteLine(DateTime.Now + contract.Symbol + ",PivotPoint开空单，价格是:" + sellOrder.LmtPrice);
                }
            }
                       

        }

    }
}


