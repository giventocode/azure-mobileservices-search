using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Giventocode.AzureSearch.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var result = string.Empty;
            Console.WriteLine("Enter Search Criteria");
            var searchCriteria = Console.ReadLine();
            Console.WriteLine("Results");
            var t = Task.Run(async () => result = await SearchTodoItemsAsync(searchCriteria));
            t.Wait();

            Console.Write(t.Result);
            Console.ReadLine();
        }

        public async static Task<string> SearchTodoItemsAsync(string criteria)
        {
            using (var http = new HttpClient())
            {
                var serviceName = "YOUR SERVICE NAME";
                var indexName = "rockbandix";
                var apiKey = "YOUR KEY";
                var apiVersion = "2014-07-31-Preview";
                var uri = new Uri(string.Format("https://{0}.search.windows.net/indexes/{1}/docs?api-version={2}&search={3}", serviceName, indexName, apiVersion, criteria));

                http.DefaultRequestHeaders.Add("api-key", apiKey);

                var result = await http.GetStringAsync(uri);

                return result;

            }
        }
    }
}
