using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IBNetTrading
{
 static class util
    {
        public static void WriteMsg(string logName, string msg,string fileName)
        {
            try
            {
                string path = Path.Combine("./log");
                if (!Directory.Exists(path))//判断是否有该文件 
                    Directory.CreateDirectory(path);
                string logFileName = path + "\\" + fileName + ".log";//生成日志文件名称 
                if (!File.Exists(logFileName))//判断日志文件是否存在
                {
                    FileStream fs;
                    fs = File.Create(logFileName);//创建文件
                    fs.Close();
                }
                StreamWriter writer = File.AppendText(logFileName);//文件中添加文件流

                writer.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + logName + "\r\n" + msg);
                writer.Flush();
                writer.Close();
            }
            catch (Exception e)
            {
                string path = Path.Combine("./log");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                string logFileName = path + "\\" + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                if (!File.Exists(logFileName))//判断日志文件是否为当天
                {
                    FileStream fs;
                    fs = File.Create(logFileName);//创建文件
                    fs.Close();
                }
                StreamWriter writer = File.AppendText(logFileName);//文件中添加文件流
                writer.WriteLine(DateTime.Now.ToString("日志记录错误HH:mm:ss") + "\r\n " + e.Message + " " + msg);
                writer.Flush();
                writer.Close();
            }
        }
        
    }
}
