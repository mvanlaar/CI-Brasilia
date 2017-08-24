using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Globalization;
using System.Threading;
using CsvHelper;
using System.Text;
using System.Web;
using System.Configuration;

namespace CI_Brasilia
{
    public class Program
    {
        static void Main(string[] args)
        {
            const string ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
            const string uamobile =
                "Mozilla/5.0 (Linux; Android 4.4; Nexus 7 Build/KOT24) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/30.0.1599.105 Safari/537.36";
            const string websitemobilte = "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";
            const string HeaderAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*;q=0.8";
            const string HeaderEncoding = "gzip,deflate";
            string APIPathBus = "bus/agencystop/";

            bool fullrun = false;

            var export = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = export;
            Thread.CurrentThread.CurrentUICulture = export;


            List<CIBusOrigens> _Origens = new List<CIBusOrigens> { };
            List<CIBusOrigensDestino> _OrigensDestino = new List<CIBusOrigensDestino> { };
            List<CIBusTramoSteps> _TramoSteps = new List<CIBusTramoSteps> { };
            List<CIBusRoutes> _Routes = new List<CIBusRoutes> { };
            List<CIBusRoutesDetails> _RoutesDetails = new List<CIBusRoutesDetails> { };
            List<CIBusRoutes> _RoutesError = new List<CIBusRoutes> { };
            List<CIBusRoutes> _RoutesNon = new List<CIBusRoutes> { };
            List<CIBusRoutesCalender> _RouteCalender = new List<CIBusRoutesCalender> { };
            List<GTFSStops> _GTFSStops = new List<GTFSStops> { };

            if (fullrun)
            {
                string CityOrigens = String.Empty;
                Console.WriteLine("Retreiving from locations...");
                using (var webClient = new System.Net.WebClient())
                {
                    webClient.Headers.Add("user-agent", ua);
                    webClient.Headers.Add("Referer", "http://www.expresobrasilia.com/en/cobertura");
                    CityOrigens =
                        webClient.DownloadString(
                            "http://186.118.168.234:8888/BrasiliaWS2Rest/Brasilia/getOrigen?NitConvenio=890100531");
                }
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(CityOrigens);
                XmlNodeList NodeOrigen = xmlDoc.SelectNodes("/Origen");
                XmlNodeList l = NodeOrigen[0].SelectNodes("Record");
                for (int i = 1; i < l.Count; i++)
                {
                    // Loop through all City's
                    //string url1 = @"http://186.118.168.234:8888/BrasiliaWS2Rest/Brasilia/getDestino?NitConvenio=890100531&CodOrigen=";
                    XmlNode node = l[i];
                    string Origen_CIUDAD_ID = node["CIUDAD_ID"].InnerText;
                    string Origen_CIUDAD_NOMBRE = node["CIUDAD_NOMBRE"].InnerText;
                    if (Origen_CIUDAD_ID != "0")
                    {
                        _Origens.Add(new CIBusOrigens
                        {
                            Ciudad_ID = Origen_CIUDAD_ID,
                            Ciudad_Nombre = Origen_CIUDAD_NOMBRE
                        });
                    }
                }
                // Loop through possible orgin for destino combo's
                Console.WriteLine("Parsing through the from to get the destionations for each from locations...");
                Parallel.ForEach(_Origens, new ParallelOptions {MaxDegreeOfParallelism = 10}, Origen =>
                {
                    Console.WriteLine("From: {0}", Origen.Ciudad_Nombre);
                    string CityDestinos = String.Empty;
                    using (var webClient = new System.Net.WebClient())
                    {
                        webClient.Headers.Add("user-agent", ua);
                        webClient.Headers.Add("Referer", "http://www.expresobrasilia.com/en/cobertura");
                        CityDestinos =
                            webClient.DownloadString(
                                "http://186.118.168.234:8888/BrasiliaWS2Rest/Brasilia/getDestino?NitConvenio=890100531&CodOrigen=" +
                                Origen.Ciudad_ID);
                    }
                    // Parse Reponse
                    XmlDocument xmlDocDestino = new XmlDocument();
                    xmlDocDestino.LoadXml(CityDestinos);
                    XmlNodeList NodeDestino = xmlDocDestino.SelectNodes("/Destino");
                    XmlNodeList ld = NodeDestino[0].SelectNodes("Record");
                    for (int i = 1; i < ld.Count; i++)
                    {
                        // Loop through all City's                    
                        XmlNode node = ld[i];
                        string Destino_CIUDAD_ID = node["CIUDAD_ID"].InnerText;
                        string Destino_CIUDAD_NOMBRE = node["CIUDAD_NOMBRE"].InnerText;
                        if (Destino_CIUDAD_ID != "0")
                        {
                            _OrigensDestino.Add(new CIBusOrigensDestino
                            {
                                Origen_Ciudad_ID = Origen.Ciudad_ID,
                                Origen_Ciudad_Nombre = Origen.Ciudad_Nombre,
                                Destino_Ciudad_ID = Destino_CIUDAD_ID,
                                Destino_Ciudad_Nombre = Destino_CIUDAD_NOMBRE
                            });
                        }
                    }
                });
                Console.WriteLine("Parsing througg the possible routes...");
                //// , new ParallelOptions { MaxDegreeOfParallelism = 10 }, (Day) =>
                ////{
                ////foreach(var FromToCombo in _OrigensDestino)
                Parallel.ForEach(_OrigensDestino, new ParallelOptions {MaxDegreeOfParallelism = 2}, FromToCombo =>
                {
                    Console.WriteLine("From: {0} to {1}", FromToCombo.Origen_Ciudad_Nombre,
                        FromToCombo.Destino_Ciudad_Nombre);
                    try
                    {
                        string Tramo = String.Empty;
                        string TramoUrl =
                            String.Format(
                                "http://186.118.168.234:8888/BrasiliaWS2Rest/Brasilia/getConsultarTramo?NitConvenio=890100531&CodOrigen={0}&CodDestino={1}",
                                FromToCombo.Origen_Ciudad_ID, FromToCombo.Destino_Ciudad_ID);
                        using (var webClient1 = new System.Net.WebClient())
                        {
                            webClient1.Headers.Add("user-agent", ua);
                            webClient1.Headers.Add("Referer", "http://www.expresobrasilia.com/en/cobertura");
                            Tramo = webClient1.DownloadString(TramoUrl);
                        }
                        XmlDocument xmlDocTramo = new XmlDocument();
                        xmlDocTramo.LoadXml(Tramo);
                        XmlElement Tramos = xmlDocTramo.DocumentElement;
                        XmlNodeList NodeTramo = xmlDocTramo.SelectNodes("/Tramos");
                        if (Tramos.HasAttribute("total"))
                        {
                            XmlAttribute Tramos_total = Tramos.GetAttributeNode("total");
                            int TramosTotal = Convert.ToInt32(Tramos_total.Value);
                            if (TramosTotal > 0)
                            {
                                // Check if we have the route. And How Many steps there are in the route list
                                // If it is less then the number we have al ready, we don't have to process this.
                                var Routenr = xmlDocTramo.SelectNodes("/Tramos/Record/RUTA")[0].InnerText;


                                //}


                                //     bool alreadyExists = _TramoSteps.Exists(x => x.RutaNr == Routenr.ToString());
                                //if (!alreadyExists)
                                //{
                                // Add Information to list
                                //_TramoSteps.Add(new CIBusTramoSteps { RutaNr = Routenr.ToString(), Steps = TramosTotal });
                                //_Routes.Add(new CIBusRoutes { RutaNr = Routenr.ToString(), From = FromToCombo.Origen_Ciudad_Nombre, To = FromToCombo.Destino_Ciudad_Nombre });
                                // Process the xml.
                                XmlNodeList nodes = xmlDocTramo.DocumentElement.SelectNodes("/Tramos/Record");
                                foreach (XmlNode noderecord in nodes)
                                {
                                    // Insert into table

                                    using (SqlConnection connection =
                                        new SqlConnection(
                                            "Server=127.0.0.1;Database=ColombiaInfo-Data;User Id=Mule;Password=P@ssw0rd;")
                                    )
                                    {
                                        using (SqlCommand command = new SqlCommand())
                                        {
                                            command.Connection = connection; // <== lacking
                                            command.CommandType = CommandType.Text;
                                            command.CommandText =
                                                "INSERT INTO[dbo].[BrasiliaRoutes] ([ROUTENR],[TRAMOS],[EMPRESA],[EMPRESAN],[AGENCIA],[AGENCIAN],[CIUDADN],[DEPARTAMENTON],[PAISN],[KILOMETROS],[MINUTOS],[Origen_Ciudad_ID],[Destino_Ciudad_ID],[Origen_Ciudad_Nombre],[Destino_Ciudad_Nombre]) VALUES (@ROUTENR,@TRAMOS,@EMPRESA,@EMPRESAN,@AGENCIA,@AGENCIAN,@CIUDADN,@DEPARTAMENTON,@PAISN,@KILOMETROS,@MINUTOS,@Origen_Ciudad_ID,@Destino_Ciudad_ID,@Origen_Ciudad_Nombre,@Destino_Ciudad_Nombre)";
                                            command.Parameters.AddWithValue("@ROUTENR", Routenr.ToString());
                                            command.Parameters.AddWithValue("@TRAMOS", TramosTotal);
                                            command.Parameters.AddWithValue("@EMPRESA",
                                                noderecord.SelectSingleNode("EMPRESA").InnerText);
                                            command.Parameters.AddWithValue("@EMPRESAN",
                                                noderecord.SelectSingleNode("EMPRESAN").InnerText);
                                            command.Parameters.AddWithValue("@AGENCIA",
                                                noderecord.SelectSingleNode("AGENCIA").InnerText);
                                            command.Parameters.AddWithValue("@AGENCIAN",
                                                noderecord.SelectSingleNode("AGENCIAN").InnerText);
                                            command.Parameters.AddWithValue("@CIUDADN",
                                                noderecord.SelectSingleNode("CIUDADN").InnerText);
                                            command.Parameters.AddWithValue("@DEPARTAMENTON",
                                                noderecord.SelectSingleNode("DEPARTAMENTON").InnerText);
                                            command.Parameters.AddWithValue("@PAISN",
                                                noderecord.SelectSingleNode("PAISN").InnerText);
                                            command.Parameters.AddWithValue("@KILOMETROS",
                                                noderecord.SelectSingleNode("KILOMETROS").InnerText);
                                            command.Parameters.AddWithValue("@MINUTOS",
                                                noderecord.SelectSingleNode("MINUTOS").InnerText);
                                            command.Parameters.AddWithValue("@Origen_Ciudad_ID",
                                                FromToCombo.Origen_Ciudad_ID);
                                            command.Parameters.AddWithValue("@Destino_Ciudad_ID",
                                                FromToCombo.Destino_Ciudad_ID);
                                            command.Parameters.AddWithValue("@Origen_Ciudad_Nombre",
                                                FromToCombo.Origen_Ciudad_Nombre);
                                            command.Parameters.AddWithValue("@Destino_Ciudad_Nombre",
                                                FromToCombo.Destino_Ciudad_Nombre);

                                            //try
                                            //{
                                            connection.Open();
                                            command.ExecuteNonQuery();
                                            //}
                                            //catch (SqlException)
                                            //{
                                            //    // error here
                                            //}
                                            //finally
                                            //{
                                            connection.Close();
                                            //}
                                        }


                                        //     _RoutesDetails.Add(new CIBusRoutesDetails
                                        //{
                                        //    EMPRESA = noderecord.SelectSingleNode("EMPRESA").InnerText,
                                        //    EMPRESAN = noderecord.SelectSingleNode("EMPRESAN").InnerText,
                                        //    AGENCIA = noderecord.SelectSingleNode("AGENCIA").InnerText,
                                        //    AGENCIAN = noderecord.SelectSingleNode("AGENCIAN").InnerText,
                                        //    CIUDADN = noderecord.SelectSingleNode("CIUDADN").InnerText,
                                        //    DEPARTAMENTON = noderecord.SelectSingleNode("DEPARTAMENTON").InnerText,
                                        //    PAISN = noderecord.SelectSingleNode("PAISN").InnerText,
                                        //    RUTA = noderecord.SelectSingleNode("RUTA").InnerText,
                                        //    KILOMETROS = noderecord.SelectSingleNode("KILOMETROS").InnerText,
                                        //    MINUTOS = noderecord.SelectSingleNode("MINUTOS").InnerText,
                                        //});
                                    }
                                }
                                //else
                                //{
                                //         // Check if route steps is larger then current.
                                //         var CurrentRoute = _TramoSteps.Find(p => p.RutaNr == Routenr.ToString());
                                //    if (TramosTotal > CurrentRoute.Steps)
                                //    {
                                //             // Only when it's larger then current route steps process the xml
                                //             // Delete The route name from Table
                                //             var itemToRemove = _Routes.Single(r => r.RutaNr == Routenr.ToString());
                                //        _Routes.Remove(itemToRemove);
                                //             // Add New route
                                //             _Routes.Add(new CIBusRoutes { RutaNr = Routenr.ToString(), From = FromToCombo.Origen_Ciudad_Nombre, To = FromToCombo.Destino_Ciudad_Nombre });
                                //             // Process the xml
                                //             XmlNodeList nodes = xmlDocTramo.DocumentElement.SelectNodes("/Tramos/Record");
                                //        foreach (XmlNode noderecord in nodes)
                                //        {
                                //            _RoutesDetails.Add(new CIBusRoutesDetails
                                //            {
                                //                EMPRESA = noderecord.SelectSingleNode("EMPRESA").InnerText,
                                //                EMPRESAN = noderecord.SelectSingleNode("EMPRESAN").InnerText,
                                //                AGENCIA = noderecord.SelectSingleNode("AGENCIA").InnerText,
                                //                AGENCIAN = noderecord.SelectSingleNode("AGENCIAN").InnerText,
                                //                CIUDADN = noderecord.SelectSingleNode("CIUDADN").InnerText,
                                //                DEPARTAMENTON = noderecord.SelectSingleNode("DEPARTAMENTON").InnerText,
                                //                PAISN = noderecord.SelectSingleNode("PAISN").InnerText,
                                //                RUTA = noderecord.SelectSingleNode("RUTA").InnerText,
                                //                KILOMETROS = noderecord.SelectSingleNode("KILOMETROS").InnerText,
                                //                MINUTOS = noderecord.SelectSingleNode("MINUTOS").InnerText,
                                //            });
                                //        }
                                //    }
                                //         // End Exists
                            }
                            // End Route Parsing
                        }
                        // End total exists checking
                        //}
                        // End Route Parsing
                    }
                    catch
                    {
                        Console.WriteLine("Timeout, skip or error");
                    }

                    GC.Collect();
                });
            }
            // Get the route information:


            using (SqlConnection connection =
                new SqlConnection("Server=127.0.0.1;Database=ColombiaInfo-Data;User Id=Mule;Password=P@ssw0rd;"))
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection; // <== lacking
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"SELECT DISTINCT[ROUTENR]
                        ,[TRAMOS]
                        ,[EMPRESA]
                        ,[EMPRESAN]
                        ,[AGENCIA]
                        ,[AGENCIAN]
                        ,[CIUDADN]
                        ,[DEPARTAMENTON]
                        ,[PAISN]
                        ,[KILOMETROS]
                        ,[MINUTOS]
                        ,[Origen_Ciudad_ID]
                        ,[Destino_Ciudad_ID]
                        ,[Origen_Ciudad_Nombre]
                        ,[Destino_Ciudad_Nombre]
                    FROM [ColombiaInfo-Data].[dbo].[BrasiliaRoutes]
                    where exists(select 1 from ( select ROUTENR, max(TRAMOS) as TRAMOS
                    from[BrasiliaRoutes]
                    group by ROUTENR
                        ) as cond
                    where[BrasiliaRoutes].ROUTENR=cond.ROUTENR
                    and[BrasiliaRoutes].TRAMOS =cond.TRAMOS
                
                        ) AND[BrasiliaRoutes].KILOMETROS = 0                          
                    order by ROUTENR, TRAMOS";


                    //try
                    //{
                    connection.Open();
                    using (SqlDataReader rdr = command.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            for (int i = 0; i < 7; i++)
                            {
                                Boolean error = false;
                                string Stage7 = String.Empty;

                                string from = (string) rdr["Origen_Ciudad_Nombre"];
                                string to = (string) rdr["Destino_Ciudad_Nombre"];
                                string dbroutenr = (string) rdr["ROUTENR"];

                                if (!from.Contains(" - "))
                                {
                                    string[] temp = from.Split('-');
                                    from = temp[0];
                                }
                                if (!to.Contains(" - "))
                                {
                                    string[] temp = to.Split('-');
                                    to = temp[0];
                                }
                                // Hotfix Lima
                                if (to == "LIMA  (PERU) -  J. PRADO")
                                {
                                    string[] temp = to.Split('-');
                                    to = temp[0];
                                }
                                try
                                {
                                    Console.WriteLine("Starting request for: {0} to {1}", from, to);
                                    CookieContainer cookieContainer = new CookieContainer();

                                    HttpWebRequest request = (HttpWebRequest) WebRequest.Create(websitemobilte);

                                    //var GetAllFlightDatesPost = new { origin = fromiata, destination = toiata, months = 12 };
                                    //string GetAllFlightDatesPostString = JsonConvert.SerializeObject(GetAllFlightDatesPost);

                                    //var dataIndex = Encoding.ASCII.GetBytes(GetAllFlightDatesPostString);

                                    request.Method = "GET";
                                    request.ContentType = "application/json; charset=utf-8";
                                    //request.ContentLength = dataIndex.Length;
                                    request.UserAgent = uamobile;
                                    request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                    request.Accept = HeaderAccept;
                                    //request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                    request.AutomaticDecompression =
                                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                    request.CookieContainer = cookieContainer;
                                    request.Proxy = null;

                                    //using (var streamIndex = request.GetRequestStream())
                                    //{
                                    //    streamIndex.Write(dataIndex, 0, dataIndex.Length);
                                    //}
                                    string Stage0 = String.Empty;
                                    using (HttpWebResponse responseIndex = (HttpWebResponse) request.GetResponse())
                                    using (StreamReader reader =
                                        new StreamReader(responseIndex.GetResponseStream()))
                                    {
                                        Stage0 = reader.ReadToEnd();
                                    }

                                    //Stage 1
                                    request = (HttpWebRequest) WebRequest.Create(
                                        "http://186.118.168.234:7777/TiquetePW/jforms/clickEvent?id=jforms[tIQW001Controller-compra-i-0]&value=&currentField=jforms[tIQW001Controller-compra-origen-0]");
                                    request.Method = "GET";
                                    request.ContentType = "application/json; charset=utf-8";
                                    request.UserAgent = uamobile;
                                    request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                    request.Accept = "application/json, text/javascript, */*; q=0.01";
                                    request.AutomaticDecompression =
                                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                    request.CookieContainer = cookieContainer;
                                    request.Proxy = null;
                                    request.Referer =
                                        "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";

                                    string Stage1 = String.Empty;
                                    using (HttpWebResponse responseIndex = (HttpWebResponse) request.GetResponse())
                                    using (StreamReader reader =
                                        new StreamReader(responseIndex.GetResponseStream()))
                                    {
                                        Stage1 = reader.ReadToEnd();
                                    }

                                    // Stage 1a
                                    request = (HttpWebRequest) WebRequest.Create(
                                        "http://186.118.168.234:7777/TiquetePW/jforms/clickEvent?id=jforms[tIQW001Controller-compra-origen-0]&value=%null&currentField=jforms[tIQW001Controller-compra-i-0]");
                                    request.Method = "GET";
                                    request.ContentType = "application/json; charset=utf-8";
                                    request.UserAgent = uamobile;
                                    request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                    request.Accept = "application/json, text/javascript, */*; q=0.01";
                                    request.AutomaticDecompression =
                                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                    request.CookieContainer = cookieContainer;
                                    request.Proxy = null;
                                    request.Referer =
                                        "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";
                                    string Stage1a = String.Empty;
                                    using (HttpWebResponse responseIndex = (HttpWebResponse) request.GetResponse())
                                    using (StreamReader reader =
                                        new StreamReader(responseIndex.GetResponseStream()))
                                    {
                                        Stage1a = reader.ReadToEnd();
                                    }

                                    //Stage 3
                                    string s3Request =
                                        string.Format(
                                            "http://186.118.168.234:7777/TiquetePW/jforms/clickEvent?id=jforms[tIQW001Controller-compra-destino-0]&value={0}&currentField=jforms[tIQW001Controller-compra-origen-0]",
                                            HttpUtility.UrlEncode(from));
                                    request = (HttpWebRequest) WebRequest.Create(s3Request);
                                    request.Method = "GET";
                                    request.ContentType = "application/json; charset=utf-8";
                                    request.UserAgent = uamobile;
                                    request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                    request.Accept = "application/json, text/javascript, */*; q=0.01";
                                    request.AutomaticDecompression =
                                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                    request.CookieContainer = cookieContainer;
                                    request.Proxy = null;
                                    request.Referer =
                                        "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";
                                    //using (var streamIndex = request.GetRequestStream())
                                    //{
                                    //    streamIndex.Write(dataIndex, 0, dataIndex.Length);
                                    //}
                                    string Stage3 = String.Empty;
                                    using (HttpWebResponse responseIndex = (HttpWebResponse) request.GetResponse())
                                    using (StreamReader reader =
                                        new StreamReader(responseIndex.GetResponseStream()))
                                    {
                                        Stage3 = reader.ReadToEnd();
                                    }
                                    // Stage 4
                                    string s4Request =
                                        string.Format(
                                            "http://186.118.168.234:7777/TiquetePW/jforms/clickEvent?id=jforms[tIQW001Controller-compra-f_ida-0]&value={0}&currentField=jforms[tIQW001Controller-compra-destino-0]",
                                            HttpUtility.UrlEncode(to));
                                    request = (HttpWebRequest) WebRequest.Create(s4Request);
                                    request.Method = "GET";
                                    request.ContentType = "application/json; charset=utf-8";
                                    request.UserAgent = uamobile;
                                    request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                    request.Accept = "application/json, text/javascript, */*; q=0.01";
                                    request.AutomaticDecompression =
                                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                    request.CookieContainer = cookieContainer;
                                    request.Proxy = null;
                                    request.Referer =
                                        "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";
                                    //using (var streamIndex = request.GetRequestStream())
                                    //{
                                    //    streamIndex.Write(dataIndex, 0, dataIndex.Length);
                                    //}
                                    string Stage4 = String.Empty;
                                    using (HttpWebResponse responseIndex = (HttpWebResponse) request.GetResponse())
                                    using (StreamReader reader =
                                        new StreamReader(responseIndex.GetResponseStream()))
                                    {
                                        Stage4 = reader.ReadToEnd();
                                    }

                                    // Stage 5
                                    DateTime requestdate = DateTime.Now;
                                    requestdate = requestdate.AddDays(i);
                                    string s5Request =
                                        string.Format(
                                            "http://186.118.168.234:7777/TiquetePW/jforms/clickEvent?id=jforms[tIQW001Controller-compra-buscar-0]&value={0}&currentField=jforms[tIQW001Controller-compra-f_ida-0]",
                                            requestdate.ToString("dd/MM/yyyy"));
                                    request = (HttpWebRequest) WebRequest.Create(s5Request);
                                    request.Method = "GET";
                                    request.ContentType = "application/json; charset=utf-8";
                                    request.UserAgent = uamobile;
                                    request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                    request.Accept = "application/json, text/javascript, */*; q=0.01";
                                    request.AutomaticDecompression =
                                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                    request.CookieContainer = cookieContainer;
                                    request.Proxy = null;
                                    request.Referer =
                                        "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";
                                    //using (var streamIndex = request.GetRequestStream())
                                    //{
                                    //    streamIndex.Write(dataIndex, 0, dataIndex.Length);
                                    //}
                                    string Stage5 = String.Empty;
                                    using (HttpWebResponse responseIndex = (HttpWebResponse) request.GetResponse())
                                    using (StreamReader reader =
                                        new StreamReader(responseIndex.GetResponseStream()))
                                    {
                                        Stage5 = reader.ReadToEnd();
                                    }

                                    //Console.WriteLine(Stage5);

                                    // Stage 6
                                    request = (HttpWebRequest) WebRequest.Create(
                                        "http://186.118.168.234:7777/TiquetePW/jforms/closeForm?beanName=tIQW001Controller");
                                    request.Method = "GET";
                                    request.ContentType = "application/json; charset=utf-8";
                                    request.UserAgent = uamobile;
                                    request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                    request.Accept = "application/json, text/javascript, */*; q=0.01";
                                    request.AutomaticDecompression =
                                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                    request.CookieContainer = cookieContainer;
                                    request.Proxy = null;
                                    request.Referer =
                                        "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";
                                    //using (var streamIndex = request.GetRequestStream())
                                    //{
                                    //    streamIndex.Write(dataIndex, 0, dataIndex.Length);
                                    //}
                                    string Stage6 = String.Empty;
                                    using (HttpWebResponse responseIndex = (HttpWebResponse) request.GetResponse())
                                    using (StreamReader reader =
                                        new StreamReader(responseIndex.GetResponseStream()))
                                    {
                                        Stage6 = reader.ReadToEnd();
                                    }

                                    dynamic Stage5Response = JsonConvert.DeserializeObject(Stage5);

                                    if (Stage5Response.jforms.webShowDocument != null)
                                    {
                                        string RedirectUrl =
                                            Stage5Response.jforms.webShowDocument[0].urlOpen.ToString();

                                        if (!RedirectUrl.Contains("NOAVAILABLE.xhtml"))
                                        {
                                            request = (HttpWebRequest) WebRequest.Create(RedirectUrl);
                                            request.Method = "GET";
                                            //request.ContentType = "application/json; charset=utf-8";
                                            request.UserAgent = uamobile;
                                            request.Headers.Add("Accept-Encoding", HeaderEncoding);
                                            //request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                                            request.Accept =
                                                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                                            request.AutomaticDecompression =
                                                DecompressionMethods.GZip | DecompressionMethods.Deflate;
                                            request.CookieContainer = cookieContainer;
                                            request.Proxy = null;
                                            request.Referer =
                                                "http://186.118.168.234:7777/TiquetePW/faces/TIQW001MOBILE.xhtml?App=S";
                                            //using (var streamIndex = request.GetRequestStream())
                                            //{
                                            //    streamIndex.Write(dataIndex, 0, dataIndex.Length);
                                            //}

                                            using (HttpWebResponse responseIndex =
                                                (HttpWebResponse) request.GetResponse())
                                            using (StreamReader reader =
                                                new StreamReader(responseIndex.GetResponseStream()))
                                            {
                                                Stage7 = reader.ReadToEnd();
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("No Route possible today or ever...");
                                            _RoutesNon.Add(
                                                new CIBusRoutes {RutaNr = dbroutenr, From = from, To = to});
                                            error = true;
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error parsing route response");
                                        _RoutesError.Add(
                                            new CIBusRoutes {RutaNr = dbroutenr, From = from, To = to});
                                        error = true;
                                    }
                                }
                                catch
                                {
                                    error = true;
                                }
                                // Get Route Listing:
                                if (!error)
                                {
                                    var doc = new HtmlDocument();
                                    doc.LoadHtml(Stage7);

                                    var routes =
                                        doc.DocumentNode.SelectNodes("//div[@class='route-widget']");

                                    foreach (var route in routes)
                                    {
                                        string routenrwithtime =
                                            route.SelectSingleNode(
                                                    "./div[1]/div[1]/div[1]/div[1]/div[1]/h3[1]/span[1]")
                                                .InnerText.Trim();
                                        string[] routenrwithtimeparts = routenrwithtime.Split('-');
                                        String RouteNr = routenrwithtimeparts[0];
                                        string TimeofDay = routenrwithtimeparts[1];
                                        RouteNr = RouteNr.Trim();
                                        TimeofDay = TimeofDay.Trim();
                                        HtmlNode servicenode = route.SelectSingleNode(
                                            "./div[1]/div[1]/div[1]/div[1]/div[2]/img[@src]");
                                        String service = servicenode.Attributes["src"].Value;
                                        int position = service.LastIndexOf('/');
                                        service = service.Substring(position + 1);
                                        int fileExtPos = service.LastIndexOf(".");
                                        if (fileExtPos >= 0)
                                            service = service.Substring(0, fileExtPos);

                                        // First part if departure
                                        string daySalida = route
                                            .SelectSingleNode(
                                                "./div[1]/div[1]/div[2]/div[1]/div[1]/h3[1]/span[1]")
                                            .InnerText.Trim();
                                        string hourSalida =
                                            route.SelectSingleNode(
                                                    "./div[1]/div[1]/div[2]/div[1]/div[2]/h3[1]/span[1]")
                                                .InnerText.Trim();
                                        string dayLlegada =
                                            route.SelectSingleNode(
                                                    "./div[1]/div[1]/div[2]/div[2]/div[1]/h3[1]/span[1]")
                                                .InnerText.Trim();
                                        string hourLlegada =
                                            route.SelectSingleNode(
                                                    "./div[1]/div[1]/div[2]/div[2]/div[2]/h3[1]/span[1]")
                                                .InnerText.Trim();
                                        DateTime datetimeSalida = DateTime.MinValue;
                                        string datetimeSalidastring = daySalida + " " + hourSalida;
                                        datetimeSalida = DateTime.ParseExact(datetimeSalidastring,
                                            "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                                        DateTime datetimeLlegada = DateTime.MinValue;
                                        string datetimeLlegadaString = dayLlegada + " " + hourLlegada;
                                        datetimeLlegada = DateTime.ParseExact(datetimeLlegadaString,
                                            "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                                        string tripdid = RouteNr + TimeofDay;

                                        if (dbroutenr.Substring(0, 4) == RouteNr)
                                        {
                                            Console.WriteLine("Route found...");
                                            bool routeExists =
                                                _Routes.Exists(x => x.RutaNr == RouteNr
                                                                    && x.From == from
                                                                    && x.To == to);
                                            if (!routeExists)
                                            {
                                                _Routes.Add(
                                                    new CIBusRoutes
                                                    {
                                                        RutaNr = RouteNr,
                                                        From = from,
                                                        To = to
                                                    });
                                            }

                                            bool routeDetailsExists =
                                                _RoutesDetails.Exists(x => x.RouteNr == RouteNr
                                                                           && x.TripNr == tripdid
                                                );
                                            if (!routeDetailsExists)
                                            {
                                                _RoutesDetails.Add(new CIBusRoutesDetails
                                                {
                                                    RouteNr = RouteNr,
                                                    TripNr = tripdid,
                                                    Salida = datetimeSalida,
                                                    Llegeda = datetimeLlegada,
                                                    Service = service
                                                });
                                            }
                                            // Route Calender
                                            bool routeCalenderExists =
                                                _RouteCalender.Exists(x => x.TripNr == tripdid
                                                );

                                            Boolean TEMP_Monday = false;
                                            Boolean TEMP_Tuesday = false;
                                            Boolean TEMP_Wednesday = false;
                                            Boolean TEMP_Thursday = false;
                                            Boolean TEMP_Friday = false;
                                            Boolean TEMP_Saterday = false;
                                            Boolean TEMP_Sunday = false;

                                            int dayofweek = Convert.ToInt32(datetimeSalida.DayOfWeek);
                                            if (dayofweek == 0)
                                            {
                                                TEMP_Sunday = true;
                                            }
                                            if (dayofweek == 1)
                                            {
                                                TEMP_Monday = true;
                                            }
                                            if (dayofweek == 2)
                                            {
                                                TEMP_Tuesday = true;
                                            }
                                            if (dayofweek == 3)
                                            {
                                                TEMP_Wednesday = true;
                                            }
                                            if (dayofweek == 4)
                                            {
                                                TEMP_Thursday = true;
                                            }
                                            if (dayofweek == 5)
                                            {
                                                TEMP_Friday = true;
                                            }
                                            if (dayofweek == 6)
                                            {
                                                TEMP_Saterday = true;
                                            }


                                            if (routeCalenderExists)
                                            {
                                                if (dayofweek == 0)
                                                {
                                                    _RouteCalender
                                                            .Find(p => p.TripNr == tripdid).Sunday =
                                                        TEMP_Sunday;
                                                }
                                                if (dayofweek == 1)
                                                {
                                                    _RouteCalender
                                                            .Find(p => p.TripNr == tripdid).Monday =
                                                        TEMP_Monday;
                                                }
                                                if (dayofweek == 2)
                                                {
                                                    _RouteCalender
                                                            .Find(p => p.TripNr == tripdid).Tuesday =
                                                        TEMP_Tuesday;
                                                }
                                                if (dayofweek == 3)
                                                {
                                                    _RouteCalender
                                                            .Find(p => p.TripNr == tripdid).Wednesday =
                                                        TEMP_Wednesday;
                                                }
                                                if (dayofweek == 4)
                                                {
                                                    _RouteCalender
                                                            .Find(p => p.TripNr == tripdid).Thursday =
                                                        TEMP_Thursday;
                                                }
                                                if (dayofweek == 5)
                                                {
                                                    _RouteCalender
                                                            .Find(p => p.TripNr == tripdid).Friday =
                                                        TEMP_Friday;
                                                }
                                                if (dayofweek == 6)
                                                {
                                                    _RouteCalender
                                                            .Find(p => p.TripNr == tripdid).Saterday =
                                                        TEMP_Saterday;
                                                }
                                            }
                                            else
                                            {
                                                _RouteCalender.Add(new CIBusRoutesCalender
                                                {
                                                    TripNr = tripdid,
                                                    Monday = TEMP_Monday,
                                                    Tuesday = TEMP_Tuesday,
                                                    Wednesday = TEMP_Wednesday,
                                                    Thursday = TEMP_Thursday,
                                                    Friday = TEMP_Friday,
                                                    Saterday = TEMP_Saterday,
                                                    Sunday = TEMP_Sunday
                                                });
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Route Passing by...");
                                        }
                                    }
                                }
                            }
                        }
                        rdr.Close();
                    }
                }

                //}
                //catch (SqlException)
                //{
                //    // error here
                //}
                //finally
                //{
                //    connection.Close();
                //}
            }


            //// Export Stops
            //var Cities = _RoutesDetails.Select(m => new { m. }).Distinct().ToList();

            //// You'll do something else with it, here I write it to a console window
            //// Console.WriteLine(text.ToString());
            //Console.WriteLine("Export City into XML...");
            //// Write the list of objects to a file.
            ////System.Xml.Serialization.XmlSerializer writerCities = new System.Xml.Serialization.XmlSerializer(Cities.GetType());
            //string myDir = AppDomain.CurrentDomain.BaseDirectory + "\\output";
            //System.IO.Directory.CreateDirectory(myDir);

            ////System.IO.StreamWriter fileCities = new System.IO.StreamWriter("output\\Cities.xml");

            ////writerCities.Serialize(fileCities, Cities);
            ////fileCities.Close();

            //// You'll do something else with it, here I write it to a console window
            // Console.WriteLine(text.ToString());
            Console.WriteLine("Export Routes into XML...");
            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writerRoutes =
                new System.Xml.Serialization.XmlSerializer(_Routes.GetType());

            System.IO.StreamWriter fileRoutes =
                new System.IO.StreamWriter("output\\routes.xml");

            writerRoutes.Serialize(fileRoutes, _Routes);
            fileRoutes.Close();

            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writerRoutesError =
                new System.Xml.Serialization.XmlSerializer(_RoutesError.GetType());

            System.IO.StreamWriter fileRoutesError =
                new System.IO.StreamWriter("output\\routesError.xml");

            writerRoutesError.Serialize(fileRoutesError, _RoutesError);
            fileRoutesError.Close();

            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writerRoutesNon =
                new System.Xml.Serialization.XmlSerializer(_RoutesNon.GetType());

            System.IO.StreamWriter fileRoutesNon =
                new System.IO.StreamWriter("output\\routesNon.xml");

            writerRoutesError.Serialize(fileRoutesNon, _RoutesNon);
            fileRoutesNon.Close();

            Console.WriteLine("Export Routes Details into XML...");
            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(_RoutesDetails.GetType());
            System.IO.StreamWriter file =
                new System.IO.StreamWriter("output\\routesdetails.xml");

            writer.Serialize(file, _RoutesDetails);
            file.Close();

            // GTFS Support

            string gtfsDir = AppDomain.CurrentDomain.BaseDirectory + "\\gtfs";
            System.IO.Directory.CreateDirectory(gtfsDir);

            Console.WriteLine("Creating GTFS Files...");

            Console.WriteLine("Creating GTFS File agency.txt...");
            using (var gtfsagency = new StreamWriter(@"gtfs\\agency.txt"))
            {
                var csv = new CsvWriter(gtfsagency);
                csv.Configuration.Delimiter = ",";
                csv.Configuration.Encoding = Encoding.UTF8;
                csv.Configuration.TrimFields = true;
                // header 
                csv.WriteField("agency_id");
                csv.WriteField("agency_name");
                csv.WriteField("agency_url");
                csv.WriteField("agency_timezone");
                csv.WriteField("agency_lang");
                csv.WriteField("agency_phone");
                csv.WriteField("agency_fare_url");
                csv.WriteField("agency_email");
                csv.NextRecord();
                csv.WriteField("EC");
                csv.WriteField("Expreso Brasilia");
                csv.WriteField("http://www.expresobrasilia.com/");
                csv.WriteField("America/Bogota");
                csv.WriteField("ES");
                csv.WriteField("+57 01 8000 51 8001");
                csv.WriteField("");
                csv.WriteField("contactenos@expresobrasilia.com ");
                csv.NextRecord();
                csv.WriteField("UN");
                csv.WriteField("Unitransco");
                csv.WriteField("http://www.expresobrasilia.com/");
                csv.WriteField("America/Bogota");
                csv.WriteField("ES");
                csv.WriteField("+57 01 8000 51 8001");
                csv.WriteField("");
                csv.WriteField("contactenos@expresobrasilia.com ");
                csv.NextRecord();
            }

            Console.WriteLine("Creating GTFS File routes.txt ...");

            using (var gtfsroutes = new StreamWriter(@"gtfs\\routes.txt"))
            {
                // Route record


                var csvroutes = new CsvWriter(gtfsroutes);
                csvroutes.Configuration.Delimiter = ",";
                csvroutes.Configuration.Encoding = Encoding.UTF8;
                csvroutes.Configuration.TrimFields = true;
                // header 
                csvroutes.WriteField("route_id");
                csvroutes.WriteField("agency_id");
                csvroutes.WriteField("route_short_name");
                csvroutes.WriteField("route_long_name");
                csvroutes.WriteField("route_desc");
                csvroutes.WriteField("route_type");
                csvroutes.WriteField("route_url");
                csvroutes.WriteField("route_color");
                csvroutes.WriteField("route_text_color");
                csvroutes.NextRecord();

                foreach (CIBusRoutes route in _Routes)
                {
                    csvroutes.WriteField(route.RutaNr);
                    csvroutes.WriteField("EC");
                    csvroutes.WriteField("");
                    csvroutes.WriteField(route.From + " - " + route.To);
                    csvroutes.WriteField(""); 
                    csvroutes.WriteField(202); // 202 nat 201 inter
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.NextRecord();
                }
            }

            using (var gtfstrips = new StreamWriter(@"gtfs\\trips.txt"))
            {
                var csvtrips = new CsvWriter(gtfstrips);
                csvtrips.Configuration.Delimiter = ",";
                csvtrips.Configuration.Encoding = Encoding.UTF8;
                csvtrips.Configuration.TrimFields = true;
                // header 
                csvtrips.WriteField("route_id");
                csvtrips.WriteField("service_id");
                csvtrips.WriteField("trip_id");
                csvtrips.WriteField("trip_headsign");
                csvtrips.WriteField("trip_short_name");
                csvtrips.WriteField("direction_id");
                csvtrips.WriteField("block_id");
                csvtrips.WriteField("shape_id");
                csvtrips.WriteField("wheelchair_accessible");
                csvtrips.WriteField("bikes_allowed ");
                csvtrips.NextRecord();
                foreach (var trip in _RoutesDetails)
                {
                    csvtrips.WriteField(trip.RouteNr.Substring(0, 4));
                    csvtrips.WriteField(trip.TripNr);
                    csvtrips.WriteField(trip.TripNr);
                    csvtrips.WriteField("");
                    csvtrips.WriteField("");
                    csvtrips.WriteField("");
                    csvtrips.WriteField("");
                    csvtrips.WriteField("");
                    csvtrips.WriteField("1");
                    csvtrips.WriteField("");
                    csvtrips.NextRecord();
                }
            }
            using (var gtfscalendar = new StreamWriter(@"gtfs\\calendar.txt"))
            {
                var csvcalendar = new CsvWriter(gtfscalendar);
                csvcalendar.Configuration.Delimiter = ",";
                csvcalendar.Configuration.Encoding = Encoding.UTF8;
                csvcalendar.Configuration.TrimFields = true;
                // header 
                csvcalendar.WriteField("service_id");
                csvcalendar.WriteField("monday");
                csvcalendar.WriteField("tuesday");
                csvcalendar.WriteField("wednesday");
                csvcalendar.WriteField("thursday");
                csvcalendar.WriteField("friday");
                csvcalendar.WriteField("saturday");
                csvcalendar.WriteField("sunday");
                csvcalendar.WriteField("start_date");
                csvcalendar.WriteField("end_date");
                csvcalendar.NextRecord();
                foreach (var calender in _RouteCalender)
                {
                    csvcalendar.WriteField(calender.TripNr);
                    csvcalendar.WriteField(Convert.ToInt32(calender.Monday));
                    csvcalendar.WriteField(Convert.ToInt32(calender.Tuesday));
                    csvcalendar.WriteField(Convert.ToInt32(calender.Wednesday));
                    csvcalendar.WriteField(Convert.ToInt32(calender.Thursday));
                    csvcalendar.WriteField(Convert.ToInt32(calender.Friday));
                    csvcalendar.WriteField(Convert.ToInt32(calender.Saterday));
                    csvcalendar.WriteField(Convert.ToInt32(calender.Sunday));
                    csvcalendar.WriteField("20170801");
                    csvcalendar.WriteField("20180801");
                    csvcalendar.NextRecord();
                }
            }

            using (var gtfsstoptimes = new StreamWriter(@"gtfs\\stop_times.txt"))
            {
                // Headers 
                var csvstoptimes = new CsvWriter(gtfsstoptimes);
                csvstoptimes.Configuration.Delimiter = ",";
                csvstoptimes.Configuration.Encoding = Encoding.UTF8;
                csvstoptimes.Configuration.TrimFields = true;
                // header 
                csvstoptimes.WriteField("trip_id");
                csvstoptimes.WriteField("arrival_time");
                csvstoptimes.WriteField("departure_time");
                csvstoptimes.WriteField("stop_id");
                csvstoptimes.WriteField("stop_sequence");
                csvstoptimes.WriteField("stop_headsign");
                csvstoptimes.WriteField("pickup_type");
                csvstoptimes.WriteField("drop_off_type");
                csvstoptimes.WriteField("shape_dist_traveled");
                csvstoptimes.WriteField("timepoint");
                csvstoptimes.NextRecord();

                foreach (var trip in _RoutesDetails)
                {
                    SqlConnection connectionstoptimes =
                        new SqlConnection(
                            "Server=127.0.0.1;Database=ColombiaInfo-Data;User Id=Mule;Password=P@ssw0rd;");
                    SqlCommand commandstoptimes = new SqlCommand();
                    commandstoptimes.Connection = connectionstoptimes; // <== lacking
                    commandstoptimes.CommandType = CommandType.Text;
                    string stringsql = @"SELECT DISTINCT [ROUTENR]
                                    ,[TRAMOS]
                                    ,[EMPRESA]
                                    ,[EMPRESAN]
                                    ,[AGENCIA]
                                    ,[AGENCIAN]
                                    ,[CIUDADN]
                                    ,[DEPARTAMENTON]
                                    ,[PAISN]
                                    ,[KILOMETROS]
                                    ,[MINUTOS]
                                    ,[Origen_Ciudad_ID]
                                    ,[Destino_Ciudad_ID]
                                    ,[Origen_Ciudad_Nombre]
                                    ,[Destino_Ciudad_Nombre]
                                FROM [ColombiaInfo-Data].[dbo].[BrasiliaRoutes]
                                where exists (  select 1 from ( select ROUTENR, max(TRAMOS) as TRAMOS
                                                            from [BrasiliaRoutes]
                                                            group by ROUTENR
                                                            ) as cond
                                            where [BrasiliaRoutes].ROUTENR=cond.ROUTENR 
                                            and [BrasiliaRoutes].TRAMOS =cond.TRAMOS                                                
                                            )
                                    AND [BrasiliaRoutes].ROUTENR = '@RouteNr'
                            order by ROUTENR, TRAMOS, KILOMETROS";
                    stringsql = stringsql.Replace("@RouteNr", trip.RouteNr.Substring(0, 4));
                    commandstoptimes.CommandText = stringsql;
                    //command.Parameters.AddWithValue("@RouteNr", trip.RouteNr.Substring(0, 4));
                    //try
                    //{
                    connectionstoptimes.Open();
                    SqlDataReader rdrstoptimes = commandstoptimes.ExecuteReader();

                    int loopnumber = 0;
                    if (!rdrstoptimes.HasRows)
                    {
                        Console.WriteLine("Geen route gevonden in de database");
                    }
                    while (rdrstoptimes.Read())
                    {
                        // Get the agency and the stop details. Add them to a list.
                        string stop_id;
                        // todo: Lookup first to no overload the api.

                        // Check if city and agency name are the same. If not then use agencyname against api.
                        string citynameapi;
                        if (rdrstoptimes["CIUDADN"].ToString() == rdrstoptimes["AGENCIAN"].ToString())
                        {
                            citynameapi = rdrstoptimes["CIUDADN"].ToString();
                        }
                        else
                        {
                            citynameapi = rdrstoptimes["AGENCIAN"].ToString();
                        }

                        bool GTFSSTopExists =
                            _GTFSStops.Exists(x => x.orgcity == citynameapi.Trim());
                        if (!GTFSSTopExists)
                        {
                            using (var clientFrom = new WebClient())
                            {
                                clientFrom.Encoding = Encoding.UTF8;
                                clientFrom.Headers.Add("user-agent", ua);
                                string cityname = HttpUtility.UrlEncode(citynameapi.Trim());
                                if (cityname.EndsWith("."))
                                {
                                    cityname = cityname.Remove(cityname.Length - 1);
                                }
                                cityname = cityname.Replace("+", "%20");
                                string urlapiFrom = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathBus +
                                                    "EC/" + cityname;
                                var jsonapiFrom = clientFrom.DownloadString(urlapiFrom);
                                dynamic AirportResponseJsonFrom = JsonConvert.DeserializeObject(jsonapiFrom);
                                _GTFSStops.Add(new GTFSStops
                                {
                                    stop_id = Convert.ToString(AirportResponseJsonFrom[0].stop_id),
                                    stop_code = Convert.ToString(AirportResponseJsonFrom[0].stop_code),
                                    stop_name = Convert.ToString(AirportResponseJsonFrom[0].stop_name),
                                    stop_desc = Convert.ToString(AirportResponseJsonFrom[0].stop_desc),
                                    stop_lat = Convert.ToString(AirportResponseJsonFrom[0].stop_lat),
                                    stop_lon = Convert.ToString(AirportResponseJsonFrom[0].stop_lon),
                                    stop_timezone = Convert.ToString(AirportResponseJsonFrom[0].stop_timezone),
                                    stop_url = Convert.ToString(AirportResponseJsonFrom[0].stop_url),
                                    wheelchair_boarding =
                                        Convert.ToString(AirportResponseJsonFrom[0].wheelchair_boarding),
                                    zone_id = Convert.ToString(AirportResponseJsonFrom[0].zone_id),
                                    location_type = Convert.ToString(AirportResponseJsonFrom[0].location_type),
                                    parent_station = Convert.ToString(AirportResponseJsonFrom[0].location_type),
                                    orgcity = citynameapi.Trim()
                                });
                                stop_id = Convert.ToString(AirportResponseJsonFrom[0].stop_id);
                            }
                        }
                        else
                        {
                            var stopinfo =
                                _GTFSStops.FirstOrDefault(y => y.orgcity == citynameapi.Trim());
                            stop_id = stopinfo.stop_id;
                        }

                        int addminutes = (int) rdrstoptimes["MINUTOS"];

                        DateTime stoptime = trip.Salida.AddMinutes(addminutes);
                        bool NextDayArrival;
                        NextDayArrival = trip.Salida.Date != stoptime.Date;
                        if (!NextDayArrival)

                        {
                            csvstoptimes.WriteField(trip.TripNr);
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", stoptime));
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", stoptime));
                            csvstoptimes.WriteField(stop_id);
                            csvstoptimes.WriteField(loopnumber.ToString());
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("");
                            csvstoptimes.NextRecord();
                        }
                        else
                        {
                            int hour = stoptime.Hour;
                            hour = hour + 24;
                            int minute = stoptime.Minute;
                            string strminute = minute.ToString();
                            if (strminute.Length == 1)
                            {
                                strminute = "0" + strminute;
                            }
                            csvstoptimes.WriteField(trip.TripNr);
                            csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                            csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                            csvstoptimes.WriteField(stop_id);
                            csvstoptimes.WriteField(loopnumber.ToString());
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("");
                            csvstoptimes.NextRecord();
                        }
                        loopnumber = loopnumber + 1;
                    }
                    rdrstoptimes.Close();
                    connectionstoptimes.Close();
                }
            }

            // stops
            using (var gtfsstops = new StreamWriter(@"gtfs\\stops.txt"))
            {
                // Route record
                var csvstops = new CsvWriter(gtfsstops);
                csvstops.Configuration.Delimiter = ",";
                csvstops.Configuration.Encoding = Encoding.UTF8;
                csvstops.Configuration.TrimFields = true;
                // header                                 
                csvstops.WriteField("stop_id");
                csvstops.WriteField("stop_code");
                csvstops.WriteField("stop_name");
                csvstops.WriteField("stop_desc");
                csvstops.WriteField("stop_lat");
                csvstops.WriteField("stop_lon");
                csvstops.WriteField("zone_id");
                csvstops.WriteField("stop_url");
                csvstops.WriteField("stop_timezone");
                csvstops.NextRecord();

                foreach (var GTFSStops in _GTFSStops.Distinct().ToList())
                {
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_id));
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_code));
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_name));
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_desc));
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_lat));
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_lon));
                    csvstops.WriteField(Convert.ToString(GTFSStops.zone_id));
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_url));
                    csvstops.WriteField(Convert.ToString(GTFSStops.stop_timezone));
                    csvstops.NextRecord();
                }
            }
        }

        [Serializable]
        public class GTFSStops
        {
            public string stop_id { get; set; }
            public string stop_code { get; set; }
            public string stop_name { get; set; }
            public string stop_desc { get; set; }
            public string stop_lat { get; set; }
            public string stop_lon { get; set; }
            public string zone_id { get; set; }
            public string stop_url { get; set; }
            public string location_type { get; set; }
            public string parent_station { get; set; }
            public string stop_timezone { get; set; }
            public string wheelchair_boarding { get; set; }
            public string orgcity { get; set; }
        }


        [Serializable]
        public class CIBusOrigens
        {
            // Auto-implemented properties. 

            public string Ciudad_ID;
            public string Ciudad_Nombre;
        }

        [Serializable]
        public class CIBusOrigensDestino
        {
            // Auto-implemented properties. 

            public string Origen_Ciudad_ID;
            public string Origen_Ciudad_Nombre;
            public string Destino_Ciudad_ID;
            public string Destino_Ciudad_Nombre;
        }

        [Serializable]
        public class CIBusTramo
        {
            // Auto-implemented properties. 

            public string Origen_Ciudad_ID;
            public string Origen_Ciudad_Nombre;
            public string Destino_Ciudad_ID;
            public string Destino_Ciudad_Nombre;
        }

        [Serializable]
        public class CIBusTramoSteps
        {
            // Auto-implemented properties. 

            public string RutaNr;
            public int Steps;
        }

        [Serializable]
        public class CIBusRoutes
        {
            // Auto-implemented properties. 

            public string RutaNr;
            public string From;
            public string To;
        }

        [Serializable]
        public class CIBusRoutesDetails
        {
            // Auto-implemented properties. 

            public string RouteNr;
            public string TripNr;
            public DateTime Salida;
            public DateTime Llegeda;
            public string Service;
        }

        [Serializable]
        public class CIBusRoutesCalender
        {
            // Auto-implemented properties. 
            public string TripNr;

            public Boolean Monday;
            public Boolean Tuesday;
            public Boolean Wednesday;
            public Boolean Thursday;
            public Boolean Friday;
            public Boolean Saterday;
            public Boolean Sunday;
        }
    }
}