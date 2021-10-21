using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBNetTrading;
using IBApi;
using IBSampleApp;
using UsStockTime;
namespace IBNetTrading.Strategy
{
    class Rbreaker : TradingInterface
    {
       
        Dictionary<Contract, double> Bbreak = new Dictionary<Contract, double>();
        Dictionary<Contract, double> Ssetup = new Dictionary<Contract, double>();
        Dictionary<Contract, double> Senter = new Dictionary<Contract, double>();
        Dictionary<Contract, double> Benter = new Dictionary<Contract, double>();
        Dictionary<Contract, double> Bsetup = new Dictionary<Contract, double>();
        Dictionary<Contract, double> Sbreak = new Dictionary<Contract, double>();

        Dictionary<Contract, double> yesHigh = new Dictionary<Contract, double>();
        Dictionary<Contract, double> yesLow = new Dictionary<Contract, double>();
        Dictionary<Contract, double> yesClose = new Dictionary<Contract, double>();
                  
        
        public Rbreaker(IBClient ibclient) :base(ibclient)
        {

        }

        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            Bbreak.Add(contract, new double());
            Ssetup.Add(contract, new double());
            Senter.Add(contract, new double());
            Benter.Add(contract, new double());
            Bsetup.Add(contract, new double());
            Sbreak.Add(contract, new double());
            yesHigh.Add(contract, new double());
            yesLow.Add(contract, new double());
            yesClose.Add(contract, new double());

            String queryTime = USStockTime.AMESNow.AddMinutes(-20).ToString("yyyyMMdd HH:mm:ss");
            ibclient.ClientSocket.reqHistoricalData(requestId[contract], contract, queryTime,  "2 D", "1 day", "MIDPOINT", 1, 1, new List<TagValue>());
           // ibclient.ClientSocket.reqMktData(requestId[contract], contract, string.Empty, false, new List<TagValue>());

        }
        public override void HisticalData(int reqId, string date, double open, double high, double low, double close, int volume, int count, double WAP, bool hasGapsa)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            yesClose[contractIDrecord[reqId]] = close;
            yesHigh[contractIDrecord[reqId]] = high;
            yesLow[contractIDrecord[reqId]] = low;
        }
        public override void HistoricalDataEnd(int reqId, string startDate, string endDate)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;            
            double pivotPoint = (yesClose[contractIDrecord[reqId]] + yesHigh[contractIDrecord[reqId]] + yesLow[contractIDrecord[reqId]]) / 3;
            Senter[contractIDrecord[reqId]] = 2 * pivotPoint - yesLow[contractIDrecord[reqId]];
            Ssetup[contractIDrecord[reqId]] = pivotPoint + yesHigh[contractIDrecord[reqId]] - yesLow[contractIDrecord[reqId]];
            Bbreak[contractIDrecord[reqId]] = yesHigh[contractIDrecord[reqId]] + 2 * (pivotPoint - yesLow[contractIDrecord[reqId]]);
            Benter[contractIDrecord[reqId]] = 2 * pivotPoint - yesHigh[contractIDrecord[reqId]];
            Bsetup[contractIDrecord[reqId]] = pivotPoint - yesHigh[contractIDrecord[reqId]] + yesLow[contractIDrecord[reqId]];
            Sbreak[contractIDrecord[reqId]] = yesLow[contractIDrecord[reqId]] - 2 * (yesHigh[contractIDrecord[reqId]] - pivotPoint);

            ibclient.ClientSocket.reqMktData(reqId, contractIDrecord[reqId], "", false, new List<TagValue>());

            
        }
        protected override void OnRemovecontract(Contract contract)
        {
          //  base.OnRemovecontract(contract);
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }
        

    }
}
