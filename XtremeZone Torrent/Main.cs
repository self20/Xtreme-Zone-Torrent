using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Wox.Plugin;

namespace FileList_Torrent {
    public class Main : IPlugin {
        PluginInitContext _context;
        const string URL = "http://myxz.org/takelogin.php";
        CookieContainer mycookie = new CookieContainer();
        HttpWebRequest wr;
        HttpWebResponse response;
        StreamReader sReader;
        bool loggedIn;
        static string pathUser = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
        string pathDownload = Path.Combine( pathUser, "Downloads\\" );

        public void login ( string username, string password ) {


            string postData = String.Format( "username={0}&password={1}", username, password );
            byte[] postBytes = Encoding.ASCII.GetBytes( postData );

            wr = (HttpWebRequest)WebRequest.Create( URL );
            wr.KeepAlive = true;
            wr.Method = "POST";
            wr.AllowAutoRedirect = true;
            wr.ContentType = "application/x-www-form-urlencoded";
            wr.ContentLength = postBytes.Length;
            wr.CookieContainer = mycookie;

            Stream wrStream = wr.GetRequestStream();
            wrStream.Write( postBytes, 0, postBytes.Length );
            wrStream.Close();

            response = (HttpWebResponse)wr.GetResponse();

            sReader = new StreamReader( response.GetResponseStream() );

            if ( response.ResponseUri.AbsolutePath.Contains( "my.php" ) ) {
                loggedIn = true;
            } else {
                loggedIn = false;
            }
        }

        public List<Torrent> TorrentSearch ( string query ) {
            wr = (HttpWebRequest)WebRequest.Create( "http://myxz.org/browse.php?search=" + query + "&genre=0&incldead=0" );
            wr.KeepAlive = true;
            wr.Method = "GET";
            wr.ContentType = "text/html";
            wr.Referer = "http://myxz.org/my.php";
            wr.CookieContainer = mycookie;

            response = (HttpWebResponse)wr.GetResponse();

            sReader = new StreamReader( response.GetResponseStream() );
            string strHTML = sReader.ReadToEnd();

            HtmlDocument html = new HtmlDocument();

            html.LoadHtml( strHTML );

            var doc = html.DocumentNode;

            List<Torrent> torrents = new List<Torrent>();
            var torrentrows = doc.QuerySelectorAll( "tr.browse" );
            foreach ( HtmlNode torrentrow in torrentrows ) {
                torrents.Add( new Torrent {
                    icon = torrentrow.QuerySelector( ":nth-child(1) img" ).GetAttributeValue( "class", "icon" )+".png",
                    title = torrentrow.QuerySelector( ":nth-child(2) b" ).InnerText,
                    size = torrentrow.QuerySelector( ":nth-child(6)" ).InnerText,
                    path = "http://myxz.org/" + torrentrow.QuerySelector( ":nth-child(3) a:last-child" ).GetAttributeValue( "href", "" ),
                    seed = torrentrow.QuerySelector( ":nth-child(7)" ).InnerText,
                    peer = torrentrow.QuerySelector( ":nth-child(8)" ).InnerText,
                    date = torrentrow.QuerySelector( ":nth-child(5) nobr" ).FirstChild.InnerHtml.Replace( "<br>", " " )
                } );
            }

            return torrents;
            //foreach (Torrent t in torrents) {
            //    Console.WriteLine( String.Format( "({0}) {1} - {2} => Seed:{3} Peer:{4}  ", t.size, t.title, t.path, t.seed, t.peer ) );
            //}

        }

        public void Download ( string path, string title ) {
            wr = (HttpWebRequest)WebRequest.Create( path );
            wr.KeepAlive = true;
            wr.Method = "GET";
            wr.Referer = "http://myxz.org/browse.php";
            wr.CookieContainer = mycookie;
            wr.Headers.Add( HttpRequestHeader.AcceptEncoding, "gzip,deflate" );
            wr.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            response = (HttpWebResponse)wr.GetResponse();
            sReader = new StreamReader( response.GetResponseStream(), Encoding.Default );
            string strHTML = sReader.ReadToEnd();
            System.IO.File.WriteAllText( pathDownload + title + ".torrent", strHTML, Encoding.Default );
            //Console.Write( strHTML );
            using ( Stream output = File.OpenWrite( pathDownload + title + ".torrent" ) )
                ( sReader.BaseStream ).CopyTo( output );

        }

        public void Init ( PluginInitContext context ) {
            _context = context;


            string assemblyFolder = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            string xmlFileName = Path.Combine( assemblyFolder, "login.xml" );

            XmlDocument doc = new XmlDocument();

            doc.Load( xmlFileName );

            XmlNode username = doc.DocumentElement.SelectSingleNode( "/login/username" );
            XmlNode password = doc.DocumentElement.SelectSingleNode( "/login/password" );

            login( username.InnerText, password.InnerText );
        }


        public List<Result> Query ( Query query ) {
            List<Result> result = new List<Result>();
            if ( loggedIn ) {
                List<Torrent> torrents = TorrentSearch( query.Search );

                foreach ( Torrent torrent in torrents ) {
                    result.Add( new Result() {
                        Title = torrent.title,
                        SubTitle = String.Format( "Seeders: {0} |      Peers: {1} |     Size: {2} |       Date:{3}", torrent.seed.PadRight( 10 ), torrent.peer.PadRight( 10 ), torrent.size.PadRight( 15 ), torrent.date ),
                        IcoPath = "icons\\"+torrent.icon,
                        Action = e => {
                            Download( torrent.path, torrent.title );
                            string pathUser = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
                            string pathDownload = Path.Combine( pathUser, "Downloads\\" );
                            System.Diagnostics.Process.Start( pathDownload + torrent.title + ".torrent" );
                            return true;
                        }
                    } );
                }
            } else {
                result.Add( new Result() {
                    Title = "Incorrect Username or Password",
                    SubTitle = "Click to edit",
                    IcoPath = "icon.png",
                    Action = e => {
                        string assemblyFolder = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
                        string xmlFileName = Path.Combine( assemblyFolder, "login.xml" );
                        Process.Start( xmlFileName );
                        return true;
                    }
                } );
            }

            return result;
        }
    }
}
