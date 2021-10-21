using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBSampleApp;
using IBApi;
using System.Threading;
using IBNetTrading.Strategy;
using System.IO;
using UsStockTime;

namespace IBTrading
{
    class Program
    {
        static void Main(string[] args)
        {
             
            USStockTime usTime = new USStockTime(9, 29, 50);//美国开盘时间，自动根据时令调整
            TimeSpan marketOpenTimeSpan = new TimeSpan(usTime.CorrespondingChinaTime.Ticks);//
            Console.WriteLine("美股开盘时间是:" + usTime.CorrespondingChinaTime);
            TimeSpan nowTicks = new TimeSpan(DateTime.Now.Ticks);
            if (marketOpenTimeSpan > nowTicks)
            {
                TimeSpan waitDuration = marketOpenTimeSpan.Subtract(nowTicks).Duration();
                Console.WriteLine("还没有开盘，需要等待 {0} 毫秒时间", waitDuration);
                Thread.Sleep(waitDuration);
            }
           
            IBClient ibclient = new IBClient();
          //  HftArbitrage hftArbitrage = new HftArbitrage("NIO", "LI", "STK",ibclient, 2);
          //  ShortGamma shortGamma = new ShortGamma(ibclient);
           // shortGamma.addUSStocksSmart(new string[] {"AAPL","FB","SPY","QQQ" });
           // HftArbitrage hftArbitrage2 = new HftArbitrage("NIO", "XPEV", "STK", ibclient, 2);
          //  HftArbitrage hftArbitrage3 = new HftArbitrage("LI", "XPEV", "STK", ibclient, 2);
              dualThrust dualThrust = new dualThrust(ibclient);
              dualThrust.addUSStocksSmart(new string[] { "TQQQ","SPXU","UPRO","UDOW","UVXY","QQQ","DIA","SDOW","DOG","SQQQ","LABU","SPY","LABD","VXX","SOXS","SOXL","YANG",
              "CXO","DXD","HES","JNJ","MRNA","PFE","RCL","VLO","XEC","AZN","AMD","LI","NIO","PENN","RUN","OSTK","GSX","AAPL",
                  "UPST","AMC","GME","BB","PINS","BNTX","APPN","FSLY","JKS","XPEV","QS"});
            //     BreakoutTrading breakout = new BreakoutTrading(ibclient);
            //     breakout.addUSStocksSmart(new string[] { "LABU", "LABD", "XBI", "SPY" });
            //  PivotPoint pivotPoint = new PivotPoint(ibclient);
            //  pivotPoint.addUSStocksSmart(new string[]{ });



            //  BreakoutTrading breakout = new BreakoutTrading(ibclient, 5);
            //  breakout.addUSStocksSmart(new string[] { "JKS","GSX","LI","NIO","QS","NNOX","XPEV","U","APPN","FSLY", "UDOW", "SDOW", "SQQQ", "TQQQ", "UVXY", "UPRO", "RUN", "OSTK", "MRNA", "AMD", "NAIL", "BNTX", "PINS" });

            /*
             *
             ticksScalper ticksScalper = new ticksScalper(ibclient);//除了UDOW和SDOW,其他全部赔钱
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("UDOW"));
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("SDOW"));//正常交易
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("PINS"));//正常交易
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("AMD"));//正常交易
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("OSTK"));//正常交易
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("RUN"));
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("PTON"));
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("BEKE"));
            ticksScalper.addcontract(ticksScalper.CreateUSStockContract("DDOG"));

            dualThrust2 dualThrust2 = new dualThrust2(ibclient, 30);
            
            SupportResistance supportResistance = new SupportResistance(ibclient, 10);           
                           
            

            scalpingRealtimebarClose scalpingRealtimebar = new scalpingRealtimebarClose(ibclient);//赔钱 

             BreakoutTrading breakoutTrading = new BreakoutTrading(ibclient);//赔钱
            breakoutTrading.addcontract(breakoutTrading.CreateUSStockContract("SPY"));
            breakoutTrading.addcontract(breakoutTrading.CreateUSStockContract("QQQ"));
            breakoutTrading.addcontract(breakoutTrading.CreateUSStockContract("AMD"));

            ShortGamma2_WithDirection WithDirection = new ShortGamma2_WithDirection(ibclient);
            WithDirection.addcontract(WithDirection.CreateUSStockContract("QQQ"));
            WithDirection.addcontract(WithDirection.CreateUSStockContract("SPY"));

            VolumeWeightedTrading volumeWeightedTrading = new VolumeWeightedTrading(ibclient);
            volumeWeightedTrading.addcontract(volumeWeightedTrading.CreateUSStockContract("SPY"));  //正常交易          

            ScalpingHFT scalpingHFT = new ScalpingHFT(ibclient);//交易太快，不用这个了
            scalpingHFT.addcontract(scalpingHFT.CreateUSStockContract("MRNA"));
            scalpingHFT.addcontract(scalpingHFT.CreateUSStockContract("TQQQ"));
           
            


            dualThrust.addcontract(dualThrust.CreateUSStockContract("TQQQ"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("XEC"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("SPXU"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("RCL"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("PFE"));            
            dualThrust.addcontract(dualThrust.CreateUSStockContract("CXO"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("CCL"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("AZN"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("UVXY"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("SDOW"));
            dualThrust.addcontract(dualThrust.CreateUSStockContract("UDOW"));            
            dualThrust.addcontract(dualThrust.CreateUSStockContract("SPY"));

            

            
           //   ShortGamma shortGamma = new ShortGamma(ibclient);
           // Contract IWM = shortGamma.CreateUSStockContract("IWM");
           //  shortGamma.addcontract(IWM);
           //  shortGamma.addcontract(shortGamma.CreateUSStockContract("RTY"));
           //  shortGamma.addcontract(shortGamma.CreateUSStockContract("QQQ"));




          LongGamma gamma = new LongGamma(ibclient);
          gamma.addcontract(gamma.CreateUSStockContract("SPY"));
          gamma.addcontract(gamma.CreateUSStockContract("QQQ"));

          //   WithDirection.addcontract(WithDirection.CreateUSStockContract("NDX"));

          ImpIntradayTrading impIntraday = new ImpIntradayTrading(ibclient);
          impIntraday.addcontract(impIntraday.CreateUSStockContract("AAPL"));
          impIntraday.addcontract(impIntraday.CreateUSStockContract("AMD"));
          impIntraday.addcontract(impIntraday.CreateUSStockContract("EYE"));
          impIntraday.addcontract(impIntraday.CreateUSStockContract("TXN"));
          impIntraday.addcontract(impIntraday.CreateUSStockContract("WB"));

          HighestImvSeller imvSeller = new HighestImvSeller(ibclient);



          Console.WriteLine("下面开始另一个策略，longgamma");


          */
            ibclient.ClientSocket.reqPositions();
            ibclient.ClientSocket.reqAccountUpdates(true,"DU843430");

            string exit = null;
            while (( exit= Console.ReadLine())!="Exit")
            {

            }
        }
    }
}
