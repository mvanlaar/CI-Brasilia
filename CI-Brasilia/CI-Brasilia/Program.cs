using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace CI_Brasilia
{
    class Program
    {
        static void Main(string[] args)
        {
            const string ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
            List<CIBusOrigens> _Origens = new List<CIBusOrigens> { };
            List<CIBusOrigensDestino> _OrigensDestino = new List<CIBusOrigensDestino> { };
            List<CIBusTramoSteps> _TramoSteps = new List<CIBusTramoSteps> { };
            List<CIBusRoutes> _Routes = new List<CIBusRoutes> { };
            List<CIBusRoutesDetails> _RoutesDetails = new List<CIBusRoutesDetails> { };
            string CityOrigens = String.Empty;
            Console.WriteLine("Retreiving from locations...");
            using (var webClient = new System.Net.WebClient())
            {
                webClient.Headers.Add("user-agent", ua);
                webClient.Headers.Add("Referer", "http://www.expresobrasilia.com/en/cobertura");                
                CityOrigens = webClient.DownloadString("http://186.118.168.234:8888/BrasiliaWS2Rest/Brasilia/getOrigen?NitConvenio=890100531");
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
                    _Origens.Add(new CIBusOrigens { Ciudad_ID = Origen_CIUDAD_ID, Ciudad_Nombre = Origen_CIUDAD_NOMBRE });
                }
            }
            // Loop through possible orgin for destino combo's
            Console.WriteLine("Parsing through the from to get the destionations for each from locations...");
            Parallel.ForEach(_Origens, new ParallelOptions { MaxDegreeOfParallelism = 10 }, Origen =>
            {
                Console.WriteLine("From: {0}", Origen.Ciudad_Nombre);
                string CityDestinos = String.Empty;
                using (var webClient = new System.Net.WebClient())
                {
                    webClient.Headers.Add("user-agent", ua);
                    webClient.Headers.Add("Referer", "http://www.expresobrasilia.com/en/cobertura");
                    CityDestinos = webClient.DownloadString("http://186.118.168.234:8888/BrasiliaWS2Rest/Brasilia/getDestino?NitConvenio=890100531&CodOrigen=" + Origen.Ciudad_ID);
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
                        _OrigensDestino.Add(new CIBusOrigensDestino { Origen_Ciudad_ID = Origen.Ciudad_ID, Origen_Ciudad_Nombre = Origen.Ciudad_Nombre, Destino_Ciudad_ID = Destino_CIUDAD_ID, Destino_Ciudad_Nombre = Destino_CIUDAD_NOMBRE });
                    }
                }
            });
            Console.WriteLine("Parsing througg the possible routes...");
            // , new ParallelOptions { MaxDegreeOfParallelism = 10 }, (Day) =>
            //{
            Parallel.ForEach(_OrigensDestino, new ParallelOptions { MaxDegreeOfParallelism = 2 }, FromToCombo =>
            {
           Console.WriteLine("From: {0} to {1}", FromToCombo.Origen_Ciudad_Nombre, FromToCombo.Destino_Ciudad_Nombre);
           string Tramo = String.Empty;
           string TramoUrl = String.Format("http://186.118.168.234:8888/BrasiliaWS2Rest/Brasilia/getConsultarTramo?NitConvenio=890100531&CodOrigen={0}&CodDestino={1}", FromToCombo.Origen_Ciudad_ID, FromToCombo.Destino_Ciudad_ID);
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
                   bool alreadyExists = _TramoSteps.Exists(x => x.RutaNr == Routenr.ToString());
                   if (!alreadyExists)
                   {
                            // Add Information to list
                            _TramoSteps.Add(new CIBusTramoSteps { RutaNr = Routenr.ToString(), Steps = TramosTotal });
                       _Routes.Add(new CIBusRoutes { RutaNr = Routenr.ToString(), From = FromToCombo.Origen_Ciudad_Nombre, To = FromToCombo.Destino_Ciudad_Nombre });
                            // Process the xml.
                            XmlNodeList nodes = xmlDocTramo.DocumentElement.SelectNodes("/Tramos/Record");
                       foreach (XmlNode noderecord in nodes)
                       {
                           _RoutesDetails.Add(new CIBusRoutesDetails
                           {
                               EMPRESA = noderecord.SelectSingleNode("EMPRESA").InnerText,
                               EMPRESAN = noderecord.SelectSingleNode("EMPRESAN").InnerText,
                               AGENCIA = noderecord.SelectSingleNode("AGENCIA").InnerText,
                               AGENCIAN = noderecord.SelectSingleNode("AGENCIAN").InnerText,
                               CIUDADN = noderecord.SelectSingleNode("CIUDADN").InnerText,
                               DEPARTAMENTON = noderecord.SelectSingleNode("DEPARTAMENTON").InnerText,
                               PAISN = noderecord.SelectSingleNode("PAISN").InnerText,
                               RUTA = noderecord.SelectSingleNode("RUTA").InnerText,
                               KILOMETROS = noderecord.SelectSingleNode("KILOMETROS").InnerText,
                               MINUTOS = noderecord.SelectSingleNode("MINUTOS").InnerText,
                           });
                       }
                   }
                   else
                   {
                            // Check if route steps is larger then current.
                            var CurrentRoute = _TramoSteps.Find(p => p.RutaNr == Routenr.ToString());
                       if (TramosTotal > CurrentRoute.Steps)
                       {
                                // Only when it's larger then current route steps process the xml
                                // Delete The route name from Table
                                var itemToRemove = _Routes.Single(r => r.RutaNr == Routenr.ToString());
                           _Routes.Remove(itemToRemove);
                                // Add New route
                                _Routes.Add(new CIBusRoutes { RutaNr = Routenr.ToString(), From = FromToCombo.Origen_Ciudad_Nombre, To = FromToCombo.Destino_Ciudad_Nombre });
                                // Process the xml
                                XmlNodeList nodes = xmlDocTramo.DocumentElement.SelectNodes("/Tramos/Record");
                           foreach (XmlNode noderecord in nodes)
                           {
                               _RoutesDetails.Add(new CIBusRoutesDetails
                               {
                                   EMPRESA = noderecord.SelectSingleNode("EMPRESA").InnerText,
                                   EMPRESAN = noderecord.SelectSingleNode("EMPRESAN").InnerText,
                                   AGENCIA = noderecord.SelectSingleNode("AGENCIA").InnerText,
                                   AGENCIAN = noderecord.SelectSingleNode("AGENCIAN").InnerText,
                                   CIUDADN = noderecord.SelectSingleNode("CIUDADN").InnerText,
                                   DEPARTAMENTON = noderecord.SelectSingleNode("DEPARTAMENTON").InnerText,
                                   PAISN = noderecord.SelectSingleNode("PAISN").InnerText,
                                   RUTA = noderecord.SelectSingleNode("RUTA").InnerText,
                                   KILOMETROS = noderecord.SelectSingleNode("KILOMETROS").InnerText,
                                   MINUTOS = noderecord.SelectSingleNode("MINUTOS").InnerText,
                               });
                           }
                       }
                            // End Exists
                        }
                        // End Route Parsing
                    }
                    // End total exists checking
                }
                // End Route Parsing
            });

            // You'll do something else with it, here I write it to a console window
            // Console.WriteLine(text.ToString());
            Console.WriteLine("Export Routes into XML...");
            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writerRoutes =
            new System.Xml.Serialization.XmlSerializer(_Routes.GetType());
            string myDir = AppDomain.CurrentDomain.BaseDirectory + "\\output";
            System.IO.Directory.CreateDirectory(myDir);

            System.IO.StreamWriter fileRoutes =
                new System.IO.StreamWriter("output\\routes.xml");

            writerRoutes.Serialize(fileRoutes, _Routes);
            fileRoutes.Close();
            
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

        public string EMPRESA;
        public string EMPRESAN;
        public string AGENCIA;
        public string AGENCIAN;
        public string CIUDADN;
        public string DEPARTAMENTON;
        public string PAISN;
        public string RUTA;
		public string KILOMETROS;
		public string MINUTOS;    
        }

}

