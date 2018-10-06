using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Mail;

namespace MasterCardChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var wc = new WebClient())
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var urlHome = ConfigurationManager.AppSettings["homeUrl"]; //"https://sorpresas.mastercard.com/ar";
                var homeContent = wc.DownloadString(urlHome);

                var regex = new Regex(ConfigurationManager.AppSettings["pattHome"]); //new Regex(@"(\/beneficios\/detalle\/[0-9]*\/[0-9]*\/[a-z0-9_]*)");
                var canjRegex = new Regex(ConfigurationManager.AppSettings["pattCanj"]); //new Regex(@"button\sid\=\""but_puntos\""");
                var ignoreList = ConfigurationManager.AppSettings["ignoreList"].Split(',').ToList();

                foreach (Match match in regex.Matches(homeContent))
                {
                    var beneContent = wc.DownloadString(urlHome + match.Value);
                    var canjMatch = canjRegex.Match(beneContent);
                    Console.ForegroundColor = (canjMatch.Length > 0) ? ConsoleColor.Green : ConsoleColor.DarkGray;
                    Console.WriteLine(canjMatch.Length > 0 ? "Beneficio disponible en {0}" : "Beneficio {0} Agotado", match.Value);
                    if (canjMatch.Length > 0)
                    {                        
                        if (!ignoreList.Any(x => match.Value.Contains(x))) { send("Beneficio disponible en " + match.Value); }
                    }
                }
            }
        }

        static private void send(string text)
        {
            var fromAddress = new MailAddress("elmokono@gmail.com");
            var toAddress = new MailAddress("elmokono@gmail.com", "Yo");
            const string fromPassword = "lachancha";
            string subject = "mastercard beneficio disponible " + text;
            string body = text;

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };
            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }
        }
    }
}
