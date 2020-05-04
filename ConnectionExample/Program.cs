using System;
using System.Threading.Tasks;
using TrueSign.Shared;

namespace ConnectionExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using(ApiClient api = new ApiClient("6f943f3a-3310-4487-a098-8774ea6e7352", "57nKSHNdA3UEX7boV4ZXnjPYMfl+gf2ai6zP+SG3"))
            {
                Console.WriteLine("API Token:");
                Console.WriteLine(api._ApiToken.Token);

                Console.WriteLine();
                Console.WriteLine("Expires (UTC):");
                Console.WriteLine(api._ApiToken.Expires_UTC);

                Console.WriteLine();
                Console.WriteLine("Users with envelope access:");
                var users = await api.GetUsers();
                foreach (var user in users)
                {

                    Console.WriteLine($"{user.Name} ({user.Email})");
                }
            }

            Console.ReadLine();
        }
    }
}
