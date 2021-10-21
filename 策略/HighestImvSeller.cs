using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class HighestImvSeller : TradingInterface
    {//只卖出
        public Dictionary<Contract, Contract> callToTradeToday = new Dictionary<Contract, Contract>();     

        //多品种的交易中，需要建立字典，事实上，其他所有字段都需要做成字典，Contract为key
        Dictionary<Contract, List<double>> callAlreadyTraded = new Dictionary<Contract, List<double>>();
        
        public Dictionary<Contract, int> underlyingConid=new Dictionary<Contract, int>();//用在reqSecDefOptParams中，IB交易系统中的ID，不是本程序赋予的ID

        Dictionary<Contract, HashSet<string>> ExpireDate = new Dictionary<Contract, HashSet<string>>();
        Dictionary<Contract, HashSet<double>> allStrikes= new Dictionary<Contract, HashSet<double>>();

        Dictionary<Contract, string> mostRecentExpireDate= new Dictionary<Contract, string>();//存储最近的到期日
        Dictionary<Contract, DateTime> mostRecentExpireTime = new Dictionary<Contract, DateTime>();
        Dictionary<Contract, double> CallATMstrike=new Dictionary<Contract, double>();

        Dictionary<Contract, bool> optionReady = new Dictionary<Contract, bool>();
        Dictionary<Contract, double> ImpliedVolatility= new Dictionary<Contract, double>();

        public HighestImvSeller(IBClient ibclient) : base(ibclient)
        {           
            ScannerSubscription scanSub = new ScannerSubscription();
            scanSub.Instrument = "STK";
            scanSub.LocationCode = "STK.US";
            scanSub.ScanCode = "HIGH_OPT_IMP_VOLAT";
            scanSub.AbovePrice = 20;
            ibclient.ClientSocket.reqScannerSubscription(7001, scanSub, null);
        }
        public override void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        {
            if (rank<6 && !callAlreadyTraded.ContainsKey(contractDetails.Summary)&& !this.requestId.ContainsKey(contractDetails.Summary))
            {
                OnAddcontract(contractDetails.Summary);
            }
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
            callToTradeToday.Add(contract, new Contract());         
            callAlreadyTraded.Add(contract, new List<double>());            

            ibclient.ClientSocket.reqMarketDataType(2);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "106", false, new List<TagValue>());//获取隐含波动率           
            reqContractSummary(contract);
        }
        public void reqSecDefOptParams(Contract contract)
        {
            ibclient.ClientSocket.reqSecDefOptParams(requestId[contract], contract.Symbol, "", "STK", underlyingConid[contract]);
            //  ibclient.ClientSocket.reqSecDefOptParams(reqId[contract], contract.Symbol, "", "STK", underlyingsContractId[contract]);
        }       
       
        public void reqContractSummary(Contract contract)
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
        public override void OnTrade(Contract contract)
        {
            if (optionReady[contract] == true && ImpliedVolatility[contract] != 0D)//option链和隐含波动率都有了
            {
                TimeSpan timeSpan = mostRecentExpireTime[contract].AddDays(1.5).Subtract(DateTime.Now);
                double Standarddeviation = ImpliedVolatility[contract] * Math.Sqrt(timeSpan.TotalDays / 365);
                double upperlimitPrice = contractPrice[contract].Last() * (1 + Standarddeviation*2);               

                Console.WriteLine("ImpliedVolatility：" + ImpliedVolatility);
                Console.WriteLine("upperlimitPrice：" + upperlimitPrice);
               

                var higherStrikes = from n in allStrikes[contract]
                                    where n - upperlimitPrice >= 0
                                    select n;

                CallATMstrike[contract] = higherStrikes.Min();


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

                    if (!callAlreadyTraded[contract].Contains(CallATMstrike[contract]))//使用均线判断短期趋势，顺势卖
                    {
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
                    }
                }               
            }
        }
    }
}
