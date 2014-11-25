using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace urlmap
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("ERROR: no input file. Provide path to the txt file with url map.");
                Console.WriteLine("Example contents:");
                Console.WriteLine();
                Console.WriteLine("/some/old/url/   /new/url");
                Console.WriteLine("/another/url   /another/new/url");
                Console.WriteLine("...   ...");
                Console.WriteLine();
                Console.WriteLine("NOTE: hostnames will be omitted in result xml url map.");
                Environment.Exit(-1);
            }

            var txtPath = args[0];
            var mapName = Path.GetFileNameWithoutExtension(txtPath);
            var urlMap = File.ReadAllLines(args[0])
                .Where(line => line.Contains('/')) // skip header
                .Select(line => new
                {
                    From = ExtractPath(line.Split(null).First()),
                    To = ExtractPath(line.Split(null).Last()),
                    Line = line
                })
                // remove 'from' == 'to' duplicates
                .Where(redirect => string.Compare(redirect.From, redirect.To, StringComparison.OrdinalIgnoreCase) != 0)
                // distinct by 'from' url path
                .GroupBy(redirect => redirect.From.ToLower())
                .Select(group => group.First())
                .ToList();

            Console.WriteLine("Found {0} unique urls. Processing...", urlMap.Count);

            var xmlMapCol = new XElement("rewriteMap",
                new XAttribute("name", mapName)
            );
            var xmlMap = new XDocument(
                new XElement("rewriteMaps",
                    xmlMapCol
            ));

            foreach (var redirect in urlMap)
            {
                xmlMapCol.Add(new XElement("add",
                    new XAttribute("key", redirect.From),
                    new XAttribute("value", redirect.To.ToLower()) // SEO: lowercase urls
                ));
            }

            xmlMap.Save("RewriteMaps.config");
            Console.WriteLine("OK! \"RewriteMaps.config\" created successfully.");
        }

        private static string ExtractPath(string url)
        {
            var urlObject = new Uri(url, UriKind.RelativeOrAbsolute);
            var path = urlObject.IsAbsoluteUri ? urlObject.AbsolutePath : urlObject.ToString();

            // remove query string
            if (path.Contains('?'))
            {
                path = path.Substring(0, path.IndexOf('?'));
            }

            // add trailing slash
            if (!path.EndsWith("/") && !path.Contains('#'))
            {
                path = path + '/';
            }

            return WebUtility.UrlDecode(path);
        }
    }
}
