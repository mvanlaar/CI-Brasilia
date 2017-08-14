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

            bool fullrun = false;

            var export = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = export;
            Thread.CurrentThread.CurrentUICulture = export;



            List<CIBusOrigens> _Origens = new List<CIBusOrigens> { };
            List<CIBusOrigensDestino> _OrigensDestino = new List<CIBusOrigensDestino> { };
            List<CIBusTramoSteps> _TramoSteps = new List<CIBusTramoSteps> { };
            List<CIBusRoutes> _Routes = new List<CIBusRoutes> { };
            List<CIBusRoutesDetails> _RoutesDetails = new List<CIBusRoutesDetails> { };
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

            // TEMP
            // TODO:
            string gtfsDir = AppDomain.CurrentDomain.BaseDirectory + "\\gtfs";
            System.IO.Directory.CreateDirectory(gtfsDir);
            //using (
            var gtfstrips = new StreamWriter(@"gtfs\\trips.txt");//)
            //{

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
           // }


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

                                string from = (string) rdr["Origen_Ciudad_Nombre"];
                                string to = (string) rdr["Destino_Ciudad_Nombre"];
                                string dbroutenr = (string) rdr["ROUTENR"];
                                
                                if (from.Contains("-"))
                                {
                                    // Split by - use only first part
                                    if (!from.Contains("TULUA -") | !from.Contains("CARTAGO -"))
                                    {
                                        string[] temp = from.Split('-');
                                        from = temp[0];
                                    }

                                }
                                if (to.Contains("-"))
                                {
                                // Split by - use only first part handle execption 
                                    if (!to.Contains("TULUA -") | !to.Contains("CARTAGO -"))
                                    {                                        
                                        string[] temp = to.Split('-');
                                        to = temp[0];
                                    }

                                }

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
                                using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
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
                                using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
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
                                using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
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
                                using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
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
                                using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
                                {
                                    Stage4 = reader.ReadToEnd();
                                }

                                // Stage 5
                                DateTime requestdate = DateTime.Now;
                                requestdate = requestdate.AddDays(7);
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
                                using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
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
                                using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
                                {
                                    Stage6 = reader.ReadToEnd();
                                }

                                // Get Route Listing:

                                dynamic Stage5Response = JsonConvert.DeserializeObject(Stage5);

                                if (Stage5Response.jforms.webShowDocument != null)
                                {
                                    string RedirectUrl = Stage5Response.jforms.webShowDocument[0].urlOpen.ToString();

                                    if (!RedirectUrl.Contains("NOAVAILABLE.xhtml"))
                                    {
                                        request = (HttpWebRequest)WebRequest.Create(RedirectUrl);
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
                                        string Stage7 = String.Empty;
                                        using (HttpWebResponse responseIndex = (HttpWebResponse)request.GetResponse())
                                        using (StreamReader reader = new StreamReader(responseIndex.GetResponseStream()))
                                        {
                                            Stage7 = reader.ReadToEnd();
                                        }

                                        var doc = new HtmlDocument();
                                        doc.LoadHtml(Stage7);

                                        var routes = doc.DocumentNode.SelectNodes("//div[@class='route-widget']");

                                        foreach (var route in routes)
                                        {
                                            string routenrwithtime =
                                                route.SelectSingleNode("./div[1]/div[1]/div[1]/div[1]/div[1]/h3[1]/span[1]")
                                                    .InnerText.Trim();
                                            string[] routenrwithtimeparts = routenrwithtime.Split('-');
                                            String RouteNr = routenrwithtimeparts[0];
                                            string TimeofDay = routenrwithtimeparts[1];
                                            RouteNr = RouteNr.Trim();
                                            TimeofDay = TimeofDay.Trim();
                                            // First part if departure
                                            string daySalida = route
                                                .SelectSingleNode("./div[1]/div[1]/div[2]/div[1]/div[1]/h3[1]/span[1]")
                                                .InnerText.Trim();
                                            string hourSalida =
                                                route.SelectSingleNode("./div[1]/div[1]/div[2]/div[1]/div[2]/h3[1]/span[1]")
                                                    .InnerText.Trim();
                                            string dayLlegada =
                                                route.SelectSingleNode("./div[1]/div[1]/div[2]/div[2]/div[1]/h3[1]/span[1]")
                                                    .InnerText.Trim();
                                            string hourLlegada =
                                                route.SelectSingleNode("./div[1]/div[1]/div[2]/div[2]/div[2]/h3[1]/span[1]")
                                                    .InnerText.Trim();
                                            DateTime datetimeSalida = DateTime.MinValue;
                                            string datetimeSalidastring = daySalida + " " + hourSalida;
                                            datetimeSalida = DateTime.ParseExact(datetimeSalidastring, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                                            DateTime datetimeLlegada = DateTime.MinValue;
                                            string datetimeLlegadaString = dayLlegada + " " + hourLlegada;
                                            datetimeLlegada = DateTime.ParseExact(datetimeLlegadaString, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                                            string tripdid = RouteNr + TimeofDay + requestdate.ToString("ddMMyyyy");
                                            _RoutesDetails.Add(new CIBusRoutesDetails
                                            {
                                                RouteNr = RouteNr,
                                                TripNr = tripdid,
                                                Salida = datetimeSalida,
                                                Llegeda = datetimeLlegada
                                            });







                                        }
                                    }
                                }
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



                //bool NextdayArrival = datetimeSalida.Date != datetimeLlegada.Date;
                //Boolean TEMP_FlightMonday = false;
                //Boolean TEMP_FlightTuesday = false;
                //Boolean TEMP_FlightWednesday = false;
                //Boolean TEMP_FlightThursday = false;
                //Boolean TEMP_FlightFriday = false;
                //Boolean TEMP_FlightSaterday = false;
                //Boolean TEMP_FlightSunday = false;

                //int dayofweek = Convert.ToInt32(datetimeSalida.DayOfWeek);
                //if (dayofweek == 0)
                //{
                //    TEMP_FlightSunday = true;
                //}
                //if (dayofweek == 1)
                //{
                //    TEMP_FlightMonday = true;
                //}
                //if (dayofweek == 2)
                //{
                //    TEMP_FlightTuesday = true;
                //}
                //if (dayofweek == 3)
                //{
                //    TEMP_FlightWednesday = true;
                //}
                //if (dayofweek == 4)
                //{
                //    TEMP_FlightThursday = true;
                //}
                //if (dayofweek == 5)
                //{
                //    TEMP_FlightFriday = true;
                //}
                //if (dayofweek == 6)
                //{
                //    TEMP_FlightSaterday = true;
                //}





                // Parse Response Stage 7

                // Select all divs with the class route-widget

                // Parse route number

                // Is this the route number that we know? 
                // No? Hmm
                // Yes Ok parse the begin and end time. Parse the day of arrival
                // use the stops we kno to create the route.
                // Save the files.

                //// Export Stops
                //var Cities = _RoutesDetails.Select(m => new { m.CIUDADN }).Distinct().ToList();

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
                //// Console.WriteLine(text.ToString());
                //Console.WriteLine("Export Routes into XML...");
                //// Write the list of objects to a file.
                //System.Xml.Serialization.XmlSerializer writerRoutes =
                //new System.Xml.Serialization.XmlSerializer(_Routes.GetType());

                //System.IO.StreamWriter fileRoutes =
                //    new System.IO.StreamWriter("output\\routes.xml");

                //writerRoutes.Serialize(fileRoutes, _Routes);
                //fileRoutes.Close();

                Console.WriteLine("Export Routes Details into XML...");
                // Write the list of objects to a file.
                System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(_RoutesDetails.GetType());
                System.IO.StreamWriter file =
                    new System.IO.StreamWriter("output\\routesdetails.xml");

                writer.Serialize(file, _RoutesDetails);
                file.Close();

            }
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
        }

    }
}

