using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCoreMicroservice
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json",
                             optional: true,
                             reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseConfiguration(config)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .ConfigureLogging(l => l.AddConsole(config.GetSection("Logging")))
                .ConfigureServices(s => s.AddRouting())
                .Configure(app =>
                {
                    app.UseRouter(r =>
                    {
                        var contactRepo = new InMemoryContactRepository();

                        r.MapGet("contacts", async (request, response, routeData) =>
                        {
                            var contacts = await contactRepo.GetAll();
                            await response.WriteJson(contacts);
                        });

                        r.MapGet("contacts/{id:int}", async (request, response, routeData) =>
                         {
                             var contact = await contactRepo.Get(Convert.ToInt32(routeData.Values["id"]));
                             if (contact == null)
                             {
                                 response.StatusCode = 404;
                                 return;
                             }

                             await response.WriteJson(contact);
                         });

                        r.MapPost("contacts", async (request, response, routeData) =>
                        {
                            var newContact = await request.HttpContext.ReadFromJson<Contact>();

                            if (newContact == null) return;

                            await contactRepo.Add(newContact);

                            response.StatusCode = 201;
                            await response.WriteJson(newContact);
                        });

                        r.MapPut("contacts/{id:int}", async (request, response, routeData) =>
                         {
                             var updatedContact = await request.HttpContext.ReadFromJson<Contact>();

                             if (updatedContact == null) return;

                             updatedContact.ContactId = Convert.ToInt32(routeData.Values["id"]);
                             await contactRepo.Update(updatedContact);

                             response.StatusCode = 204;
                         });

                        r.MapDelete("contacts/{id:int}", async (request, response, routeData) =>
                        {
                            await contactRepo.Delete(Convert.ToInt32(routeData.Values["id"]));
                            response.StatusCode = 204;
                        });
                    });
                })
                .Build();

            host.Run();
        }
    }

    public static class HttpExtensions
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        public static Task WriteJson<T>(this HttpResponse response, T obj)
        {
            response.ContentType = "application/json";
            return response.WriteAsync(JsonConvert.SerializeObject(obj));
        }

        public static async Task<T> ReadFromJson<T>(this HttpContext httpContext)
        {
            using (var streamReader = new StreamReader(httpContext.Request.Body))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                var obj = Serializer.Deserialize<T>(jsonTextReader);

                var results = new List<ValidationResult>();
                if (Validator.TryValidateObject(obj, new ValidationContext(obj), results))
                {
                    return obj;
                }

                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteJson(results);

                return default(T);
            }
        }
    }
}
