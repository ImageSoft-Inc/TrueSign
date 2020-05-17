using System;
using System.Threading.Tasks;
using TrueSign.Shared;

namespace ConnectionExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using(ApiClient trueSign = new ApiClient("<<Enter Client ID>>", "<<Enter Client Secret>>"))
            {
                Console.WriteLine("API Token:");
                Console.WriteLine(trueSign._ApiToken.Token);

                Console.WriteLine();
                Console.WriteLine("--------------------------------------------------------------------------");
                Console.WriteLine("Expires (UTC):");
                Console.WriteLine(trueSign._ApiToken.Expires_UTC);

                Console.WriteLine();
                Console.WriteLine("--------------------------------------------------------------------------");
                Console.WriteLine("Users with access to the authenticated envelope type:");
                var users = await trueSign.GetUsers();
                foreach (var user in users)
                {

                    Console.WriteLine($"{user.Name} ({user.Email})");
                }
            }

            Console.ReadLine();
        }
    }
}