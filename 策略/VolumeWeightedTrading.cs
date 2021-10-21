using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class VolumeWeightedTrading : TradingInterface
    {        
        Dictionary<Contract, double> totalVolume;
        Dictionary<Contract, double> totalAmount;
        Dictionary<Contract, double> lastPrice;
        Dictionary<Contract, double> vwap;//= (totalAmount + nowPrice*nowVolume)/totalVolume+nowVolume
        Dictionary<Contract, double> lastVwap;        
      
        public VolumeWeightedTrading(IBClient ibclient) : base(ibclient)        {
            this.numberOfData = 100;
            totalVolume = new Dictionary<Contract, double>();
            totalAmount = new Dictionary<Contract, double>();
            lastPrice = new Dictionary<Contract, double>();
            vwap = new Dictionary<Contract, double>();
            lastVwap = new Dictionary<Contract, double>();            
                        
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            totalVolume.Add(contract, 0);
            totalAmount.Add(contract, 0);
            lastPrice.Add(contract, 0);
            vwap.Add(contract, 0);
            lastVwap.Add(contract, 0);

            ibclient.ClientSocket.reqMarketDataType(2);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
          
        }
        public override void OnTradeSize(Contract contract)//先fire价格，再fire数量，所以应该在size里处理
        {            
            double nowPrice = contractPrice[contract].Last();
            double nowVolume = contractSize[contract].Last();

            vwap[contract] = (totalAmount[contract] + nowPrice * nowVolume) / (totalVolume[contract] + nowVolume);
            Console.WriteLine(contract.Symbol + ":vwap:" + vwap[contract]+",last tradeSize:"+ nowVolume);
            if (contractPrice[contract].Count >= numberOfData)
            {
                if (nowPrice > vwap[contract])//做多
                {
                    if (contractPosition[contract].holding <= 0)
                    {
                        Order buyOrder = new Order();
                        buyOrder.Action = "BUY";
                        buyOrder.TotalQuantity = 100;
                        buyOrder.OrderType = "LMT";
                        buyOrder.Transmit = true;
                        buyOrder.Tif = "IOC";//fill or kill
                        buyOrder.LmtPrice = nowPrice;

                        PlaceOrder(contract, buyOrder);
                        
                    }
                }
                else if (nowPrice < vwap[contract])//平多
                {
                    if (contractPosition[contract].holding >= 0 )//平仓多头
                    {
                        Order sellorder = new Order();
                        sellorder.Action = "SELL";
                        sellorder.TotalQuantity = 100;
                        sellorder.OrderType = "LMT";
                        sellorder.Transmit = true;
                        sellorder.Tif = "IOC";
                        sellorder.LmtPrice = nowPrice;

                        PlaceOrder(contract, sellorder);
                        

                    }
                }
            }//保证有足够的数据才行

            totalAmount[contract] = totalAmount[contract] + nowPrice * nowVolume;
            lastPrice[contract] = nowPrice;
            totalVolume[contract] = totalVolume[contract] + nowVolume;
            lastVwap[contract] = vwap[contract];
        }
        public override void OnTrade(Contract contract)
        {
           
        }
      
    }
}
