using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class ShortGamma2_WithDirection : TradingInterface
    {
        public Dictionary<Contract, Contract> callToTradeToday;
        public Dictionary<Contract, Contract> putToTradeToday;


        //多品种的交易中，需要建立字典，事实上，其他所有字段都需要做成字典，Contract为key
        Dictionary<Contract, List<double>> callAlreadyTraded;
        Dictionary<Contract, List<double>> putAlreadyTraded;

        public Dictionary<Contract, int> underlyingConid;//用在reqSecDefOptParams中，IB交易系统中的ID，不是本程序赋予的ID

        Dictionary<Contract, HashSet<string>> ExpireDate;
        Dictionary<Contract, HashSet<double>> allStrikes;

        Dictionary<Contract, string> mostRecentExpireDate;//存储最近的到期日
        Dictionary<Contract, DateTime> mostRecentExpireTime;
        Dictionary<Contract, double> CallATMstrike;
        Dictionary<Contract, double> PutATMstrike;

        Dictionary<Contract, bool> optionReady;

        Dictionary<Contract, double> ImpliedVolatility;

        Dictionary<Contract, List<double>> OptionPrices;

        // DateTime NextDayInMiddleTwive = 

        public ShortGamma2_WithDirection(IBClient ibclient) : base(ibclient)
        {
            callToTradeToday = new Dictionary<Contract, Contract>();
            putToTradeToday = new Dictionary<Contract, Contract>();

            callAlreadyTraded = new Dictionary<Contract, List<double>>();
            putAlreadyTraded = new Dictionary<Contract, List<double>>();
            underlyingConid = new Dictionary<Contract, int>();
            ExpireDate = new Dictionary<Contract, HashSet<string>>();
            allStrikes = new Dictionary<Contract, HashSet<double>>();
            mostRecentExpireDate = new Dictionary<Contract, string>();
            mostRecentExpireTime = new Dictionary<Contract, DateTime>();
            CallATMstrike = new Dictionary<Contract, double>();
            PutATMstrike = new Dictionary<Contract, double>();

            optionReady = new Dictionary<Contract, bool>();
            ImpliedVolatility = new Dictionary<Contract, double>();
            OptionPrices = new Dictionary<Contract, List<double>>();
        }

        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);

            underlyingConid.Add(contract, new int());
            allStrikes.Add(contract, new HashSet<double>());
            ExpireDate.Add(contract, new HashSet<string>());
            mostRecentExpireDate.Add(contract, "");
            mostRecentExpireTime.Add(contract, new DateTime());
            optionReady.Add(contract, false);
            ImpliedVolatility.Add(contract, 0);
            CallATMstrike.Add(contract, 0);
            PutATMstrike.Add(contract, 0);
            callToTradeToday.Add(contract, new Contract());
            putToTradeToday.Add(contract, new Contract());
            callAlreadyTraded.Add(contract, new List<double>());
            putAlreadyTraded.Add(contract, new List<double>());

            ibclient.ClientSocket.reqMarketDataType(2);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "106", false, new List<TagValue>());//获取隐含波动率           
            reqOptionSummary(contract);
        }
        void AddOptionsContract(Contract contract)
        {
            
        }

        public void reqSecDefOptParams(Contract contract)
        {
            ibclient.ClientSocket.reqSecDefOptParams(requestId[contract], contract.Symbol, "", "STK", underlyingConid[contract]);
            //  ibclient.ClientSocket.reqSecDefOptParams(reqId[contract], contract.Symbol, "", "STK", underlyingsContractId[contract]);
        }
        public void reqOptionSummary(Contract contract)
        {
            ibclient.ClientSocket.reqContractDetails(requestId[contract], contract);
        }

        public override void SecurityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            allStrikes[contractIDrecord[reqId]].UnionWith(strikes);
            ExpireDate[contractIDrecord[reqId]].UnionWith(expirations);
        }
        public override void SecurityDefinitionOptionParameterEnd(int reqId)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            mostRecentExpireDate[contractIDrecord[reqId]] = ExpireDate[contractIDrecord[reqId]].Min();//目前最近的到期日 
            mostRecentExpireTime[contractIDrecord[reqId]] = DateTime.ParseExact(mostRecentExpireDate[contractIDrecord[reqId]], "yyyyMMdd", System.Globalization.CultureInfo.CurrentCulture);
            optionReady[contractIDrecord[reqId]] = true;//信号

        }

        public override void ContractDetails(int reqId, ContractDetails contractDetails)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            underlyingConid[contractIDrecord[reqId]] = contractDetails.Summary.ConId;
            // underlyingsContractId.Add(contractIDrecord[reqId], contractDetails.Summary.ConId);
            Console.WriteLine("Symbol:" + contractDetails.Summary.Symbol + ",ContractID:" + contractDetails.Summary.ConId);
            reqSecDefOptParams(contractIDrecord[reqId]);
        }        
        
        public override void OnTrade(Contract contract)
        {
            if (optionReady[contract] == true && ImpliedVolatility[contract] != 0D&&DateTime.Now<usStockCloseTime.CorrespondingChinaTime)//option链和隐含波动率都有了
            {
              //  TimeSpan timeSpan = mostRecentExpireTime[contract].AddDays(1.5).Subtract(DateTime.Now);
                TimeSpan timeSpan = usStockCloseTime.CorrespondingChinaTime.AddHours(8).Subtract(DateTime.Now);
                double Standarddeviation = ImpliedVolatility[contract] * Math.Sqrt(timeSpan.TotalDays / 365);
                double upperlimitPrice = contractPrice[contract].Last() * (1 + Standarddeviation); 
                double bottomlimitPrice = contractPrice[contract].Last() * (1 - Standarddeviation);

                Console.WriteLine("ImpliedVolatility：" + ImpliedVolatility);
                Console.WriteLine("upperlimitPrice：" + upperlimitPrice);
                Console.WriteLine("bottomlimitPrice：" + bottomlimitPrice);
                
                var higherStrikes = from n in allStrikes[contract]
                                    where n - upperlimitPrice >= 0
                                    select n;

                CallATMstrike[contract] = higherStrikes.Min();               
                                
                Console.WriteLine(DateTime.Now + contract.Symbol + ",要交易的call行权价是：" + CallATMstrike[contract] + ",本策略目前交易的行权价是:" + callToTradeToday[contract].Strike);//调试的目的

                if (callToTradeToday[contract].Strike != CallATMstrike[contract] || callToTradeToday[contract].Strike == 0D)//检查是否已经交易过，保证每个行权价只交易一次
                {
                    Contract calltosell = new Contract();
                    calltosell.Strike = CallATMstrike[contract];
                    calltosell.Symbol = contract.Symbol;
                    calltosell.SecType = "OPT";
                    calltosell.Right = "C";
                    calltosell.Currency = "USD";
                    calltosell.Exchange = "SMART";
                    calltosell.LastTradeDateOrContractMonth = mostRecentExpireDate[contract];

                    if (!callAlreadyTraded[contract].Contains(CallATMstrike[contract]) && contractPrice[contract].Last() < contractPrice[contract].First() && contractPrice[contract].Last() < contractPrice[contract].Average())//使用均线判断短期趋势，顺势卖
                    {//初始交易
                        base.OnAddcontract(calltosell);
                        if (contractPosition[calltosell].holding >= 0 && !sellPlaced[calltosell])
                        {
                            Order sellcallmarket = new Order();
                            sellcallmarket.Action = "SELL";
                            sellcallmarket.OrderType = "MKT";
                            sellcallmarket.TotalQuantity = 2;
                            sellcallmarket.Transmit = true;
                            PlaceOrder(calltosell, sellcallmarket);
                            sellPlaced[calltosell] = true;

                        }
                        callToTradeToday[contract] = calltosell;
                        callAlreadyTraded[contract].Add(CallATMstrike[contract]);

                        ibclient.ClientSocket.reqRealTimeBars(requestId[calltosell], calltosell, 5, "MIDPOINT", true, new List<TagValue>());//请求期权的realtimebar。期权没有tick数据
                        OptionPrices.Add(calltosell, new List<double>());
                    }
                    //后续交易，请求期权价格的realtime bar，求均线，发现价格越过平均价格时候，就平仓
                    
                    
                }
               
                var lowerStrikes = from n in allStrikes[contract]
                                   where n - bottomlimitPrice <= 0
                                   select n;

                PutATMstrike[contract] = lowerStrikes.Max();
                

                Console.WriteLine(DateTime.Now + contract.Symbol + ",要交易的put行权价是：" + PutATMstrike[contract] + ",本策略目前交易的行权价是:" + putToTradeToday[contract].Strike);//调试的目的
                if (putToTradeToday[contract].Strike != PutATMstrike[contract] || putToTradeToday[contract].Strike == 0D)
                {
                    Contract putToSell = new Contract();
                    putToSell.Strike = PutATMstrike[contract];
                    putToSell.Symbol = contract.Symbol;
                    putToSell.SecType = "OPT";
                    putToSell.Right = "P";
                    putToSell.Currency = "USD";
                    putToSell.Exchange = "SMART";
                    putToSell.LastTradeDateOrContractMonth = mostRecentExpireDate[contract];

                    if (!putAlreadyTraded[contract].Contains(PutATMstrike[contract]) && contractPrice[contract].Last() > contractPrice[contract].First() && contractPrice[contract].Last() > contractPrice[contract].Average())
                    {
                        base.OnAddcontract(putToSell);
                        if (contractPosition[putToSell].holding >= 0 && !sellPlaced[putToSell])
                        {
                            Order sellputmarket = new Order();
                            sellputmarket.Action = "SELL";
                            sellputmarket.OrderType = "MKT";
                            sellputmarket.TotalQuantity = 2;
                            sellputmarket.Transmit = true;
                            PlaceOrder(putToSell, sellputmarket);
                            sellPlaced[putToSell] = true;
                        }
                        putToTradeToday[contract] = putToSell;
                        putAlreadyTraded[contract].Add(PutATMstrike[contract]);

                        ibclient.ClientSocket.reqRealTimeBars(requestId[putToSell], putToSell, 5, "MIDPOINT", true, new List<TagValue>());//请求期权的realtimebar。期权没有tick数据
                        OptionPrices.Add(putToSell, new List<double>());
                    }

                }

            }

        }

        public override void RealtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count)
        {//根据期权的moving average，进行止损操作
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            Contract ticker = contractIDrecord[reqId];//根据reqID找到对应的contract

            OptionPrices[ticker].Add(close);
            if (OptionPrices[ticker].Count < 1000)
                return;

            if (contractPosition[ticker].holding<0 && close>OptionPrices[ticker].Average()&&close> contractPosition[ticker].averageCost*1.2)
            {//平仓
                Order buyCover = new Order();
                buyCover.Action = "BUY";
                buyCover.Transmit = true;
                buyCover.OrderType = "MKT";
                buyCover.TotalQuantity = Math.Abs(contractPosition[ticker].holding);
                if (buyPlaced[ticker]==false)
                {
                    PlaceOrder(ticker, buyCover);
                    buyPlaced[ticker] = true;
                }
            }
        }

        public override void TickGeneric(int tickerId, int field, double value)
        {            
            if (!contractIDrecord.ContainsKey(tickerId))
                return;
            if (field == 24)
            {
                ImpliedVolatility[contractIDrecord[tickerId]] = value;
                Console.WriteLine(contractIDrecord[tickerId].Symbol + " ImpliedVolatility:" + ImpliedVolatility);
            }

        }
        protected override void OnRemovecontract(Contract contract)
        {

        }
        protected override void TakeCareOfTime()
        {
            //什么也不做，收盘后不平仓。父类默认是平仓的。
        }
    }
}

