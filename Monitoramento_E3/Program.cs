using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Projeto_Classes.Classes;
using Projeto_Classes.Classes.Gerencial;
using System.Data;
using System.Xml;
using System.Collections;

namespace Monitoramento_E3
{
    class Program
    {
        private static SortedDictionary<string, TcpClient> socket_rastreadores = new SortedDictionary<string, TcpClient>();
        private static ArrayList contas = new ArrayList();

        //private static int i = 0;
        private static void Main()
        {

            #region Contas HERE

            XmlDocument xDoc = new XmlDocument();
            xDoc.Load("http://rastrear.a3rastreadores.com.br/contas_here/contas_here.xml");

            XmlNodeList coluna = xDoc.GetElementsByTagName("coluna");
            XmlNodeList app_id = xDoc.GetElementsByTagName("app_id");
            XmlNodeList app_code = xDoc.GetElementsByTagName("app_code");
            XmlNodeList inicio = xDoc.GetElementsByTagName("inicio");
            XmlNodeList fim = xDoc.GetElementsByTagName("fim");

            for (int i = 0; i < coluna.Count; i++)
            {
                ArrayList itens = new ArrayList();
                itens.Add(coluna[i].InnerText);
                itens.Add(app_id[i].InnerText);
                itens.Add(app_code[i].InnerText);
                itens.Add(inicio[i].InnerText);
                itens.Add(fim[i].InnerText);
                contas.Add(itens);
            }

            #endregion

            TcpListener socket;
            socket = new TcpListener(IPAddress.Any, 7003);
            try
            {
                Console.WriteLine("Conectado!");

                socket.Start();                

                while (true)
                {
                    TcpClient client = socket.AcceptTcpClient();

                    Thread tcpListenThread = new Thread(TcpListenThread);
                    tcpListenThread.Start(client);


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n" + ex.Message);
                //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta E3 - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
            finally
            {
                Thread tcpListenThread = new Thread(Main);
                tcpListenThread.Start();
                socket.Stop();
            }


        }

        private static void TcpListenThread(object param)
        {

            TcpClient client = (TcpClient)param;
            NetworkStream stream;
            stream = client.GetStream();
            
            //Thread tcpLSendThread = new Thread(new ParameterizedThreadStart(TcpLSendThread));

            Byte[] bytes = new Byte[99999];
            String mensagem_traduzida;
            string id = "-1";
            //int x = 0;
            int i;
            bool from_raster = true;
            stream.ReadTimeout = 1200000;
            try
            {

                while (from_raster && (i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    //Console.WriteLine(Encoding.UTF8.GetString(bytes, 0, i));
                    mensagem_traduzida = Encoding.UTF8.GetString(bytes, 0, i);
                    
                    //var a = 
                    /*
                    0 "\\*..," +                          // Manufacturer
                    1 "(\\d+)," +                         // IMEI
                    2 "([^,]{2})," +                      // Command
                    3 "([AV])," +                         // Validity
                    4 "(\\p{XDigit}{2})" +                // Year
                      "(\\p{XDigit}{2})" +                // Month
                      "(\\p{XDigit}{2})," +               // Day
                    5 "(\\p{XDigit}{2})" +                // Hours
                      "(\\p{XDigit}{2})" +                // Minutes
                      "(\\p{XDigit}{2})," +               // Seconds
                    6 "(\\p{XDigit})" +
                      "(\\p{XDigit}{7})," +               // Latitude
                    7 "(\\p{XDigit})" +
                      "(\\p{XDigit}{7})," +               // Longitude
                    8 "(\\p{XDigit}{4})," +               // Speed
                    9 "(\\p{XDigit}{4})," +               // Course
                   10 "(\\p{XDigit}{8})," +               // Status
                   11 "(\\d+)," +                         // Signal
                   12 "(\\d+)," +                         // Power
                   13 "(\\p{XDigit}{4})," +               // Oil
                   14 "(\\p{XDigit}+),?" +                // Milage
                   15 "(\\d+)?" +                         // Altitude
                    ".*");
                    */

                    var mensagem = mensagem_traduzida.Split(',').ToList();

                    //mensagem original
                    /*StreamWriter wr = new StreamWriter("mensagem_original.txt", true);
                    wr.WriteLine(mensagem_traduzida);
                    wr.Close();*/

                    //Console.WriteLine(mensagem_traduzida);

                    if (mensagem[0].Equals("CLIENTE", StringComparison.InvariantCultureIgnoreCase))
                    {
                        from_raster = false;
                        NetworkStream sender;
                        if (socket_rastreadores.Keys.Contains(mensagem[1]))
                        {
                            sender = socket_rastreadores[mensagem[1]].GetStream();
                            Byte[] bytes_send = Encoding.UTF8.GetBytes(mensagem[2]);
                            sender.Write(bytes_send, 0, bytes_send.Length);
                            //stream.ReadTimeout = 1000;
                        }
                        else
                        {
                            Console.WriteLine("\n KEY NAO EXIST:" + mensagem[1]);
                        }
                    }
                    else
                    {
                        if (mensagem[2].Contains("MQ")) // || mensagem[2].Contains("JZ"))
                        {

                            NetworkStream sender;
                            sender = client.GetStream();
                            mensagem_traduzida = "*ET," + mensagem[1] + ",MQ#";
                            Byte[] bytes_send = Encoding.UTF8.GetBytes(mensagem_traduzida);
                            sender.Write(bytes_send, 0, bytes_send.Length);
                            //stream.ReadTimeout = 1000;
                        }

                        id = mensagem[1];
                        if (socket_rastreadores.ContainsKey(id))
                        {
                            socket_rastreadores[id] = client;
                        }
                        else
                        {
                            socket_rastreadores.Add(id, client);
                        }
                        if (mensagem.Count == 16)
                            Interpretar_Msg(mensagem);

                        //if (!tcpLSendThread.IsAlive)
                        //   tcpLSendThread.Start(new Tuple<NetworkStream, string>(stream, mensagem[1]));
                    }

                }
            }
            catch (Exception ex)
            {
                //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta E3 - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                Console.WriteLine("\n" + ex.Message);
                client.Close();
            }
            client.Close();
        }
        
        private static void Interpretar_Msg(List<String> mensagem)
        {
            string id = "";

            try
            {
                //bool gravar = false;
                id = mensagem[1];
                Rastreador r = new Rastreador();
                r.PorId(id);

                Mensagens m = new Mensagens();

                #region Ignição/Sirene/Bloqueio
                char[] s = "000000".ToCharArray();
                s[0] = mensagem[10][0] == '4' || mensagem[10][2] == '8' ? '1' : '0'; //CArro em movimento ou somente com a ignição ligada
                s[4] = mensagem[10][3] == '8' ? '1' : '0'; //cut off engine power (Bloqueio)
                string status = new string(s);
                #endregion

                #region Preenchendo Objeto
                m.Data_Rastreador = "20" + int.Parse(mensagem[4].Substring(0, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + "/" + //ANO
                                            int.Parse(mensagem[4].Substring(2, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + "/" + //MÊS
                                            int.Parse(mensagem[4].Substring(4, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + " " + //DIA
                                            int.Parse(mensagem[5].Substring(0, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + ":" + //horada
                                            int.Parse(mensagem[5].Substring(2, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + ":" + //min
                                            int.Parse(mensagem[5].Substring(4, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0');        //seg

                m.Data_Gps = "20" + int.Parse(mensagem[4].Substring(0, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + "/" + //ANO
                                    int.Parse(mensagem[4].Substring(2, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + "/" + //MÊS
                                    int.Parse(mensagem[4].Substring(4, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + " " + //DIA
                                    int.Parse(mensagem[5].Substring(0, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + ":" + //horada
                                    int.Parse(mensagem[5].Substring(2, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + ":" + //min
                                    int.Parse(mensagem[5].Substring(4, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0');        //seg

                m.Data_Recebida = DateTime.Now.ToString();
                m.ID_Rastreador = id;
                m.Ras_codigo = r.Codigo;
                m.Tipo_Mensagem = "STT";
                m.Latitude = (mensagem[6][0] == '8' ? "-" : "+") + (int.Parse(mensagem[6].Substring(1), System.Globalization.NumberStyles.HexNumber) / 600000.00).ToString().Replace(',', '.');
                m.Longitude = (mensagem[7][0] == '8' ? "-" : "+") + (int.Parse(mensagem[7].Substring(1), System.Globalization.NumberStyles.HexNumber) / 600000.00).ToString().Replace(',', '.');
                m.Hodometro = Convert.ToString(int.Parse(mensagem[14], System.Globalization.NumberStyles.HexNumber) / 10);
                m.Tensao = mensagem[12];    
                m.Velocidade = Convert.ToString(Convert.ToInt32(UInt16.Parse(mensagem[8].ToString(), NumberStyles.HexNumber)) / 100);
                m.Vei_codigo = r.Vei_codigo != 0 ? r.Vei_codigo : 0;
                m.Tipo_Alerta = "";
                m.CodAlerta = 0;
                m.Bloqueio = status[4] == '1' ? true : false;
                m.Sirene = status[5] == '1' ? true : false;
                //m.Ignicao = s[0] == '1' ? true : false;
                m.Ignicao = Convert.ToInt32(m.Velocidade) > 0 ? true : false;
                m.Horimetro = 0;
                

                m.Mensagem = "ET01;" + //Modelo do equipamento
                              id + //IMEI
                             ";04;393;"
                             +
                             "20" + int.Parse(mensagem[4].Substring(0, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') +  //ANO
                                    int.Parse(mensagem[4].Substring(2, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') +  //MÊS
                                    int.Parse(mensagem[4].Substring(4, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') +  //DIA
                             ";" +
                                    int.Parse(mensagem[5].Substring(0, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + ":" + //hora
                                    int.Parse(mensagem[5].Substring(2, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0') + ":" + //min
                                    int.Parse(mensagem[5].Substring(4, 2), System.Globalization.NumberStyles.HexNumber).ToString().PadLeft(2, '0')         //seg
                             + ";0000;"
                             + m.Latitude + ";"
                             + m.Longitude + ";"
                             + m.Velocidade //velocidade
                             + ";0;0;0;"
                             + m.Hodometro + ";" //hodometro (KM)
                             + m.Tensao + ";" //Power -> Tensão
                             + status + ";" //Ignição/bloqueio/Sirene
                             + s[0] //ignição
                             + ";0;0;0;0";

                //m.Endereco = Mensagens.RequisitarEndereco(m.Latitude, m.Longitude);
                m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude, contas);
                #endregion

                Console.WriteLine("\n" + m.Mensagem);

                #region Eventos
                if (mensagem[10][3] == '4')
                {
                    m.Tipo_Alerta = "Energia Principal Removida";
                    m.CodAlerta = 2;
                    m.Tipo_Mensagem = "EMG";

                }
                else if (mensagem[10][1] == '8')
                {
                    m.Tipo_Alerta = "Bateria Interna Desligada";
                    m.CodAlerta = 19;
                    m.Tipo_Mensagem = "EMG";
                }
                #endregion

                #region Gravar
                if (m.Gravar())
                {
                    //gravar = false;
                    m.Tipo_Mensagem = "EMG";
                    if (r.veiculo != null)
                    {
                        Mensagens.EventoAreaCerca(m);

                        //Evento Por E-mail
                        var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                        Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                    }


                    #region Velocidade
                    if (r.Vei_codigo != 0)
                    {
                        var veiculo = Veiculo.BuscarVeiculoVelocidade(m.Vei_codigo);
                        var velocidade_nova = Convert.ToDecimal(veiculo.vei_velocidade);
                        if (velocidade_nova < Convert.ToDecimal(m.Velocidade) && velocidade_nova > 0)
                        {
                            m.Tipo_Mensagem = "EVT";
                            m.Tipo_Alerta = "Veículo Ultrapassou a Velocidade";
                            m.CodAlerta = 23;
                            m.GravarEvento();

                            //Evento Por E-mail
                            var corpoEmail = m.Tipo_Alerta + "<br /> Velocidade: " + m.Velocidade + "<br /> Endereço: " + m.Endereco;
                            Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                        }
                    }
                    #endregion

                }
                #endregion

            }
            catch (Exception ex)
            {
                //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta E3 - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                /*StreamWriter wr = new StreamWriter("Erro interpretacao.txt", true);
                wr.WriteLine(string.Format("ERRO:{0} /n DATA:{1} ID:{2} LOCAL:{3}", e.ToString(), DateTime.Now, id, e.StackTrace));
                wr.Close();*/
            }

        }

    }
}
