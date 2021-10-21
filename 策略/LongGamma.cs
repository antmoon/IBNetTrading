using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class LongGamma : TradingInterface    
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

        public LongGamma(IBClient ibclient) : base(ibclient)
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

        public override void OnFill(Contract contract)
        {
        }
        public override void OnTrade(Contract contract)
        {
            if (optionReady[contract] == true && ImpliedVolatility[contract] != 0D && DateTime.Now < usStockCloseTime.CorrespondingChinaTime)//option链和隐含波动率都有了
            {
                //  TimeSpan timeSpan = mostRecentExpireTime[contract].AddDays(1.5).Subtract(DateTime.Now);
                TimeSpan timeSpan = usStockCloseTime.CorrespondingChinaTime.AddHours(8).Subtract(DateTime.Now);
                double Standarddeviation = ImpliedVolatility[contract] * Math.Sqrt(timeSpan.TotalDays / 365);
                double upperlimitPrice = contractPrice[contract].Last() * (1 + Standarddeviation);
                double bottomlimitPrice = contractPrice[contract].Last() * (1 - Standarddeviation);

                Console.WriteLine("ImpliedVolatility：" + ImpliedVolatility);
                Console.WriteLine("upperlimitPrice：" + upperlimitPrice);
                Console.WriteLine("bottomlimitPrice：" + bottomlimitPrice);
               
                //改为价内期权，买入
                var higherStrikes = from n in allStrikes[contract]
                                    where n - bottomlimitPrice <= 0
                                    select n;

                CallATMstrike[contract] = higherStrikes.Max();

                Console.WriteLine(DateTime.Now + contract.Symbol + ",要交易的call行权价是：" + CallATMstrike[contract] + ",本策略目前交易的行权价是:" + callToTradeToday[contract].Strike);//调试的目的

                if (callToTradeToday[contract].Strike != CallATMstrike[contract] || callToTradeToday[contract].Strike == 0D)//检查是否已经交易过，保证每个行权价只交易一次
                {
                    Contract calltobuy = new Contract();
                    calltobuy.Strike = CallATMstrike[contract];
                    calltobuy.Symbol = contract.Symbol;
                    calltobuy.SecType = "OPT";
                    calltobuy.Right = "C";
                    calltobuy.Currency = "USD";
                    calltobuy.Exchange = "SMART";
                    calltobuy.LastTradeDateOrContractMonth = mostRecentExpireDate[contract];

                    if (!callAlreadyTraded[contract].Contains(CallATMstrike[contract]))//使用均线判断短期趋势，顺势卖
                    {
                        base.OnAddcontract(calltobuy);
                        if (contractPosition[calltobuy].holding >= 0 && !buyPlaced[calltobuy])
                        {
                            Order buycall = new Order();
                            buycall.Action = "BUY";
                            buycall.OrderType = "MKT";
                            buycall.TotalQuantity = 2;
                            buycall.Transmit = true;
                            PlaceOrder(calltobuy, buycall);
                            buyPlaced[calltobuy] = true;

                        }
                        callToTradeToday[contract] = calltobuy;
                        callAlreadyTraded[contract].Add(CallATMstrike[contract]);
                    }


                }
                
                //改为价内期权，买入
                var lowerStrikes = from n in allStrikes[contract]
                                   where n - upperlimitPrice >= 0
                                   select n;

                PutATMstrike[contract] = lowerStrikes.Min();

                Console.WriteLine(DateTime.Now + contract.Symbol + ",要交易的put行权价是：" + PutATMstrike[contract] + ",本策略目前交易的行权价是:" + putToTradeToday[contract].Strike);//调试的目的
                if (putToTradeToday[contract].Strike != PutATMstrike[contract] || putToTradeToday[contract].Strike == 0D)
                {
                    Contract putToBuy = new Contract();
                    putToBuy.Strike = PutATMstrike[contract];
                    putToBuy.Symbol = contract.Symbol;
                    putToBuy.SecType = "OPT";
                    putToBuy.Right = "P";
                    putToBuy.Currency = "USD";
                    putToBuy.Exchange = "SMART";
                    putToBuy.LastTradeDateOrContractMonth = mostRecentExpireDate[contract];

                    if (!putAlreadyTraded[contract].Contains(PutATMstrike[contract]))
                    {
                        base.OnAddcontract(putToBuy);
                        if (contractPosition[putToBuy].holding >= 0 && !buyPlaced[putToBuy])
                        {
                            Order buyput = new Order();
                            buyput.Action = "BUY";
                            buyput.OrderType = "MKT";
                            buyput.TotalQuantity = 2;
                            buyput.Transmit = true;
                            PlaceOrder(putToBuy, buyput);
                            buyPlaced[putToBuy] = true;
                        }
                        putToTradeToday[contract] = putToBuy;
                        putAlreadyTraded[contract].Add(PutATMstrike[contract]);
                    }

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
    }
}
