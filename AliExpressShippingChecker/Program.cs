using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AliExpressShippingChecker
{
    public class FreightResult
    {
        public Carrier[] Freight { get; set; }
    }

    public class Carrier
    {
        public string id { get; set; }
        public string company { get; set; }
        public string companyDisplayName { get; set; }
        public decimal localPrice { get; set; }
        public string sendGoodsCountryFullName { get; set; }
        public string time { get; set; }
    }

    class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("AliExpress Shipping Price Checker");
            Console.WriteLine("---------------------------------");

            Console.Write("search query:");
            var query = Console.ReadLine();

            Console.Write("include free? (y/n) default 'no':");
            var includeFree = Console.ReadLine() == "y";
            var timeStamp = DateTime.Now.ToString("yyyyMMddhhmmss");

            using (var wc = new WebClient())
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var searchResultUrl = ConfigurationManager.AppSettings["searchResultUrl"]
                    .Replace("{{QUERY}}", query.Replace(" ", "+"))
                    .Replace("{{TIMESTAMP}}", timeStamp);

                var homeContent = wc.DownloadString(searchResultUrl);

                //"//www.aliexpress.com/item/Free-Shipping-For-Intel-Xeon-X5650-CPU-2-66GHz-LGA1366-12MB-L3-Cache-Six-Core-6/2033514296.html?spm=2114.01010208.3.2.Yb0lKg&amp;ws_ab_test=searchweb0_0,searchweb201602_1_10065_10068_10136_10137_10138_10060_10062_10141_10056_10055_10054_10059_124_10099_10103_10102_10096_10148_10147_10052_10053_10050_10107_10142_10051_10143_9987_10084_10083_10119_10080_10082_10081_10110_10111_10112_10113_10114_10037_10032_10078_10079_10077_10073_10070_10123_10120_10124-10050_9987,searchweb201603_6,afswitch_1_afChannel,ppcSwitch_4,single_sort_0_default&amp;btsid=66bcb4f4-4803-4b34-80b9-f6ff55544d95&amp;algo_expid=6fec5f9f-67e6-4952-9816-3d1c191dc584-0&amp;algo_pvid=6fec5f9f-67e6-4952-9816-3d1c191dc584"

                var regex = new Regex(ConfigurationManager.AppSettings["pattID"], RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                //var regexPage = new Regex(ConfigurationManager.AppSettings["pageIdxPattern"].Replace("{{QUERY}}", query), RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                var regexCount = new Regex(ConfigurationManager.AppSettings["pattCount"], RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                var urlShipping = ConfigurationManager.AppSettings["urlShipping"];
                var pagesSleep = int.Parse(ConfigurationManager.AppSettings["pagesSleep"]);
                var itemsSleep = int.Parse(ConfigurationManager.AppSettings["itemsSleep"]);
                var itemsPerPage = int.Parse(ConfigurationManager.AppSettings["itemsPerPage"]);
                var carriers = new List<Carrier>();
                //var pages = regexPage.Matches(homeContent);
                var count = decimal.Parse(regexCount.Matches(homeContent)[0].Groups[1].Value.Replace(",",""));
                var pages = Math.Floor(count / itemsPerPage);
                Console.WriteLine("- Found {0} result(s)", count);
                
                for (int page = 0; page < pages; page++)
                {
                    Console.WriteLine("- Page {0}", page);

                    if (page > 0)
                    {
                        searchResultUrl = ConfigurationManager.AppSettings["urlPaging"]
                            .Replace("{{QUERY}}", query.Replace(" ", "+"))
                            .Replace("{{IDX}}", (page + 1).ToString())
                            //.Replace("{{CAT}}", pages[0].Groups["cat"].Value)
                            .Replace("{{TIMESTAMP}}", timeStamp);

                        Console.WriteLine("Sleeping 5 seconds to avoid banning..");
                        System.Threading.Thread.Sleep(pagesSleep);
                        homeContent = wc.DownloadString(searchResultUrl);
                    }

                    foreach (Match match in regex.Matches(homeContent))
                    {
                        System.Threading.Thread.Sleep(itemsSleep);

                        //var product = wc.DownloadString("https:" + match.Groups["link"].Value);
                        var jsonShipping = wc.DownloadString(
                            urlShipping
                                .Replace("{{ID}}", match.Groups["id"].Value)
                                .Replace("{{RND}}", (new Random().NextDouble() * int.MaxValue).ToString())
                        );

                        var result = Newtonsoft.Json.JsonConvert.DeserializeObject<FreightResult>(
                            jsonShipping
                                .Substring(1, jsonShipping.Length - 1)
                                .Substring(0, jsonShipping.Length - 2)
                            );

                        Console.WriteLine("- Product {0}", match.Groups["id"].Value);
                        foreach (var crr in result.Freight.Where(x => x.company.Contains("DHL") || x.company.Contains("UPS") || x.company.Contains("FEDEX")))
                        {
                            crr.id = match.Groups["id"].Value;
                            carriers.Add(crr);
                            Console.WriteLine("      USD {0:#0.00} by {1}  / {2} days", crr.localPrice, crr.companyDisplayName, crr.time);
                        }
                    }
                }
                var item = carriers.Where(x => (!includeFree && x.localPrice > 0) || (includeFree)).OrderBy(x => x.localPrice).FirstOrDefault();
                Console.WriteLine("*******************************************************");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("!!Cheapest carrier ----> {0}", item.id);
                Console.WriteLine("USD {0:#0.00} by {1}  / {2} days", item?.localPrice, item?.companyDisplayName, item?.time);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("*******************************************************");
            }
        }

        //static private void send(string text)
        //{
        //    var fromAddress = new MailAddress("elmokono@gmail.com");
        //    var toAddress = new MailAddress("elmokono@gmail.com", "Yo");
        //    const string fromPassword = "lachancha";
        //    string subject = "mastercard beneficio disponible " + text;
        //    string body = text;

        //    var smtp = new SmtpClient
        //    {
        //        Host = "smtp.gmail.com",
        //        Port = 587,
        //        EnableSsl = true,
        //        DeliveryMethod = SmtpDeliveryMethod.Network,
        //        UseDefaultCredentials = false,
        //        Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
        //    };
        //    using (var message = new MailMessage(fromAddress, toAddress)
        //    {
        //        Subject = subject,
        //        Body = body
        //    })
        //    {
        //        smtp.Send(message);
        //    }
        //}
    }
}
