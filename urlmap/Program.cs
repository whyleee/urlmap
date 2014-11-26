using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
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
            var lines = File.ReadAllLines(args[0])
                .Where(line => line.Contains('/')) // skip header
                .ToList();
            Stats.LineCount = lines.Count;

            var urlMap = lines.Select(line => new Redirect
                {
                    From = ExtractPath(line.Split('\t').First()),
                    To = ExtractPath(line.Split('\t').Last(), encodeQs: true),
                    Line = line
                })
                .FilterDuplicates()
                .DistinctByFrom()
                .ToList();
            Stats.UniqueCount = urlMap.Count;

            Console.WriteLine("----------------STATS---------------");
            Console.WriteLine("LINES: " + Stats.LineCount);
            Console.WriteLine("DUPLICATES: " + Stats.DuplicateCount);
            Console.WriteLine("REPEATS: " + Stats.RepeatCount);
            Console.WriteLine("OK: " + Stats.UniqueCount);
            Console.WriteLine("------------------------------------");

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

        private static string ExtractPath(string url, bool encodeQs = false)
        {
            var urlObject = new Uri(url, UriKind.RelativeOrAbsolute);
            var path = urlObject.ToString();

            if (urlObject.IsAbsoluteUri)
            {
                var server = urlObject.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
                path = path.Replace(server, "");
            }

            // add trailing slash
            if (!path.EndsWith("/") && !path.Contains('#') && !path.Contains('?'))
            {
                path = path + '/';
            }

            // encode query string
            if (encodeQs && path.Contains('?'))
            {
                var query = path.Substring(path.IndexOf('?') + 1);
                var pathWithoutQs = path.Substring(0, path.IndexOf('?') + 1);

                var qs = HttpUtility.ParseQueryString(query);
                var encodedQuery = "";

                foreach (string q in qs)
                {
                    encodedQuery += string.Format("{0}={1}&", q, Uri.EscapeUriString(qs[q]));
                }

                if (encodedQuery.Length > 0)
                {
                    encodedQuery = encodedQuery.Remove(encodedQuery.Length - 1);
                }

                return WebUtility.UrlDecode(pathWithoutQs) + encodedQuery;
            }

            return WebUtility.UrlDecode(path);
        }
    }

    public class Redirect
    {
        public string From { get; set; }

        public string To { get; set; }

        public string Line { get; set; }
    }

    public static class Stats
    {
        public static int LineCount { get; set; }

        public static int UniqueCount { get; set; }

        public static int DuplicateCount { get; set; }

        public static int RepeatCount { get; set; }
    }

    static class Extensions
    {
        // remove 'from' == 'to' duplicates
        public static IEnumerable<Redirect> FilterDuplicates(this IEnumerable<Redirect> redirects)
        {
            return redirects.Where(redirect =>
            {
                var ok = string.Compare(redirect.From, redirect.To, StringComparison.OrdinalIgnoreCase) != 0;

                if (!ok)
                {
                    Console.WriteLine("DUPLICATE: {0} --> {1}", redirect.From, redirect.To);
                    Stats.DuplicateCount++;
                }

                return ok;
            });
        }

        // distinct by 'from' url path
        public static IEnumerable<Redirect> DistinctByFrom(this IEnumerable<Redirect> redirects)
        {
            return redirects.GroupBy(redirect => redirect.From.ToLower())
                .Select(group =>
                {
                    var unique = group.First();
                    var repeated = group.Count() - 1;

                    if (repeated > 0)
                    {
                        Console.WriteLine("REPEAT ({0}): {1} --> {2}", repeated, unique.From, unique.To);
                        Stats.RepeatCount += repeated;
                    }

                    return unique;
                });
        }
    }
}
