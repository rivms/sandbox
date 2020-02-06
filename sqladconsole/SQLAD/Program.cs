using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Adal = Microsoft.IdentityModel.Clients.ActiveDirectory;
using Msal = Microsoft.Identity.Client;

/// <summary>
/// Based on: https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/894
/// </summary>
namespace SQLAD
{
	public class AzureADConnection
	{
		private readonly string TENANT_ID = Environment.GetEnvironmentVariable("TENANT_ID");
		private readonly string CLIENT_ID = Environment.GetEnvironmentVariable("CLIENT_ID"); 
		private readonly string CLIENT_SECRET = Environment.GetEnvironmentVariable("CLIENT_SECRET"); 
		private readonly string CONNECTION_STRING = Environment.GetEnvironmentVariable("CONNECTION_STRING"); 

		public async Task<string> GetAccessTokenAdal()
		{
			var creds = new Adal.ClientCredential(CLIENT_ID, CLIENT_SECRET);
			var ctx = new Adal.AuthenticationContext($"https://login.microsoftonline.com/{TENANT_ID}", false);
			var token = await ctx.AcquireTokenAsync("https://database.windows.net/", creds);
			return token.AccessToken;
		}

		public async Task<string> GetAccessTokenMsal()
		{
			// Source: https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Client-credential-flows
			var app = Msal.ConfidentialClientApplicationBuilder.Create(CLIENT_ID)
				.WithAuthority(Msal.AzureCloudInstance.AzurePublic, TENANT_ID)
				.WithClientSecret(CLIENT_SECRET)
				.Build();

			var res = await app.AcquireTokenForClient(new List<string>() { "https://database.windows.net//.default" }).ExecuteAsync();

			return res.AccessToken;
		}

		public IEnumerable<string> GetUsernames(string accessToken)
		{
			using (var connection = new SqlConnection(CONNECTION_STRING))
			{

				connection.AccessToken = accessToken;
				Console.WriteLine($"Access token for {CLIENT_ID} is:\n{connection.AccessToken}");

				connection.Open();
				var cmd = connection.CreateCommand();
				cmd.CommandText = "SELECT TOP 10 Name from Users";
				var reader = cmd.ExecuteReader();
				var names = new List<string>();

				while (reader.Read())
				{
					names.Add((string)reader["Name"]);
				}

				return names;
			}
		}

		public void PrintUserNames(IEnumerable<string> names)
		{
			foreach (var n in names)
			{
				Console.WriteLine(n);
			}
		}

		public static async Task Main(string[] args)
		{
			var program = new AzureADConnection();

			Console.WriteLine("Using ADAL");
			var adalAccessToken = await program.GetAccessTokenAdal();
			var namesAdal = program.GetUsernames(adalAccessToken);
			program.PrintUserNames(namesAdal);

			Console.WriteLine("----------");
			Console.WriteLine("Using MSAL");
			var msalAccessToken = await program.GetAccessTokenMsal();
			var namesMsal = program.GetUsernames(msalAccessToken);
			program.PrintUserNames(namesAdal);

		}
	}
}